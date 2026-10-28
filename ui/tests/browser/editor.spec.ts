import { test, expect } from "@playwright/test";
test("draw, edit, save, reselect, reload and remove district", async ({
    page,
}) => {
    const errors: string[] = [];
    page.on("pageerror", (e) => errors.push(e.message));
    await page.goto("/");
    await page
        .getByRole("button", { name: "Draw district", exact: true })
        .click();
    const canvas = page.getByTestId("district-canvas");
    await canvas.click({ position: { x: 220, y: 240 } });
    await canvas.click({ position: { x: 550, y: 240 } });
    await canvas.click({ position: { x: 550, y: 480 } });
    await canvas.click({ position: { x: 220, y: 480 } });
    await page.getByRole("button", { name: "Finish district" }).click();
    await page
        .getByRole("button", {
            name: "Placement: Bulk upload · Switch",
            exact: true,
        })
        .click();
    await expect(
        page.getByRole("button", {
            name: "Placement: Per entity upload · Switch",
            exact: true,
        }),
    ).toBeVisible();
    await page
        .getByRole("button", {
            name: "Placement: Per entity upload · Switch",
            exact: true,
        })
        .click();
    await expect(
        page.getByRole("button", {
            name: "Placement: Bulk upload · Switch",
            exact: true,
        }),
    ).toBeVisible();
    await expect(page.getByLabel("District name")).toHaveValue(
        "Vegetation district 1",
    );
    await page.getByLabel("Density multiplier", { exact: true }).fill("0.42");
    await expect(
        page.getByRole("heading", {
            name: "Farmland set *",
            exact: true,
        }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Save as…", exact: true }).click();
    await page.getByLabel("Set name", { exact: true }).fill("My test mix");
    await page.getByRole("button", { name: "Save new copy" }).click();
    await expect(
        page.getByRole("heading", { name: "My test mix", exact: true }),
    ).toBeVisible();
    await page
        .getByRole("button", { name: "Set settings · 3 vegetation assets" })
        .click();
    await page.getByRole("button", { name: "Remove London Plane" }).click();
    await page
        .getByRole("button", { name: "Add vegetation…", exact: true })
        .click();
    await expect(
        page.getByRole("button", {
            name: "Add London Plane",
        }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Add London Plane" }).click();
    await page
        .getByRole("button", { name: "City defaults", exact: true })
        .click();
    await page
        .getByLabel("Reference temperature (°C)", { exact: true })
        .fill("18");
    await expect(
        page.getByRole("button", { name: "Generate", exact: true }),
    ).toBeDisabled();
    await page.reload();
    await page
        .getByRole("button", { name: "Vegetation district 1", exact: true })
        .click();
    await expect(
        page.getByLabel("Density multiplier", { exact: true }),
    ).toHaveValue("0.42");
    await page
        .getByRole("button", { name: "Delete district", exact: true })
        .click();
    await page.getByRole("button", { name: "Delete permanently" }).click();
    await expect(
        page.getByText("No districts yet.", { exact: true }),
    ).toBeVisible();
    expect(errors).toEqual([]);
});
test("invalid input stays local and cancellation keeps district untouched", async ({
    page,
}) => {
    await page.goto("/");
    await page.getByLabel("Density multiplier", { exact: true }).fill("2.1");
    await expect(page.getByRole("alert")).toContainText("≤ 2");
    await page
        .getByRole("button", { name: "Draw district", exact: true })
        .click();
    await page
        .getByTestId("district-canvas")
        .click({ position: { x: 200, y: 250 } });
    await page.getByRole("button", { name: "Cancel drawing" }).click();
    await page.reload();
    await expect(
        page.getByLabel("Density multiplier", { exact: true }),
    ).toHaveValue("0.75");
    await expect(
        page.getByText("No districts yet.", { exact: true }),
    ).toBeVisible();
});

test("saving before replacement preserves edits and custom library survives a new city", async ({
    page,
}) => {
    await page.goto("/");
    await page.getByLabel("Density multiplier", { exact: true }).fill("0.3");
    await page
        .getByLabel("Vegetation set", { exact: true })
        .selectOption("builtin:london-plane");
    await page.getByRole("button", { name: "Save as first…" }).click();
    await page.getByLabel("Set name", { exact: true }).fill("Retained mix");
    await page.getByRole("button", { name: "Save new copy" }).click();
    await expect(
        page.getByRole("heading", { name: "Farmland set", exact: true }),
    ).toBeVisible();
    await page
        .getByLabel("Vegetation set", { exact: true })
        .selectOption({ label: "My sets · Retained mix" });
    await expect(
        page.getByLabel("Density multiplier", { exact: true }),
    ).toHaveValue("0.3");
    page.once("dialog", (d) => d.accept());
    await page
        .getByRole("button", { name: "New preview city", exact: true })
        .click();
    await page
        .getByLabel("Vegetation set", { exact: true })
        .selectOption({ label: "My sets · Retained mix" });
    await expect(
        page.getByLabel("Density multiplier", { exact: true }),
    ).toHaveValue("0.3");
});

test("redraw cancellation and undo restore the existing district", async ({
    page,
}) => {
    await page.goto("/");
    await page
        .getByRole("button", { name: "Draw district", exact: true })
        .click();
    const canvas = page.getByTestId("district-canvas");
    for (const position of [
        { x: 220, y: 240 },
        { x: 550, y: 240 },
        { x: 550, y: 480 },
    ])
        await canvas.click({ position });
    await page.getByRole("button", { name: "Finish district" }).click();
    const polygon = page.getByRole("button", {
        name: "Select Vegetation district 1",
        exact: true,
    });
    const original = await polygon.getAttribute("points");
    await page.getByRole("button", { name: "Redraw boundary" }).click();
    await canvas.click({ position: { x: 300, y: 400 } });
    await page.getByRole("button", { name: "Cancel drawing" }).click();
    await expect(polygon).toHaveAttribute("points", original!);
    await page.getByLabel("Density multiplier", { exact: true }).fill("0.2");
    await page.getByRole("button", { name: "Undo preview edit" }).click();
    await expect(
        page.getByLabel("Density multiplier", { exact: true }),
    ).toHaveValue("0.75");
});

test("picker offers researched vegetation while preventing duplicates", async ({
    page,
}) => {
    await page.goto("/");
    await page
        .getByRole("button", { name: "Set settings · 3 vegetation assets" })
        .click();
    await page
        .getByRole("button", { name: "Add vegetation…", exact: true })
        .click();
    const picker = page.getByRole("dialog", {
        name: "Add vegetation",
        exact: true,
    });
    await expect(
        picker.getByRole("button", {
            name: "Add Wild Green Bush 01",
            exact: true,
        }),
    ).toBeEnabled();
    await expect(
        picker.getByRole("button", {
            name: "Add London Plane",
            exact: true,
        }),
    ).toBeDisabled();
    await picker.getByLabel("Search vegetation").fill("nonexistent");
    await expect(
        picker.getByRole("status").filter({ hasText: "No vegetation matches" }),
    ).toBeVisible();
    await picker.getByRole("button", { name: "Cancel", exact: true }).click();
    await expect(
        page.getByRole("heading", {
            name: "Farmland set",
            exact: true,
        }),
    ).toBeVisible();
});

test("NZ preset exposes its three editable proxy profiles", async ({
    page,
}) => {
    await page.goto("/");
    await page
        .getByLabel("Vegetation set", { exact: true })
        .selectOption("builtin:nz-regenerating-bush");
    await page
        .getByRole("button", { name: "Set settings · 3 vegetation assets" })
        .click();
    await expect(
        page.getByLabel("Wild Green Bush 01 density (%)", { exact: true }),
    ).toHaveValue("55");
    await page
        .getByRole("button", {
            name: "Species profile · shared across ages",
            exact: true,
        })
        .first()
        .click();
    await page.screenshot({
        path: "../artifacts/editor-nz-checkpoint.png",
        fullPage: true,
    });
    await expect(
        page.getByLabel("Density multiplier", { exact: true }),
    ).toHaveValue("1");
    await page.getByLabel("Density multiplier", { exact: true }).fill("2");
    await expect(
        page.getByLabel("Spacing adjustment (m)", { exact: true }).first(),
    ).toHaveValue("0");
    await page
        .getByLabel("Spacing adjustment (m)", { exact: true })
        .first()
        .fill("-1");
    await expect(
        page
            .getByLabel("Maximum slope for culling (°)", { exact: true })
            .first(),
    ).toHaveValue("60");
    await page
        .getByLabel("Maximum slope for culling (°)", { exact: true })
        .first()
        .fill("25");
    await page.reload();
    await expect(
        page.getByLabel("Density multiplier", { exact: true }),
    ).toHaveValue("2");
    await page
        .getByRole("button", { name: "Set settings · 3 vegetation assets" })
        .click();
    await page
        .getByRole("button", {
            name: "Species profile · shared across ages",
            exact: true,
        })
        .first()
        .click();
    await expect(
        page.getByLabel("Spacing adjustment (m)", { exact: true }).first(),
    ).toHaveValue("-1");
    await expect(
        page
            .getByLabel("Maximum slope for culling (°)", { exact: true })
            .first(),
    ).toHaveValue("25");
});
