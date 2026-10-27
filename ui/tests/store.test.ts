import { describe, expect, it } from "vitest";
import {
    catalog,
    clone,
    deserialize,
    initialWorkspace,
    modified,
    polygonError,
    reduce,
    resolveClimate,
    serialize,
} from "../src/domain/store";
import { catalogSchema, setSchema } from "../src/domain/schema";
const boundary = [
    { x: 0, z: 0 },
    { x: 100, z: 0 },
    { x: 100, z: 100 },
    { x: 0, z: 100 },
];
const withDistrict = () =>
    reduce(initialWorkspace(), {
        type: "createDistrict",
        id: "district-a",
        boundary,
    });
describe("editor domain", () => {
    it("adds new built-ins on reload without overwriting saved district profiles", () => {
        const state = withDistrict();
        state.library = [state.library[0]];
        state.districts[0].working.assets[0].profile.matureSpacingM = 8;
        const loaded = deserialize(serialize(state));
        expect(loaded.library).toHaveLength(7);
        expect(loaded.districts[0]).toEqual(state.districts[0]);
        const nz = loaded.library.find(
            (entry) => entry.id === "builtin:nz-regenerating-bush",
        )!;
        expect(nz.set.assets[0].profile.temperature.optimum).toBe(12);
        expect(nz.set.assets[0].profile.limits.maxSlopeDegrees).toBe(60);
    });
    it("loads London Plane with one species profile and age proportions", () => {
        expect(catalogSchema.safeParse(catalog).success).toBe(true);
        expect(catalog.assets[0].placeholder).toBe(false);
        expect(catalog.assets[0].profile.matureSpacingM).toBe(9.2802);
    });
    it("snapshots settings per district without aliasing", () => {
        const state = withDistrict();
        const next = reduce(state, {
            type: "editSet",
            districtId: "district-a",
            set: { ...state.districts[0].working, overallDensityPercent: 12 },
        });
        expect(next.districts[0].working.overallDensityPercent).toBe(12);
        expect(state.districts[0].working.overallDensityPercent).toBe(75);
        expect(next.draft.working.overallDensityPercent).toBe(75);
        expect(next.library[0].set.overallDensityPercent).toBe(75);
    });
    it("star reflects content, clears on exact revert, ignores city edits", () => {
        const state = withDistrict();
        let next = reduce(state, {
            type: "editSet",
            districtId: "district-a",
            set: { ...state.districts[0].working, overallDensityPercent: 12 },
        });
        expect(modified(next.districts[0])).toBe(true);
        next = reduce(next, {
            type: "editSet",
            districtId: "district-a",
            set: clone(state.districts[0].working),
        });
        next = reduce(next, {
            type: "city",
            city: { ...next.city, referenceTemperatureC: 20 },
        });
        expect(modified(next.districts[0])).toBe(false);
    });
    it("live inheritance changes only fields without overrides", () => {
        const state = initialWorkspace();
        const set = clone(state.draft.working);
        set.climate.referenceElevationM = 800;
        const next = reduce(state, {
            type: "city",
            city: {
                ...state.city,
                referenceTemperatureC: 22,
                referenceElevationM: 99,
            },
        });
        expect(resolveClimate(next, set).climate).toEqual({
            referenceTemperatureC: 22,
            referenceElevationM: 800,
            coolingCPerKm: 6.5,
        });
    });
    it("game source uses stable host climate, with explicit unavailable fallback", () => {
        const state = initialWorkspace();
        state.city.source = "game";
        expect(resolveClimate(state, state.draft.working).warning).toContain(
            "unavailable",
        );
        state.gameTemperatureC = 8;
        expect(
            resolveClimate(state, state.draft.working).climate
                .referenceTemperatureC,
        ).toBe(8);
    });
    it("protects built-ins and saves nested edits as a custom set", () => {
        const state = withDistrict();
        expect(() =>
            reduce(state, {
                type: "saveSet",
                districtId: "district-a",
                name: "Overwrite",
            }),
        ).toThrow("Save as");
        const set = clone(state.districts[0].working);
        set.assets[0].profile.heightM = 7;
        set.climate.coolingCPerKm = 5;
        let next = reduce(state, {
            type: "editSet",
            districtId: "district-a",
            set,
        });
        next = reduce(next, {
            type: "saveSet",
            districtId: "district-a",
            name: "My mix",
            newId: "user:mix",
        });
        expect(
            next.library.find((entry) => entry.id === "user:mix")!.set.assets[0]
                .profile.heightM,
        ).toBe(7);
        expect(
            next.library.find((entry) => entry.id === "user:mix")!.set.climate,
        ).toEqual({ coolingCPerKm: 5 });
        expect(modified(next.districts[0])).toBe(false);
    });
    it("updating a library set preserves other district snapshots and baselines", () => {
        let state = reduce(withDistrict(), {
            type: "saveSet",
            districtId: "district-a",
            name: "Mix",
            newId: "user:mix",
        });
        state = reduce(state, {
            type: "chooseSet",
            districtId: null,
            libraryId: "user:mix",
        });
        state = reduce(state, {
            type: "createDistrict",
            id: "district-b",
            boundary,
        });
        state = reduce(state, {
            type: "editSet",
            districtId: "district-a",
            set: { ...state.districts[0].working, overallDensityPercent: 10 },
        });
        state = reduce(state, {
            type: "saveSet",
            districtId: "district-a",
            name: "Mix",
        });
        expect(state.districts[1].working.overallDensityPercent).toBe(75);
        expect(modified(state.districts[1])).toBe(false);
    });
    it("allows overlap and concave districts without a cross-district policy", () => {
        const state = reduce(withDistrict(), {
            type: "createDistrict",
            id: "district-b",
            boundary,
        });
        expect(state.districts).toHaveLength(2);
        expect(
            polygonError([
                { x: 0, z: 0 },
                { x: 100, z: 0 },
                { x: 100, z: 100 },
                { x: 60, z: 100 },
                { x: 60, z: 40 },
                { x: 40, z: 40 },
                { x: 40, z: 100 },
                { x: 0, z: 100 },
            ]),
        ).toBeNull();
    });
    it("rejects degenerate, self-intersecting and touching boundaries", () => {
        expect(
            polygonError([
                { x: 0, z: 0 },
                { x: 1, z: 1 },
            ]),
        ).toBeTruthy();
        expect(
            polygonError([
                { x: 0, z: 0 },
                { x: 1, z: 1 },
                { x: 2, z: 2 },
            ]),
        ).toBeTruthy();
        expect(
            polygonError([
                { x: 0, z: 0 },
                { x: 100, z: 100 },
                { x: 0, z: 100 },
                { x: 100, z: 0 },
            ]),
        ).toContain("cross");
    });
    it("rejects invalid curves, duplicate assets, inverted limits and nonfinite values", () => {
        const set = clone(catalog.sets[0].set);
        set.assets[0].profile.temperature.optimum = 100;
        expect(setSchema.safeParse(set).success).toBe(false);
        expect(
            setSchema.safeParse({
                ...catalog.sets[0].set,
                overallDensityPercent: NaN,
            }).success,
        ).toBe(false);
        expect(
            setSchema.safeParse({
                ...catalog.sets[0].set,
                assets: [catalog.assets[0], catalog.assets[0]],
            }).success,
        ).toBe(false);
        expect(
            setSchema.safeParse({
                ...catalog.sets[0].set,
                limits: {
                    minElevationM: 100,
                    maxElevationM: 0,
                    maxSlopeDegrees: 50,
                },
            }).success,
        ).toBe(false);
    });
    it("retains boundary, settings, selection baseline and mode across round-trip", () => {
        let state = withDistrict();
        state = reduce(state, {
            type: "district",
            districtId: "district-a",
            generationMode: "auto",
        });
        state = reduce(state, {
            type: "editSet",
            districtId: "district-a",
            set: { ...state.districts[0].working, overallDensityPercent: 35 },
        });
        expect(deserialize(serialize(state))).toEqual(state);
        expect(modified(deserialize(serialize(state)).districts[0])).toBe(true);
    });
    it("refuses unknown schema versions, oversized imports and invalid persisted boundaries", () => {
        expect(() => deserialize('{"schemaVersion":2}')).toThrow();
        expect(() => deserialize(" ".repeat(5_000_001))).toThrow("too large");
        const state = withDistrict();
        state.districts[0].boundary = [
            { x: 0, z: 0 },
            { x: 1, z: 1 },
            { x: 2, z: 2 },
        ];
        expect(() => deserialize(JSON.stringify(state))).toThrow("area");
    });
    it("deleting one district preserves another even when they overlap", () => {
        const state = reduce(withDistrict(), {
            type: "createDistrict",
            id: "district-b",
            boundary,
        });
        const next = reduce(state, {
            type: "deleteDistrict",
            districtId: "district-a",
        });
        expect(next.districts.map((d) => d.id)).toEqual(["district-b"]);
        expect(next.library).toEqual(state.library);
    });
});
