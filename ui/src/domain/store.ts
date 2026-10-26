import manifest from "../../../data/catalog.v2.json";
import londonPlane from "../../../data/london-plane.json";
import { migrateLegacyWorkspace } from "./migrate";
import {
    catalogSchema,
    workspaceSchema,
    setSchema,
    type Workspace,
    type VegetationSet,
    type Point,
    type Climate,
} from "./schema";

export const catalog = catalogSchema.parse({
    ...manifest,
    assets: [londonPlane.asset],
    sets: manifest.sets.map((entry) => ({
        ...entry,
        set: {
            ...entry.set,
            assets: entry.set.assetIds.map((id) => {
                if (id !== londonPlane.asset.id)
                    throw new Error("Unknown catalog asset");
                return londonPlane.asset;
            }),
        },
    })),
});
export function clone<T>(value: T): T {
    return JSON.parse(JSON.stringify(value)) as T;
}
// Canonical comparison ignores object property order (including climate override edit order).
function canonical(value: unknown): string {
    if (Array.isArray(value)) return `[${value.map(canonical).join(",")}]`;
    if (value !== null && typeof value === "object")
        return `{${Object.entries(value)
            .sort(([a], [b]) => a.localeCompare(b))
            .map(([k, v]) => `${JSON.stringify(k)}:${canonical(v)}`)
            .join(",")}}`;
    return JSON.stringify(value);
}
export function modified(target: Workspace["draft"]): boolean {
    return canonical(target.working) !== canonical(target.baseline);
}
export function initialWorkspace(): Workspace {
    const entry = catalog.sets[0];
    return workspaceSchema.parse({
        schemaVersion: 1,
        city: catalog.cityDefaults,
        gameTemperatureC: null,
        library: clone(catalog.sets),
        districts: [],
        draft: {
            libraryId: entry.id,
            baseline: clone(entry.set),
            working: clone(entry.set),
        },
    });
}
export function targetOf(state: Workspace, districtId: string | null) {
    if (districtId === null) return state.draft;
    const district = state.districts.find((d) => d.id === districtId);
    if (!district) throw new Error("District no longer exists");
    return district;
}
export function resolveClimate(
    state: Workspace,
    set: VegetationSet,
): { climate: Omit<Climate, "source">; warning: string | null } {
    const cityTemperature =
        state.city.source === "game"
            ? state.gameTemperatureC
            : state.city.referenceTemperatureC;
    return {
        climate: {
            referenceTemperatureC:
                set.climate.referenceTemperatureC ??
                cityTemperature ??
                state.city.referenceTemperatureC,
            referenceElevationM:
                set.climate.referenceElevationM ??
                state.city.referenceElevationM,
            coolingCPerKm:
                set.climate.coolingCPerKm ?? state.city.coolingCPerKm,
        },
        warning:
            set.climate.referenceTemperatureC === undefined &&
            cityTemperature === null
                ? "Game climate unavailable. Using the city’s manual fallback temperature."
                : null,
    };
}
export function polygonError(points: Point[]): string | null {
    if (points.length < 3) return "Draw at least three nodes.";
    const cross = (a: Point, b: Point, c: Point) =>
        (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
    const on = (a: Point, b: Point, c: Point) =>
        Math.abs(cross(a, b, c)) < 1e-8 &&
        c.x >= Math.min(a.x, b.x) &&
        c.x <= Math.max(a.x, b.x) &&
        c.z >= Math.min(a.z, b.z) &&
        c.z <= Math.max(a.z, b.z);
    for (let i = 0; i < points.length; i++) {
        const a = points[i],
            b = points[(i + 1) % points.length];
        if (Math.hypot(a.x - b.x, a.z - b.z) < 0.01)
            return "Nodes must be distinct.";
        for (let j = i + 1; j < points.length; j++) {
            if (j === i + 1 || (i === 0 && j === points.length - 1)) continue;
            const c = points[j],
                d = points[(j + 1) % points.length];
            if (
                (cross(a, b, c) * cross(a, b, d) < 0 &&
                    cross(c, d, a) * cross(c, d, b) < 0) ||
                on(a, b, c) ||
                on(a, b, d) ||
                on(c, d, a) ||
                on(c, d, b)
            )
                return "Boundary must not cross or touch itself.";
        }
    }
    const area = points.reduce((sum, p, i) => {
        const q = points[(i + 1) % points.length];
        return sum + p.x * q.z - q.x * p.z;
    }, 0);
    return Math.abs(area) < 0.01 ? "Boundary must enclose an area." : null;
}
export type Command =
    | { type: "editSet"; districtId: string | null; set: VegetationSet }
    | { type: "chooseSet"; districtId: string | null; libraryId: string }
    | { type: "newSet"; districtId: string | null }
    | {
          type: "saveSet";
          districtId: string | null;
          name: string;
          newId?: string;
          afterSaveLibraryId?: string;
      }
    | { type: "city"; city: Climate }
    | { type: "createDistrict"; id: string; boundary: Point[] }
    | { type: "boundary"; districtId: string; boundary: Point[] }
    | {
          type: "district";
          districtId: string;
          name?: string;
          generationMode?: "onDemand" | "auto";
      }
    | { type: "deleteDistrict"; districtId: string };

// Reference command semantics for the local preview; the game host must validate commands too.
export function reduce(state: Workspace, command: Command): Workspace {
    const next = clone(state);
    if (command.type === "city") next.city = command.city;
    else if (command.type === "createDistrict") {
        const error = polygonError(command.boundary);
        if (error) throw new Error(error);
        next.districts.push({
            ...clone(next.draft),
            id: command.id,
            name: `Vegetation district ${next.districts.length + 1}`,
            boundary: command.boundary,
            seed: 1,
            generationMode: "onDemand",
            revision: 0,
        });
    } else if (command.type === "deleteDistrict") {
        targetOf(next, command.districtId);
        next.districts = next.districts.filter(
            (d) => d.id !== command.districtId,
        );
    } else if (command.type === "district" || command.type === "boundary") {
        const district = next.districts.find(
            (d) => d.id === command.districtId,
        );
        if (!district) throw new Error("District no longer exists");
        if (command.type === "boundary") {
            const error = polygonError(command.boundary);
            if (error) throw new Error(error);
            district.boundary = command.boundary;
        } else {
            if (command.name !== undefined) district.name = command.name;
            if (command.generationMode)
                district.generationMode = command.generationMode;
        }
        district.revision++;
    } else {
        const target = targetOf(next, command.districtId);
        if (command.type === "editSet")
            target.working = setSchema.parse(command.set);
        if (command.type === "chooseSet") {
            const entry = next.library.find((e) => e.id === command.libraryId);
            if (!entry) throw new Error("Set no longer exists");
            Object.assign(target, {
                libraryId: entry.id,
                baseline: clone(entry.set),
                working: clone(entry.set),
            });
        }
        if (command.type === "newSet") {
            target.libraryId = null;
            target.baseline = null;
            target.working.name = "New vegetation set";
            target.working.assets = [];
        }
        if (command.type === "saveSet") {
            const saved = setSchema.parse({
                ...target.working,
                name: command.name,
            });
            if (command.newId) {
                if (next.library.some((e) => e.id === command.newId))
                    throw new Error("Set identifier already exists");
                next.library.push({
                    id: command.newId,
                    builtIn: false,
                    set: clone(saved),
                });
                target.libraryId = command.newId;
            } else {
                const entry = next.library.find(
                    (e) => e.id === target.libraryId,
                );
                if (!entry || entry.builtIn)
                    throw new Error("Use Save as for a built-in or new set");
                entry.set = clone(saved);
            }
            target.working = clone(saved);
            target.baseline = clone(saved);
            if (command.afterSaveLibraryId === "__new") {
                target.libraryId = null;
                target.baseline = null;
                target.working.name = "New vegetation set";
                target.working.assets = [];
            } else if (command.afterSaveLibraryId) {
                const replacement = next.library.find(
                    (e) => e.id === command.afterSaveLibraryId,
                );
                if (!replacement)
                    throw new Error("Replacement set no longer exists");
                Object.assign(target, {
                    libraryId: replacement.id,
                    baseline: clone(replacement.set),
                    working: clone(replacement.set),
                });
            }
        }
        if (command.districtId !== null)
            next.districts.find((d) => d.id === command.districtId)!.revision++;
    }
    return workspaceSchema.parse(next);
}
export function serialize(state: Workspace): string {
    return JSON.stringify(workspaceSchema.parse(state));
}
export function deserialize(json: string): Workspace {
    if (json.length > 5_000_000)
        throw new Error("Settings document is too large");
    const state = workspaceSchema.parse(migrateLegacyWorkspace(JSON.parse(json)));
    for (const d of state.districts) {
        const error = polygonError(d.boundary);
        if (error) throw new Error(error);
    }
    return state;
}
