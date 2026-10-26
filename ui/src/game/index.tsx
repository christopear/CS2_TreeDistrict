import React from "react";
import { bindValue, useValue, trigger } from "cs2/api";
import {
    Panel,
    Button,
    Icon,
    Dropdown,
    DropdownItem,
    DropdownToggle,
} from "cs2/ui";
import type { ModRegistrar } from "cs2/modding";
import { z } from "zod";
import { Controls, type Primitives } from "../editor/controls";
import { Editor } from "../editor/Editor";
import { workspaceSchema } from "../domain/schema";
import "../editor/style.css";

// No preview store, localStorage, fictional canvas, ReactDOM root or game mutation here.
// A future C# host owns this snapshot and executes validated commands on the simulation thread.
const snapshot = bindValue("TreeDistrict", "editor", "");
const envelopeSchema = z.object({
    protocolVersion: z.literal(1),
    visible: z.boolean(),
    workspace: workspaceSchema,
    selectedId: z.string().nullable(),
    drawing: z.boolean(),
    error: z.string().nullable(),
});
const native: Primitives = {
    Panel: ({ children, ...props }) => <Panel {...props}>{children}</Panel>,
    Button: ({ title, ...props }) => <Button {...props} tooltipLabel={title} />,
    Choice: ({ label, value, options, onChange }) => (
        <div className="td-field">
            <span>{label}</span>
            <Dropdown
                content={
                    <>
                        {options.map((o) => (
                            <DropdownItem
                                key={o.value}
                                value={o.value}
                                selected={value === o.value}
                                onChange={onChange}
                            >
                                {o.label}
                            </DropdownItem>
                        ))}
                    </>
                }
            >
                <DropdownToggle>
                    {options.find((o) => o.value === value)?.label ?? "Choose…"}
                </DropdownToggle>
            </Dropdown>
        </div>
    ),
};
function send(command: unknown) {
    trigger("TreeDistrict", "command", JSON.stringify(command));
}
function Entry() {
    return (
        <Button
            onSelect={() => send({ type: "togglePanel" })}
            tooltipLabel="Vegetation districts"
        >
            <Icon className="td-icon" src="coui://uil/Standard/Trees.svg" />
            Vegetation districts
        </Button>
    );
}
function GamePanel() {
    const json = useValue(snapshot);
    if (!json) return null;
    let envelope: z.infer<typeof envelopeSchema>;
    try {
        envelope = envelopeSchema.parse(JSON.parse(json));
    } catch {
        return (
            <Panel header="TreeDistrict">
                The settings host sent an incompatible snapshot. Check the game
                log.
            </Panel>
        );
    }
    if (!envelope.visible) return null;
    if (
        envelope.selectedId !== null &&
        !envelope.workspace.districts.some((d) => d.id === envelope.selectedId)
    )
        return (
            <Panel header="TreeDistrict">
                Waiting for district selection to refresh.
            </Panel>
        );
    return (
        <div className="td-game-panel">
            <Controls.Provider value={native}>
                <Editor
                    key={envelope.selectedId ?? "draft"}
                    state={envelope.workspace}
                    selectedId={envelope.selectedId}
                    send={send}
                    select={(id) => send({ type: "selectDistrict", id })}
                    beginDrawing={(edit) =>
                        send({ type: "beginDrawing", edit })
                    }
                    drawing={envelope.drawing}
                    error={envelope.error}
                />
            </Controls.Provider>
        </div>
    );
}
export const hasCSS = true;
const register: ModRegistrar = (registry) => {
    try {
        registry.append("UniversalModMenu", Entry);
        registry.append("Game", GamePanel);
    } catch (error) {
        console.error("TreeDistrict UI registration failed", error);
    }
};
export default register;
