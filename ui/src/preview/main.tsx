import React, { useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { Controls, type Primitives } from "../editor/controls";
import { Editor } from "../editor/Editor";
import {
    deserialize,
    initialWorkspace,
    polygonError,
    reduce,
    serialize,
    type Command,
} from "../domain/store";
import { type Workspace, type Point } from "../domain/schema";
import "../editor/style.css";
import "./preview.css";
const storageKey = "TreeDistrict.preview.city.v1";
const controls: Primitives = {
    Button: ({
        children,
        onSelect,
        disabled,
        selected,
        title,
        className,
        ...accessibility
    }) => (
        <button
            {...accessibility}
            type="button"
            className={className}
            onClick={onSelect}
            disabled={disabled}
            aria-pressed={selected}
            title={title}
        >
            {children}
        </button>
    ),
    Panel: ({ children, header, className }) => (
        <aside className={className}>
            <header className="td-panel-header">{header}</header>
            {children}
        </aside>
    ),
    Choice: ({ label, value, options, onChange }) => (
        <label className="td-field">
            <span>{label}</span>
            <select
                aria-label={label}
                value={value}
                onChange={(e) => onChange(e.target.value)}
            >
                <option value="" disabled>
                    Choose…
                </option>
                {options.map((o) => (
                    <option key={o.value} value={o.value}>
                        {o.label}
                    </option>
                ))}
            </select>
        </label>
    ),
};
function load(): { state: Workspace; error: string | null } {
    try {
        const saved = localStorage.getItem(storageKey);
        return {
            state: saved ? deserialize(saved) : initialWorkspace(),
            error: null,
        };
    } catch {
        return {
            state: initialWorkspace(),
            error: "Saved preview data could not be read. The original storage has been left untouched. Export or reset it before editing.",
        };
    }
}
const loaded = load();
function Preview() {
    const [state, setState] = useState(loaded.state);
    const stateRef = useRef(state);
    const [selectedId, setSelected] = useState<string | null>(null);
    const [drawing, setDrawing] = useState(false);
    const [editing, setEditing] = useState<string | null>(null);
    const [nodes, setNodes] = useState<Point[]>([]);
    const [error, setError] = useState(loaded.error);
    const [blocked, setBlocked] = useState(!!loaded.error);
    const [history, setHistory] = useState<Workspace[]>([]);
    const send = (command: Command) => {
        if (blocked)
            throw new Error(
                "Recover or reset the unreadable preview before editing.",
            );
        const next = reduce(stateRef.current, command);
        try {
            localStorage.setItem(storageKey, serialize(next));
        } catch {
            setError(
                "Browser storage is full or unavailable; changes are kept in memory only. Export the preview to retain them.",
            );
        }
        const previous = stateRef.current;
        setHistory((h) => [...h.slice(-29), previous]);
        stateRef.current = next;
        setState(next);
    };
    const run = (fn: () => void) => {
        try {
            fn();
        } catch (e) {
            setError(e instanceof Error ? e.message : "Operation failed");
        }
    };
    const cancel = () => {
        setDrawing(false);
        setNodes([]);
        setEditing(null);
    };
    const finish = () =>
        run(() => {
            const id = editing ?? crypto.randomUUID();
            send(
                editing
                    ? { type: "boundary", districtId: id, boundary: nodes }
                    : { type: "createDistrict", id, boundary: nodes },
            );
            setSelected(id);
            cancel();
        });
    const select = (id: string | null) => {
        cancel();
        setSelected(id);
    };
    const exportData = () => {
        const data = blocked
            ? (localStorage.getItem(storageKey) ?? "")
            : serialize(stateRef.current);
        const url = URL.createObjectURL(
            new Blob([data], { type: "application/json" }),
        );
        const a = document.createElement("a");
        a.href = url;
        a.download = "treedistrict-preview.v1.json";
        a.click();
        URL.revokeObjectURL(url);
    };
    const undo = () => {
        const previous = history[history.length - 1];
        if (!previous) return;
        try {
            localStorage.setItem(storageKey, serialize(previous));
        } catch {
            setError("Undo could not be persisted.");
        }
        stateRef.current = previous;
        setState(previous);
        setHistory((h) => h.slice(0, -1));
        if (!previous.districts.some((d) => d.id === selectedId))
            setSelected(null);
        cancel();
    };
    return (
        <Controls.Provider value={controls}>
            <div
                className="td-preview"
                onKeyDown={(e) => {
                    if (e.key === "Escape") cancel();
                }}
            >
                <header className="preview-toolbar">
                    <div>
                        <strong>TreeDistrict</strong>
                        <span> Local UX preview · no game connection</span>
                    </div>
                    <div>
                        <button onClick={undo} disabled={!history.length}>
                            Undo preview edit
                        </button>
                        <button
                            onClick={() => {
                                if (
                                    !window.confirm(
                                        "Start an empty preview city? Export first to keep the current districts. Saved vegetation sets will remain.",
                                    )
                                )
                                    return;
                                run(() => {
                                    const fresh = initialWorkspace();
                                    fresh.library = stateRef.current.library;
                                    localStorage.setItem(
                                        storageKey,
                                        serialize(fresh),
                                    );
                                    stateRef.current = fresh;
                                    setState(fresh);
                                    setHistory([]);
                                    select(null);
                                });
                            }}
                        >
                            New preview city
                        </button>
                        <button onClick={exportData}>Export preview</button>
                        <label className="import-label">
                            Import
                            <input
                                aria-label="Import preview"
                                type="file"
                                accept="application/json,.json"
                                onChange={(e) => {
                                    const file = e.target.files?.[0];
                                    if (file) {
                                        if (file.size > 5_000_000) {
                                            setError("Import exceeds 5 MB.");
                                            return;
                                        }
                                        void file.text().then((text) =>
                                            run(() => {
                                                const imported =
                                                    deserialize(text);
                                                localStorage.setItem(
                                                    storageKey,
                                                    serialize(imported),
                                                );
                                                stateRef.current = imported;
                                                setState(imported);
                                                setBlocked(false);
                                                setError(null);
                                                setHistory([]);
                                                select(null);
                                            }),
                                        );
                                    }
                                    e.target.value = "";
                                }}
                            />
                        </label>
                    </div>
                </header>
                <Editor
                    key={selectedId ?? "draft"}
                    state={state}
                    selectedId={selectedId}
                    send={send}
                    select={select}
                    drawing={drawing}
                    error={error}
                    beginDrawing={(edit) => {
                        setEditing(edit ? selectedId : null);
                        setNodes([]);
                        setDrawing(true);
                    }}
                />
                <main className="preview-map">
                    <div className="map-caption">
                        <span className="td-eyebrow">Boundary sandbox</span>
                        <h1>
                            {drawing
                                ? "Draw your vegetation district"
                                : "A set. A boundary. Your forest."}
                        </h1>
                        <p>
                            {drawing
                                ? "Click to place nodes. Finish closes the boundary; Escape cancels."
                                : "Choose a set on the left, then draw. Click a district to edit it."}
                        </p>
                        <small>
                            Flat test canvas · coordinates are illustrative · no
                            terrain or vegetation
                        </small>
                    </div>
                    <svg
                        data-testid="district-canvas"
                        className={drawing ? "drawing" : ""}
                        viewBox="0 0 1000 700"
                        role="img"
                        aria-label="District boundary drawing canvas"
                        onClick={(e) => {
                            if (!drawing) return;
                            const svg = e.currentTarget;
                            const matrix = svg.getScreenCTM();
                            if (!matrix) return;
                            const p = new DOMPoint(
                                e.clientX,
                                e.clientY,
                            ).matrixTransform(matrix.inverse());
                            setNodes((n) => [
                                ...n,
                                { x: Math.round(p.x), z: Math.round(p.y) },
                            ]);
                        }}
                    >
                        <defs>
                            <pattern
                                id="grid"
                                width="50"
                                height="50"
                                patternUnits="userSpaceOnUse"
                            >
                                <path
                                    d="M 50 0 L 0 0 0 50"
                                    fill="none"
                                    stroke="#ffffff0b"
                                    strokeWidth="1"
                                />
                            </pattern>
                        </defs>
                        <rect width="1000" height="700" fill="url(#grid)" />
                        <path
                            d="M -80 510 C 150 130 280 630 450 330 S 780 100 1080 380"
                            className="map-contour"
                        />
                        <path
                            d="M -80 540 C 150 160 280 660 450 360 S 780 130 1080 410"
                            className="map-contour"
                        />
                        {state.districts.map((d) => (
                            <g key={d.id}>
                                <polygon
                                    tabIndex={drawing ? -1 : 0}
                                    role="button"
                                    aria-label={`Select ${d.name}`}
                                    points={d.boundary
                                        .map((p) => `${p.x},${p.z}`)
                                        .join(" ")}
                                    className={`district ${selectedId === d.id ? "selected" : ""}`}
                                    onClick={(e) => {
                                        if (!drawing) {
                                            e.stopPropagation();
                                            select(d.id);
                                        }
                                    }}
                                    onKeyDown={(e) => {
                                        if (
                                            !drawing &&
                                            (e.key === "Enter" || e.key === " ")
                                        ) {
                                            e.preventDefault();
                                            select(d.id);
                                        }
                                    }}
                                />
                                <text
                                    x={d.boundary[0].x + 8}
                                    y={d.boundary[0].z + 22}
                                    pointerEvents="none"
                                >
                                    {d.name}
                                </text>
                            </g>
                        ))}
                        {drawing && (
                            <>
                                <polyline
                                    className="draft"
                                    points={nodes
                                        .map((p) => `${p.x},${p.z}`)
                                        .join(" ")}
                                />
                                {nodes.map((p, i) => (
                                    <circle
                                        key={i}
                                        cx={p.x}
                                        cy={p.z}
                                        r="5"
                                        fill="#d9fba4"
                                    />
                                ))}
                            </>
                        )}
                    </svg>
                    {drawing && (
                        <div className="drawing-controls">
                            <strong>{nodes.length} nodes</strong>
                            <button
                                onClick={() => setNodes((n) => n.slice(0, -1))}
                                disabled={!nodes.length}
                            >
                                Undo node
                            </button>
                            <button
                                disabled={!!polygonError(nodes)}
                                onClick={finish}
                            >
                                Finish district
                            </button>
                            <button onClick={cancel}>Cancel drawing</button>
                            <small>
                                {polygonError(nodes) ??
                                    "Valid boundary — ready to finish"}
                            </small>
                        </div>
                    )}
                    <div className="district-list">
                        <h3>Districts · {state.districts.length}</h3>
                        {state.districts.length === 0 ? (
                            <p>No districts yet.</p>
                        ) : (
                            state.districts.map((d) => (
                                <button
                                    key={d.id}
                                    onClick={() => select(d.id)}
                                    aria-pressed={selectedId === d.id}
                                >
                                    {d.name}
                                </button>
                            ))
                        )}
                    </div>
                    {blocked && (
                        <div role="alert" className="recovery">
                            {error}
                            <button onClick={exportData}>
                                Export original data
                            </button>
                            <button
                                onClick={() => {
                                    localStorage.removeItem(storageKey);
                                    setBlocked(false);
                                    setError(null);
                                }}
                            >
                                Discard unreadable preview
                            </button>
                        </div>
                    )}
                </main>
            </div>
        </Controls.Provider>
    );
}
createRoot(document.getElementById("root")!).render(<Preview />);
