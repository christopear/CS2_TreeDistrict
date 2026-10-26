import { z } from "zod";

const number = z.number().finite();
const id = z.string().min(1).max(160);
const name = z.string().trim().min(1).max(80);
const percent = number.min(0).max(100);
export const climateSchema = z.object({
    source: z.enum(["game", "manual"]),
    referenceTemperatureC: number.min(-100).max(100),
    referenceElevationM: number.min(-2000).max(20000),
    coolingCPerKm: number.min(0).max(20),
});
export const overridesSchema = climateSchema.omit({ source: true }).partial();
export const responseSchema = z
    .object({ min: number, optimum: number, max: number })
    .refine(
        (r) => r.min < r.optimum && r.optimum < r.max,
        "Response must have min < optimum < max",
    );
export const limitsSchema = z
    .object({
        minElevationM: number.min(-2000).max(20000),
        maxElevationM: number.min(-2000).max(20000),
        maxSlopeDegrees: number.min(0).max(90),
    })
    .refine(
        (l) => l.minElevationM <= l.maxElevationM,
        "Minimum elevation must not exceed maximum",
    );
export const ageSchema = z.object({
    id,
    name,
    enabled: z.boolean(),
    densityPercent: percent,
    footprintRadiusM: number.positive().max(100),
    heightM: number.positive().max(200),
    canopyRadiusM: number.min(0).max(100),
    canopyOpacityPercent: percent,
    shadeTolerancePercent: percent,
    temperature: responseSchema,
    slope: responseSchema.refine(
        (r) => r.min >= 0 && r.max <= 90,
        "Slope response must be within 0–90°",
    ),
    limits: limitsSchema,
});
export const assetSchema = z
    .object({
        id,
        name,
        placeholder: z.boolean(),
        densityPercent: percent,
        ages: z.array(ageSchema).min(1).max(16),
    })
    .refine(
        (a) => new Set(a.ages.map((x) => x.id)).size === a.ages.length,
        "Duplicate age identifier",
    );
export const setSchema = z
    .object({
        name,
        overallDensityPercent: percent,
        clumpingPercent: percent,
        climate: overridesSchema,
        limits: limitsSchema,
        assets: z.array(assetSchema).max(128),
    })
    .refine(
        (s) => new Set(s.assets.map((x) => x.id)).size === s.assets.length,
        "Duplicate vegetation asset",
    );
export const pointSchema = z.object({ x: number, z: number });
export const districtSchema = z.object({
    id,
    name,
    boundary: z.array(pointSchema).min(3).max(2048),
    seed: number.int().min(0).max(2147483647),
    generationMode: z.enum(["onDemand", "auto"]),
    revision: number.int().nonnegative(),
    libraryId: id.nullable(),
    baseline: setSchema.nullable(),
    working: setSchema,
});
export const libraryEntrySchema = z.object({
    id,
    builtIn: z.boolean(),
    set: setSchema,
});
export const workspaceSchema = z
    .object({
        schemaVersion: z.literal(1),
        city: climateSchema,
        // Adapter supplies a stable climate baseline, never the current weather. Null means unavailable.
        gameTemperatureC: number.min(-100).max(100).nullable(),
        library: z.array(libraryEntrySchema).min(1).max(256),
        districts: z.array(districtSchema).max(2048),
        draft: z.object({
            libraryId: id.nullable(),
            baseline: setSchema.nullable(),
            working: setSchema,
        }),
    })
    .refine(
        (w) =>
            new Set(w.library.map((x) => x.id)).size === w.library.length &&
            new Set(w.districts.map((x) => x.id)).size === w.districts.length,
        "Duplicate library/district identifier",
    );
export const catalogSchema = z.object({
    schemaVersion: z.literal(1),
    disclaimer: z.string(),
    cityDefaults: climateSchema,
    assets: z.array(assetSchema).min(1),
    sets: z.array(libraryEntrySchema).min(1),
});
export type Climate = z.infer<typeof climateSchema>;
export type VegetationSet = z.infer<typeof setSchema>;
export type AgeProfile = z.infer<typeof ageSchema>;
export type VegetationAsset = z.infer<typeof assetSchema>;
export type District = z.infer<typeof districtSchema>;
export type Workspace = z.infer<typeof workspaceSchema>;
export type Point = z.infer<typeof pointSchema>;
export type Catalog = z.infer<typeof catalogSchema>;
