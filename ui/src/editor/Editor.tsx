import React, { useState } from "react";
import {
    type Workspace,
    type VegetationSet,
    type AgeProfile,
    type Climate,
    setSchema,
} from "../domain/schema";
import {
    catalog,
    clone,
    modified,
    resolveClimate,
    targetOf,
    type Command,
} from "../domain/store";
import { Foldout, Modal, NumberField, useControls } from "./controls";
export interface EditorProps {
    state: Workspace;
    selectedId: string | null;
    send: (command: Command) => void;
    select: (id: string | null) => void;
    beginDrawing: (edit: boolean) => void;
    drawing: boolean;
    error: string | null;
}
const climateFields = [
    ["referenceTemperatureC", "Reference temperature (°C)", -100, 100, 0.5],
    ["referenceElevationM", "Reference elevation (m)", -2000, 20000, 10],
    ["coolingCPerKm", "Cooling (°C/km)", 0, 20, 0.1],
] as const;
function Limits({
    value,
    onChange,
}: {
    value: VegetationSet["limits"];
    onChange: (v: VegetationSet["limits"]) => void;
}) {
    return (
        <>
            <NumberField
                label="Minimum elevation (m)"
                value={value.minElevationM}
                min={-2000}
                max={20000}
                onChange={(v) => onChange({ ...value, minElevationM: v })}
            />
            <NumberField
                label="Maximum elevation (m)"
                value={value.maxElevationM}
                min={-2000}
                max={20000}
                onChange={(v) => onChange({ ...value, maxElevationM: v })}
            />
            <NumberField
                label="Maximum slope (°)"
                value={value.maxSlopeDegrees}
                min={0}
                max={90}
                onChange={(v) => onChange({ ...value, maxSlopeDegrees: v })}
            />
        </>
    );
}
function AgeEditor({
    age,
    change,
}: {
    age: AgeProfile;
    change: (a: AgeProfile) => void;
}) {
    const { Button } = useControls();
    return (
        <Foldout
            title={`${age.name} · ${age.enabled ? "enabled" : "excluded"}`}
        >
            <Button
                onSelect={() => change({ ...age, enabled: !age.enabled })}
                selected={age.enabled}
            >
                {age.enabled ? "Exclude this age" : "Include this age"}
            </Button>
            <NumberField
                label="Age density (%)"
                value={age.densityPercent}
                min={0}
                max={100}
                onChange={(v) => change({ ...age, densityPercent: v })}
            />
            {(
                [
                    [
                        "footprintRadiusM",
                        "Physical footprint radius (m)",
                        0.01,
                        100,
                    ],
                    ["heightM", "Height (m)", 0.01, 200],
                    ["canopyRadiusM", "Canopy radius (m)", 0, 100],
                    ["canopyOpacityPercent", "Canopy opacity (%)", 0, 100],
                    ["shadeTolerancePercent", "Shade tolerance (%)", 0, 100],
                ] as const
            ).map(([key, label, min, max]) => (
                <NumberField
                    key={key}
                    label={label}
                    value={age[key]}
                    min={min}
                    max={max}
                    step={0.1}
                    onChange={(v) => change({ ...age, [key]: v })}
                />
            ))}
            {(["temperature", "slope"] as const).map((key) => (
                <Foldout
                    key={key}
                    title={
                        key === "temperature"
                            ? "Temperature response (°C)"
                            : "Slope response (°)"
                    }
                >
                    <p className="td-help">
                        Triangular response: zero at minimum/maximum, strongest
                        at optimum. Placeholder model.
                    </p>
                    {(["min", "optimum", "max"] as const).map((part) => (
                        <NumberField
                            key={part}
                            label={`${part} ${key === "temperature" ? "(°C)" : "(°)"}`}
                            value={age[key][part]}
                            min={key === "slope" ? 0 : -100}
                            max={key === "slope" ? 90 : 100}
                            step={0.5}
                            onChange={(v) =>
                                change({
                                    ...age,
                                    [key]: { ...age[key], [part]: v },
                                })
                            }
                        />
                    ))}
                    <svg
                        className="td-curve"
                        viewBox="0 0 200 45"
                        aria-label={`${key} response curve`}
                        role="img"
                    >
                        <path
                            d={`M 5 40 L ${5 + (190 * (age[key].optimum - age[key].min)) / (age[key].max - age[key].min)} 5 L 195 40`}
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2"
                        />
                    </svg>
                </Foldout>
            ))}
            <Foldout title="Hard placement limits">
                <Limits
                    value={age.limits}
                    onChange={(limits) => change({ ...age, limits })}
                />
            </Foldout>
        </Foldout>
    );
}
export function Editor({
    state,
    selectedId,
    send,
    select,
    beginDrawing,
    drawing,
    error,
}: EditorProps) {
    const { Panel, Button, Choice } = useControls();
    const target = targetOf(state, selectedId),
        set = target.working;
    const district = state.districts.find((d) => d.id === selectedId);
    const changed = modified(target);
    const resolved = resolveClimate(state, set);
    const entry = state.library.find((s) => s.id === target.libraryId);
    const [query, setQuery] = useState("");
    const [dialog, setDialog] = useState<"save" | "replace" | "delete" | null>(
        null,
    );
    const [pending, setPending] = useState<string | null>(null);
    const [saveName, setSaveName] = useState("");
    const [saveError, setSaveError] = useState("");
    const edit = (next: VegetationSet) => {
        const result = setSchema.safeParse(next);
        if (!result.success) throw new Error(result.error.issues[0].message);
        send({ type: "editSet", districtId: selectedId, set: result.data });
    };
    const choose = (libraryId: string) => {
        if (changed) {
            setPending(libraryId);
            setDialog("replace");
        } else send({ type: "chooseSet", districtId: selectedId, libraryId });
    };
    const openSave = () => {
        setSaveName(set.name);
        setSaveError("");
        setDialog("save");
    };
    const save = (newCopy: boolean) => {
        try {
            send({
                type: "saveSet",
                districtId: selectedId,
                name: saveName,
                newId: newCopy
                    ? `user:${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`
                    : undefined,
                afterSaveLibraryId: pending ?? undefined,
            });
            setPending(null);
            setDialog(null);
        } catch (e) {
            setSaveError(e instanceof Error ? e.message : "Could not save set");
        }
    };
    return (
        <Panel className="td-editor" header="Vegetation districts">
            <div
                className="td-editor-content"
                onClick={(e) => e.stopPropagation()}
                onPointerDown={(e) => e.stopPropagation()}
                onWheel={(e) => e.stopPropagation()}
            >
                <div className="td-eyebrow">
                    {district ? "Selected district" : "New district"}
                </div>
                {district && (
                    <label className="td-field">
                        <span>District name</span>
                        <input
                            aria-label="District name"
                            value={district.name}
                            maxLength={80}
                            onChange={(e) => {
                                if (e.target.value.trim())
                                    send({
                                        type: "district",
                                        districtId: district.id,
                                        name: e.target.value,
                                    });
                            }}
                        />
                    </label>
                )}
                <h2>
                    {set.name}
                    {changed && (
                        <span title="Modified set — saved with this district; save to reuse elsewhere.">
                            {" "}
                            *
                        </span>
                    )}
                </h2>
                <label className="td-field">
                    <span>Find a vegetation set</span>
                    <input
                        value={query}
                        onChange={(e) => setQuery(e.target.value)}
                        placeholder="Search built-in and my sets"
                    />
                </label>
                <Choice
                    label="Vegetation set"
                    value={target.libraryId ?? ""}
                    options={state.library
                        .filter(
                            (s) =>
                                s.set.name
                                    .toLowerCase()
                                    .includes(query.toLowerCase()) ||
                                s.id === target.libraryId,
                        )
                        .map((s) => ({
                            value: s.id,
                            label: `${s.builtIn ? "Built-in" : "My sets"} · ${s.set.name}`,
                        }))}
                    onChange={choose}
                />
                <div className="td-actions">
                    <Button
                        onSelect={() => {
                            setPending(null);
                            openSave();
                        }}
                    >
                        Save as…
                    </Button>
                    <Button
                        disabled={!entry || entry.builtIn || !changed}
                        onSelect={() =>
                            send({
                                type: "saveSet",
                                districtId: selectedId,
                                name: set.name,
                            })
                        }
                    >
                        Save set
                    </Button>
                    <Button
                        onSelect={() => {
                            if (changed) {
                                setPending("__new");
                                setDialog("replace");
                            } else
                                send({
                                    type: "newSet",
                                    districtId: selectedId,
                                });
                        }}
                    >
                        New set
                    </Button>
                </div>
                <NumberField
                    label="Overall density (%)"
                    value={set.overallDensityPercent}
                    min={0}
                    max={100}
                    onChange={(v) => edit({ ...set, overallDensityPercent: v })}
                />
                <div className="td-actions">
                    <Button
                        disabled={drawing}
                        onSelect={() => beginDrawing(!!district)}
                    >
                        {district ? "Redraw boundary" : "Draw district"}
                    </Button>
                    {district && (
                        <Button onSelect={() => select(null)}>
                            New district
                        </Button>
                    )}
                </div>
                <p className="td-help">
                    {district
                        ? "Valid edits are retained with this district."
                        : "Choose a set, then draw its boundary."}{" "}
                    No vegetation is generated in this scaffold.
                </p>
                <Foldout
                    title={`Set settings · ${set.assets.length} vegetation assets`}
                >
                    <NumberField
                        label="Clumping (%)"
                        value={set.clumpingPercent}
                        min={0}
                        max={100}
                        onChange={(v) => edit({ ...set, clumpingPercent: v })}
                    />
                    <Foldout title="Climate · city defaults or overrides">
                        {climateFields.map(([key, label, min, max, step]) => (
                            <div key={key} className="td-override">
                                <label>
                                    <input
                                        type="checkbox"
                                        checked={set.climate[key] === undefined}
                                        onChange={(e) => {
                                            const climate = { ...set.climate };
                                            if (e.target.checked)
                                                delete climate[key];
                                            else
                                                climate[key] =
                                                    resolved.climate[key];
                                            edit({ ...set, climate });
                                        }}
                                    />{" "}
                                    Use city default: {label}
                                </label>
                                {set.climate[key] === undefined ? (
                                    <p className="td-help">
                                        Resolved: {resolved.climate[key]} ·
                                        inherited
                                    </p>
                                ) : (
                                    <NumberField
                                        label={label}
                                        min={min}
                                        max={max}
                                        step={step}
                                        value={set.climate[key]!}
                                        onChange={(v) =>
                                            edit({
                                                ...set,
                                                climate: {
                                                    ...set.climate,
                                                    [key]: v,
                                                },
                                            })
                                        }
                                    />
                                )}
                            </div>
                        ))}
                        {resolved.warning && (
                            <p role="status">{resolved.warning}</p>
                        )}
                    </Foldout>
                    <Foldout title="Set hard placement limits">
                        <Limits
                            value={set.limits}
                            onChange={(limits) => edit({ ...set, limits })}
                        />
                    </Foldout>
                    <h3>Vegetation</h3>
                    <p className="td-help">
                        Fictional assets and values for testing the editor.
                        Research and actual game asset selection come later.
                    </p>
                    {set.assets.map((asset) => (
                        <div key={asset.id} className="td-asset">
                            <strong>{asset.name}</strong>
                            <span className="td-badge">
                                {asset.placeholder
                                    ? "Placeholder"
                                    : "Game asset"}
                            </span>
                            <NumberField
                                label={`${asset.name} density (%)`}
                                value={asset.densityPercent}
                                min={0}
                                max={100}
                                onChange={(v) =>
                                    edit({
                                        ...set,
                                        assets: set.assets.map((a) =>
                                            a.id === asset.id
                                                ? { ...a, densityPercent: v }
                                                : a,
                                        ),
                                    })
                                }
                            />
                            <p className="td-help">
                                {Math.round(
                                    (set.overallDensityPercent *
                                        asset.densityPercent) /
                                        100,
                                )}
                                % of baseline opportunities, before
                                age/suitability.
                            </p>
                            <Foldout title="Age profiles & placement">
                                {asset.ages.map((age) => (
                                    <AgeEditor
                                        key={age.id}
                                        age={age}
                                        change={(updated) =>
                                            edit({
                                                ...set,
                                                assets: set.assets.map((a) =>
                                                    a.id === asset.id
                                                        ? {
                                                              ...a,
                                                              ages: a.ages.map(
                                                                  (p) =>
                                                                      p.id ===
                                                                      age.id
                                                                          ? updated
                                                                          : p,
                                                              ),
                                                          }
                                                        : a,
                                                ),
                                            })
                                        }
                                    />
                                ))}
                                <Button
                                    onSelect={() => {
                                        const defaults = catalog.assets.find(
                                            (a) => a.id === asset.id,
                                        );
                                        if (defaults)
                                            edit({
                                                ...set,
                                                assets: set.assets.map((a) =>
                                                    a.id === asset.id
                                                        ? clone(defaults)
                                                        : a,
                                                ),
                                            });
                                    }}
                                >
                                    Reset asset to supplied values
                                </Button>
                            </Foldout>
                            <Button
                                onSelect={() =>
                                    edit({
                                        ...set,
                                        assets: set.assets.filter(
                                            (a) => a.id !== asset.id,
                                        ),
                                    })
                                }
                            >
                                Remove {asset.name}
                            </Button>
                        </div>
                    ))}
                    {catalog.assets
                        .filter((a) => !set.assets.some((x) => x.id === a.id))
                        .map((a) => (
                            <Button
                                key={a.id}
                                onSelect={() =>
                                    edit({
                                        ...set,
                                        assets: [...set.assets, clone(a)],
                                    })
                                }
                            >
                                Add {a.name}
                            </Button>
                        ))}
                    {!set.assets.length && (
                        <p role="status">
                            No vegetation selected. Add an asset to configure
                            this set.
                        </p>
                    )}
                </Foldout>
                {district && (
                    <Foldout title="District actions" initial>
                        <Choice
                            label="Generation mode"
                            value={district.generationMode}
                            options={[
                                {
                                    value: "onDemand",
                                    label: "On demand (default)",
                                },
                                {
                                    value: "auto",
                                    label: "Auto-generate (setting only)",
                                },
                            ]}
                            onChange={(v) =>
                                send({
                                    type: "district",
                                    districtId: district.id,
                                    generationMode: v as "auto" | "onDemand",
                                })
                            }
                        />
                        <p className="td-help" role="status">
                            Not generated · generator not implemented
                        </p>
                        <div className="td-actions">
                            {[
                                "Generate",
                                "Regenerate",
                                "Ungenerate",
                                "Bake + delete",
                            ].map((label) => (
                                <Button
                                    key={label}
                                    disabled
                                    title="Available when generation and ownership are implemented"
                                >
                                    {label}
                                </Button>
                            ))}
                            <Button
                                className="td-danger"
                                onSelect={() => setDialog("delete")}
                            >
                                Delete district
                            </Button>
                        </div>
                    </Foldout>
                )}
                <Foldout title="City defaults">
                    <Choice
                        label="Temperature source"
                        value={state.city.source}
                        options={[
                            {
                                value: "manual",
                                label: "Manual reference temperature",
                            },
                            { value: "game", label: "Stable game climate" },
                        ]}
                        onChange={(source) =>
                            send({
                                type: "city",
                                city: {
                                    ...state.city,
                                    source: source as Climate["source"],
                                },
                            })
                        }
                    />
                    {state.city.source === "game" && (
                        <p className="td-help">
                            {state.gameTemperatureC === null
                                ? "Game climate unavailable in this host; manual temperature below is the fallback."
                                : `Stable climate: ${state.gameTemperatureC} °C`}
                        </p>
                    )}
                    {climateFields.map(([key, label, min, max, step]) => (
                        <NumberField
                            key={key}
                            label={label}
                            value={state.city[key]}
                            min={min}
                            max={max}
                            step={step}
                            onChange={(v) =>
                                send({
                                    type: "city",
                                    city: { ...state.city, [key]: v },
                                })
                            }
                        />
                    ))}
                </Foldout>
                {error && (
                    <p className="td-error" role="alert">
                        {error}
                    </p>
                )}
                {dialog && (
                    <Modal
                        title={
                            dialog === "save"
                                ? "Save vegetation set"
                                : dialog === "replace"
                                  ? "Replace modified set"
                                  : "Delete district"
                        }
                        onClose={() => {
                            setDialog(null);
                            setPending(null);
                        }}
                    >
                        {dialog === "replace" && (
                            <>
                                <h3>Replace modified set?</h3>
                                <p>
                                    These edits are stored in the district, but
                                    choosing another set will replace them.
                                </p>
                                <Button
                                    onSelect={() => {
                                        setSaveName(set.name);
                                        setDialog("save");
                                    }}
                                >
                                    Save as first…
                                </Button>
                                <Button
                                    onSelect={() => {
                                        if (pending === "__new")
                                            send({
                                                type: "newSet",
                                                districtId: selectedId,
                                            });
                                        else if (pending)
                                            send({
                                                type: "chooseSet",
                                                districtId: selectedId,
                                                libraryId: pending,
                                            });
                                        setPending(null);
                                        setDialog(null);
                                    }}
                                >
                                    Replace
                                </Button>
                            </>
                        )}
                        {dialog === "save" && (
                            <>
                                <h3>Save vegetation set</h3>
                                <label className="td-field">
                                    <span>Set name</span>
                                    <input
                                        autoFocus
                                        maxLength={80}
                                        value={saveName}
                                        onChange={(e) =>
                                            setSaveName(e.target.value)
                                        }
                                    />
                                </label>
                                <Button
                                    disabled={!saveName.trim()}
                                    onSelect={() => save(true)}
                                >
                                    Save new copy
                                </Button>
                                {saveError && <p role="alert">{saveError}</p>}
                            </>
                        )}
                        {dialog === "delete" && (
                            <>
                                <h3>Delete {district?.name}?</h3>
                                <p>
                                    Remove this district and its settings. There
                                    are no generated plants in this scaffold.
                                </p>
                                <Button
                                    className="td-danger"
                                    onSelect={() => {
                                        if (district)
                                            send({
                                                type: "deleteDistrict",
                                                districtId: district.id,
                                            });
                                        select(null);
                                        setDialog(null);
                                    }}
                                >
                                    Delete permanently
                                </Button>
                            </>
                        )}
                        <Button
                            onSelect={() => {
                                setDialog(null);
                                setPending(null);
                            }}
                        >
                            Cancel
                        </Button>
                    </Modal>
                )}
            </div>
        </Panel>
    );
}
