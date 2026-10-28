import React, { useState } from "react";
import {
    type Workspace,
    type VegetationSet,
    type VegetationAsset,
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
import {
    CheckboxField,
    Foldout,
    Modal,
    NumberField,
    TextField,
    useControls,
} from "./controls";
export interface EditorProps {
    state: Workspace;
    selectedId: string | null;
    send: (command: Command) => void;
    select: (id: string | null) => void;
    beginDrawing: (edit: boolean) => void;
    drawing: boolean;
    error: string | null;
    nativeAreaTools?: boolean;
    topContent?: React.ReactNode;
    generationAction?: (
        type: "generate" | "removeGenerated" | "bakeDelete",
        districtId: string,
        confirmed?: boolean,
    ) => void;
}
const climateFields = [
    ["referenceTemperatureC", "Reference temperature (°C)", -100, 100, 0.5],
    ["referenceElevationM", "Reference elevation (m)", -2000, 20000, 10],
    ["coolingCPerKm", "Cooling (°C/km)", 0, 20, 0.1],
] as const;
function Limits({
    value,
    onChange,
    showSlope = true,
}: {
    value: VegetationSet["limits"];
    onChange: (v: VegetationSet["limits"]) => void;
    showSlope?: boolean;
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
            {showSlope && (
                <NumberField
                    label="Maximum slope (°)"
                    value={value.maxSlopeDegrees}
                    min={0}
                    max={90}
                    onChange={(v) => onChange({ ...value, maxSlopeDegrees: v })}
                />
            )}
        </>
    );
}
function ProfileEditor({
    profile,
    change,
}: {
    profile: VegetationAsset["profile"];
    change: (profile: VegetationAsset["profile"]) => void;
}) {
    return (
        <Foldout title="Species profile · shared across ages">
            <p className="td-help">
                Normal centre spacing is canopy diameter plus spacing
                adjustment. Negative adjustment allows foliage overlap; minimum
                spacing is 0.1 m. Density scales this spacing. Native growth is
                unchanged.
            </p>
            {(
                [
                    ["spacingAdjustmentM", "Spacing adjustment (m)", -1, 1],
                    ["heightM", "Height (m)", 0.01, 200],
                    ["canopyRadiusM", "Canopy radius (m)", 0, 100],
                    ["canopyOpacityPercent", "Canopy opacity (%)", 0, 100],
                    ["shadeTolerancePercent", "Shade tolerance (%)", 0, 100],
                ] as const
            ).map(([key, label, min, max]) => (
                <NumberField
                    key={key}
                    label={label}
                    value={profile[key] ?? 0}
                    min={min}
                    max={max}
                    step={0.1}
                    onChange={(v) => change({ ...profile, [key]: v })}
                />
            ))}
            <NumberField
                label="Maximum slope for culling (°)"
                value={profile.limits.maxSlopeDegrees}
                min={0}
                max={90}
                onChange={(maxSlopeDegrees) =>
                    change({
                        ...profile,
                        limits: { ...profile.limits, maxSlopeDegrees },
                    })
                }
            />
            <p className="td-help">
                On generation, exclude placements above the lower of this limit
                and the set maximum slope. This is a hard cutoff, separate from
                the slope response below; existing plants remain until
                regeneration.
            </p>
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
                        at optimum. Estimated placement model.
                    </p>
                    {(["min", "optimum", "max"] as const).map((part) => (
                        <NumberField
                            key={part}
                            label={`${part} ${key === "temperature" ? "(°C)" : "(°)"}`}
                            value={profile[key][part]}
                            min={key === "slope" ? 0 : -100}
                            max={key === "slope" ? 90 : 100}
                            step={0.5}
                            onChange={(v) =>
                                change({
                                    ...profile,
                                    [key]: { ...profile[key], [part]: v },
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
                            d={`M 5 40 L ${5 + (190 * (profile[key].optimum - profile[key].min)) / (profile[key].max - profile[key].min)} 5 L 195 40`}
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2"
                        />
                    </svg>
                </Foldout>
            ))}
            <Foldout title="Hard placement limits">
                <Limits
                    value={profile.limits}
                    showSlope={false}
                    onChange={(limits) => change({ ...profile, limits })}
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
    nativeAreaTools = false,
    topContent,
    generationAction,
}: EditorProps) {
    const { Panel, Button, Choice } = useControls();
    const target = targetOf(state, selectedId),
        set = target.working;
    const district = state.districts.find((d) => d.id === selectedId);
    const areaM2 = district
        ? Math.abs(
              district.boundary.reduce((sum, point, i, points) => {
                  const next = points[(i + 1) % points.length];
                  return sum + point.x * next.z - next.x * point.z;
              }, 0),
          ) / 2
        : 0;
    const changed = modified(target);
    const resolved = resolveClimate(state, set);
    const entry = state.library.find((s) => s.id === target.libraryId);
    const [query, setQuery] = useState("");
    const [assetQuery, setAssetQuery] = useState("");
    const [dialog, setDialog] = useState<
        "save" | "replace" | "delete" | "bake" | "addVegetation" | null
    >(null);
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
                {topContent}
                <div className="td-eyebrow">
                    {district ? "Selected district" : "New district"}
                </div>
                {district && (
                    <label className="td-field">
                        <span>District name</span>
                        <TextField
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
                    <TextField
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
                    label="Density multiplier"
                    value={set.overallDensityPercent / 100}
                    min={0}
                    max={2}
                    step={0.1}
                    onChange={(v) =>
                        edit({ ...set, overallDensityPercent: v * 100 })
                    }
                />
                <p className="td-help">
                    0 = empty; 1 = normal; 2 = twice the target density. Spacing
                    scales by 1 / √multiplier.
                </p>
                <div className="td-actions">
                    <Button
                        disabled={drawing}
                        onSelect={() => beginDrawing(!!district)}
                    >
                        {district
                            ? nativeAreaTools
                                ? "Edit boundary"
                                : "Redraw boundary"
                            : "Draw district"}
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
                    {generationAction
                        ? "Generate applies changes to the whole area when you choose."
                        : "Generation is available in the game."}
                </p>
                <Foldout
                    title={`Set settings · ${set.assets.length} vegetation assets`}
                >
                    <NumberField
                        label="Clumping (%) · reserved; use 0"
                        value={set.clumpingPercent}
                        min={0}
                        max={100}
                        onChange={(v) => edit({ ...set, clumpingPercent: v })}
                    />
                    <Foldout title="Climate · city defaults or overrides">
                        {climateFields.map(([key, label, min, max, step]) => (
                            <div key={key} className="td-override">
                                <CheckboxField
                                    label={label}
                                    checked={set.climate[key] === undefined}
                                    onChange={(checked) => {
                                        const climate = { ...set.climate };
                                        if (checked) delete climate[key];
                                        else
                                            climate[key] =
                                                resolved.climate[key];
                                        edit({ ...set, climate });
                                    }}
                                />
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
                    <Button
                        onSelect={() => {
                            setAssetQuery("");
                            setDialog("addVegetation");
                        }}
                    >
                        Add vegetation…
                    </Button>
                    <p className="td-help">
                        Profiles use measured mature footprints and estimated
                        temperature/slope responses. Preset overrides stay with
                        the set; scrub plants are visual proxies.
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
                                Relative selection weight before local
                                suitability. Zero excludes this asset; overall
                                density controls spacing across the set.
                            </p>
                            <ProfileEditor
                                profile={asset.profile}
                                change={(profile) =>
                                    edit({
                                        ...set,
                                        assets: set.assets.map((a) =>
                                            a.id === asset.id
                                                ? { ...a, profile }
                                                : a,
                                        ),
                                    })
                                }
                            />
                            <Foldout title="Initial age distribution">
                                <p className="td-help">
                                    Percentages must total 100 to generate. Zero
                                    excludes an age. This is a placement target;
                                    trees then age naturally.
                                </p>
                                {asset.ages.map((age) => (
                                    <NumberField
                                        key={age.id}
                                        label={age.name + " (%)"}
                                        min={0}
                                        max={100}
                                        value={age.proportionPercent}
                                        onChange={(v) =>
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
                                                                          ? {
                                                                                ...p,
                                                                                proportionPercent:
                                                                                    v,
                                                                            }
                                                                          : p,
                                                              ),
                                                          }
                                                        : a,
                                                ),
                                            })
                                        }
                                    />
                                ))}
                                <p role="status">
                                    Total:{" "}
                                    {Math.round(
                                        asset.ages.reduce(
                                            (sum, age) =>
                                                sum + age.proportionPercent,
                                            0,
                                        ) * 100,
                                    ) / 100}
                                    %
                                </p>
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
                    {!set.assets.length && (
                        <p role="status">
                            No vegetation selected. Add an asset to configure
                            this set.
                        </p>
                    )}
                </Foldout>
                {district && (
                    <Foldout title="District actions" initial>
                        {areaM2 > 25_000_000 && (
                            <p className="td-help" role="status">
                                Large area ({(areaM2 / 1_000_000).toFixed(1)}{" "}
                                km²). Generation may place millions of plants
                                and freeze the game while it finishes. There is
                                no TreeDistrict population cap.
                            </p>
                        )}
                        <p className="td-help" role="status">
                            {district.generatedCount ?? 0} linked trees. Manual
                            generation only.
                            {district.generatedRevision != null &&
                            district.generatedRevision !== district.revision
                                ? " Settings or boundary changed; regenerate to apply."
                                : ""}
                            {district.generationStatus}
                        </p>
                        <div className="td-actions">
                            <Button
                                disabled={!generationAction}
                                onSelect={() =>
                                    generationAction?.("generate", district.id)
                                }
                            >
                                {(district.generatedCount ?? 0) > 0
                                    ? "Regenerate"
                                    : "Generate"}
                            </Button>
                            <Button
                                disabled={
                                    !generationAction ||
                                    !(district.generatedCount ?? 0)
                                }
                                onSelect={() =>
                                    generationAction?.(
                                        "removeGenerated",
                                        district.id,
                                    )
                                }
                            >
                                Remove generated trees
                            </Button>
                            <Button
                                disabled={!generationAction}
                                onSelect={() => setDialog("bake")}
                            >
                                Bake + delete area
                            </Button>
                            <Button
                                className="td-danger"
                                onSelect={() => setDialog("delete")}
                            >
                                {nativeAreaTools
                                    ? "Delete area and its trees"
                                    : "Delete district"}
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
                            dialog === "bake"
                                ? "Bake and delete area"
                                : dialog === "save"
                                  ? "Save vegetation set"
                                  : dialog === "replace"
                                    ? "Replace modified set"
                                    : dialog === "addVegetation"
                                      ? "Add vegetation"
                                      : "Delete district"
                        }
                        onClose={() => {
                            setDialog(null);
                            setPending(null);
                        }}
                    >
                        {dialog === "addVegetation" && (
                            <>
                                <h3>Add vegetation</h3>
                                <p className="td-help">
                                    Field Maple is the first supported game
                                    asset.
                                </p>
                                <label className="td-field">
                                    <span>Search vegetation</span>
                                    <TextField
                                        value={assetQuery}
                                        onChange={(e) =>
                                            setAssetQuery(e.target.value)
                                        }
                                        placeholder="Search available assets"
                                    />
                                </label>
                                {catalog.assets
                                    .filter((a) =>
                                        a.name
                                            .toLowerCase()
                                            .includes(
                                                assetQuery.trim().toLowerCase(),
                                            ),
                                    )
                                    .map((asset) => {
                                        const included = set.assets.some(
                                            (a) => a.id === asset.id,
                                        );
                                        return (
                                            <div
                                                key={asset.id}
                                                className="td-asset"
                                            >
                                                <strong>{asset.name}</strong>
                                                {included ? (
                                                    <p className="td-help">
                                                        Already in this set
                                                    </p>
                                                ) : (
                                                    <p className="td-help">
                                                        {asset.ages.length} age
                                                        profile(s) · supplied
                                                        settings included
                                                    </p>
                                                )}
                                                <Button
                                                    disabled={included}
                                                    onSelect={() => {
                                                        edit({
                                                            ...set,
                                                            assets: [
                                                                ...set.assets,
                                                                clone(asset),
                                                            ],
                                                        });
                                                        setDialog(null);
                                                    }}
                                                >
                                                    Add {asset.name}
                                                </Button>
                                            </div>
                                        );
                                    })}
                                {!catalog.assets.some((a) =>
                                    a.name
                                        .toLowerCase()
                                        .includes(
                                            assetQuery.trim().toLowerCase(),
                                        ),
                                ) && (
                                    <p role="status">
                                        No vegetation matches your search.
                                    </p>
                                )}
                                {catalog.assets.every((a) =>
                                    set.assets.some((x) => x.id === a.id),
                                ) && (
                                    <p role="status">
                                        All available vegetation is already in
                                        this set.
                                    </p>
                                )}
                            </>
                        )}
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
                                    <TextField
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
                        {dialog === "bake" && (
                            <>
                                <h3>Bake and delete {district?.name}?</h3>
                                <p>
                                    The trees will remain as ordinary world
                                    trees. Their ownership links, this area and
                                    its settings will be removed. This area
                                    cannot regenerate them afterward.
                                </p>
                                <Button
                                    className="td-danger"
                                    onSelect={() => {
                                        if (district)
                                            generationAction?.(
                                                "bakeDelete",
                                                district.id,
                                                true,
                                            );
                                        setDialog(null);
                                    }}
                                >
                                    Confirm bake + delete
                                </Button>
                            </>
                        )}
                        {dialog === "delete" && (
                            <>
                                <h3>Delete {district?.name}?</h3>
                                <p>
                                    Remove this area, its settings and all
                                    surviving trees it generated. Other trees
                                    remain untouched.
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
