import React from "react";
import { bindValue, useValue, trigger } from "cs2/api";
import {
    Panel,
    Button,
    Icon,
    Dropdown,
    DropdownItem,
    DropdownToggle,
    Scrollable,
} from "cs2/ui";
import type { ModRegistrar } from "cs2/modding";
import { getModule } from "cs2/modding";
import { z } from "zod";
import { Controls, type Primitives } from "../editor/controls";
import { Editor } from "../editor/Editor";
import { workspaceSchema } from "../domain/schema";
import "../editor/style.css";

// No preview store, localStorage, fictional canvas, ReactDOM root or game mutation here.
// The C# host owns this snapshot and applies queued, validated commands during its update.
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
    Checkbox: ({ label, checked, onChange }) => (
        <native.Choice
            label={label}
            value={checked ? "inherit" : "override"}
            options={[
                { value: "inherit", label: "Use city default" },
                { value: "override", label: "Override for this set" },
            ]}
            onChange={(value) => onChange(value === "inherit")}
        />
    ),
    Input: (props) => {
        // Use the installed game's keyboard/gamepad-aware text field.
        const Input = getModule(
            "game-ui/common/input/text/text-input.tsx",
            "TextInput",
        ) as React.ComponentType<React.InputHTMLAttributes<HTMLInputElement>>;
        if (props.type === "checkbox") return <input {...props} />;
        return <Input {...props} type="text" className="td-input" />;
    },
    Panel: ({ children, ...props }) => (
        <Panel {...props} onClose={() => send({ type: "togglePanel" })}>
            <Scrollable vertical className="td-game-scroll">
                {children}
            </Scrollable>
        </Panel>
    ),
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
            tooltipLabel="Tree Areas"
            className="td-entry"
        >
            <Icon className="td-icon" src="coui://treedistrict/TreeArea.svg" />
            Tree Areas
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
                    topContent={
                        <>
                            <native.Choice
                                label="Tree Area"
                                value={envelope.selectedId ?? ""}
                                options={[
                                    { value: "", label: "New Tree Area" },
                                    ...envelope.workspace.districts.map(
                                        (d) => ({
                                            value: d.id,
                                            label: d.name,
                                        }),
                                    ),
                                ]}
                                onChange={(id) =>
                                    send({
                                        type: "selectDistrict",
                                        id: id || null,
                                    })
                                }
                            />
                        </>
                    }
                    generationAction={(type, districtId, confirmed) =>
                        send({ type, districtId, confirmed })
                    }
                    nativeAreaTools
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
        registry.append("GameTopLeft", Entry);
        registry.append("Game", GamePanel);
    } catch (error) {
        console.error("TreeDistrict UI registration failed", error);
    }
};
export default register;
