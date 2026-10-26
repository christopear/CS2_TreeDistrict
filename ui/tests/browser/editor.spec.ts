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
    await expect(page.getByLabel("District name")).toHaveValue(
        "Vegetation district 1",
    );
    await page.getByLabel("Overall density (%)", { exact: true }).fill("42");
    await expect(
        page.getByRole("heading", {
            name: "Example mixed vegetation *",
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
        .getByRole("button", { name: "Set settings · 2 vegetation assets" })
        .click();
    await page
        .getByRole("button", { name: "Remove Example understorey — fictional" })
        .click();
    await page
        .getByRole("button", { name: "Add vegetation…", exact: true })
        .click();
    await expect(
        page.getByRole("button", {
            name: "Add Example understorey — fictional",
        }),
    ).toBeVisible();
    await page
        .getByRole("button", { name: "Add Example understorey — fictional" })
        .click();
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
        page.getByLabel("Overall density (%)", { exact: true }),
    ).toHaveValue("42");
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
    await page.getByLabel("Overall density (%)", { exact: true }).fill("150");
    await expect(page.getByRole("alert")).toContainText("≤ 100");
    await page
        .getByRole("button", { name: "Draw district", exact: true })
        .click();
    await page
        .getByTestId("district-canvas")
        .click({ position: { x: 200, y: 250 } });
    await page.getByRole("button", { name: "Cancel drawing" }).click();
    await page.reload();
    await expect(
        page.getByLabel("Overall density (%)", { exact: true }),
    ).toHaveValue("75");
    await expect(
        page.getByText("No districts yet.", { exact: true }),
    ).toBeVisible();
});

test("saving before replacement preserves edits and custom library survives a new city", async ({
    page,
}) => {
    await page.goto("/");
    await page.getByLabel("Overall density (%)", { exact: true }).fill("30");
    await page
        .getByLabel("Vegetation set", { exact: true })
        .selectOption("builtin:example-open");
    await page.getByRole("button", { name: "Save as first…" }).click();
    await page.getByLabel("Set name", { exact: true }).fill("Retained mix");
    await page.getByRole("button", { name: "Save new copy" }).click();
    await expect(
        page.getByRole("heading", { name: "Example open canopy", exact: true }),
    ).toBeVisible();
    await page
        .getByLabel("Vegetation set", { exact: true })
        .selectOption({ label: "My sets · Retained mix" });
    await expect(
        page.getByLabel("Overall density (%)", { exact: true }),
    ).toHaveValue("30");
    page.once("dialog", (d) => d.accept());
    await page
        .getByRole("button", { name: "New preview city", exact: true })
        .click();
    await page
        .getByLabel("Vegetation set", { exact: true })
        .selectOption({ label: "My sets · Retained mix" });
    await expect(
        page.getByLabel("Overall density (%)", { exact: true }),
    ).toHaveValue("30");
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
    await page.getByLabel("Overall density (%)", { exact: true }).fill("20");
    await page.getByRole("button", { name: "Undo preview edit" }).click();
    await expect(
        page.getByLabel("Overall density (%)", { exact: true }),
    ).toHaveValue("75");
});

test("add vegetation remains discoverable when all placeholder assets are included", async ({
    page,
}) => {
    await page.goto("/");
    await page
        .getByRole("button", { name: "Set settings · 2 vegetation assets" })
        .click();
    await page
        .getByRole("button", { name: "Add vegetation…", exact: true })
        .click();
    const picker = page.getByRole("dialog", {
        name: "Add vegetation",
        exact: true,
    });
    await expect(
        picker.getByText(
            "All available placeholder vegetation is already in this set.",
        ),
    ).toBeVisible();
    await expect(
        picker.getByRole("button", {
            name: "Add Example canopy — fictional",
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
            name: "Example mixed vegetation",
            exact: true,
        }),
    ).toBeVisible();
});
