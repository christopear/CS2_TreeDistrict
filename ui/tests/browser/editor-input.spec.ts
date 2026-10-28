import { test, expect } from "@playwright/test";
import { readFileSync } from "node:fs";

test("editor extension accepts input while empty overlay passes through", async ({
    page,
}) => {
    await page.setContent(`
        <button id="world" style="position:absolute;left:900px;top:400px">World</button>
        <div style="pointer-events:none;position:absolute;inset:0">
            <div style="position:absolute;top:0;height:48px;width:100%;background:black;pointer-events:auto"></div>
            <div class="td-editor-entry"><button>Tree Areas</button></div>
            <div class="td-game-panel" hidden><input aria-label="Area name" /></div>
        </div>
    `);
    await page.addStyleTag({
        content: readFileSync("src/editor/style.css", "utf8"),
    });
    await page.evaluate(() => {
        document
            .querySelector(".td-editor-entry button")!
            .addEventListener("click", () => {
                document.querySelector<HTMLElement>(".td-game-panel")!.hidden =
                    false;
            });
        document.querySelector("#world")!.addEventListener("click", () => {
            document.querySelector("#world")!.textContent = "World clicked";
        });
    });
    await page.getByRole("button", { name: "Tree Areas" }).click();
    await page.getByLabel("Area name").fill("Editor forest");
    await expect(page.getByLabel("Area name")).toHaveValue("Editor forest");
    await page.getByRole("button", { name: "World", exact: true }).click();
    await expect(
        page.getByRole("button", { name: "World clicked" }),
    ).toBeVisible();
});
