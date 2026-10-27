// Legacy preview/import data had separate environmental profiles for every age.
// Convert only at the load boundary; regular edits still pass strict current validation.
export function migrateLegacyWorkspace(value: unknown): unknown {
    const object = (x: unknown): x is Record<string, any> =>
        typeof x === "object" && x !== null && !Array.isArray(x);
    const set = (value: unknown) => {
        if (!object(value) || !Array.isArray(value.assets)) return;
        for (const asset of value.assets) {
            if (
                !object(asset) ||
                asset.profile ||
                !Array.isArray(asset.ages) ||
                !asset.ages.length
            )
                continue;
            const mature =
                asset.ages.find((a: any) => a.id === "mature") ?? asset.ages[0];
            const {
                id,
                name,
                enabled,
                densityPercent,
                footprintRadiusM,
                ...profile
            } = mature;
            asset.profile = {
                ...profile,
                matureSpacingM: Math.max(1, mature.canopyRadiusM * 2),
            };
            const total = asset.ages.reduce(
                (sum: number, a: any) =>
                    sum + (a.enabled ? a.densityPercent : 0),
                0,
            );
            asset.ages = asset.ages.map((a: any) => ({
                id: a.id,
                name: a.name,
                proportionPercent:
                    total > 0 && a.enabled
                        ? (a.densityPercent * 100) / total
                        : 0,
            }));
        }
    };
    const working = (x: unknown) => {
        if (object(x)) {
            set(x.working);
            set(x.baseline);
        }
    };
    if (object(value)) {
        working(value.draft);
        if (Array.isArray(value.districts)) value.districts.forEach(working);
        if (Array.isArray(value.library))
            value.library.forEach((entry: any) => set(entry?.set));
    }
    return value;
}
