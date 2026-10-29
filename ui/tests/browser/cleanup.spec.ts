import { test, expect } from "@playwright/test";

test("global cleanup requires confirmation and works without a selected area", async ({
    page,
}) => {
    await page.goto("/");
    await page.evaluate(async () => {
        const load = (path: string) => import(/* @vite-ignore */ path);
        const React = (await load("/node_modules/.vite/deps/react.js")).default;
        const { default: ReactDOM } = await load(
            "/node_modules/.vite/deps/react-dom_client.js",
        );
        const { Editor } = await load("/src/editor/Editor.tsx");
        const { Controls } = await load("/src/editor/controls.tsx");
        const { initialWorkspace } = await load("/src/domain/store.ts");
        const h = React.createElement;
        const controls = {
            Panel: ({ children }: any) => h("div", null, children),
            Button: ({ children, onSelect, disabled }: any) =>
                h("button", { onClick: onSelect, disabled }, children),
            Input: (props: any) => h("input", props),
            Checkbox: () => null,
            Choice: () => null,
        };
        const host = document.createElement("div");
        document.body.replaceChildren(host);
        ReactDOM.createRoot(host).render(
            h(
                Controls.Provider,
                { value: controls },
                h(Editor, {
                    state: initialWorkspace(),
                    selectedId: null,
                    send: () => {},
                    select: () => {},
                    beginDrawing: () => {},
                    drawing: false,
                    error: null,
                    deleteAllVegetation: () => {
                        document.body.dataset.cleanupCalls = String(
                            Number(document.body.dataset.cleanupCalls || 0) + 1,
                        );
                    },
                }),
            ),
        );
    });
    await page.getByRole("button", { name: "Map cleanup" }).click();
    await page
        .getByRole("button", {
            name: "DELETE ALL TREES AND AREAS",
            exact: true,
        })
        .click();
    await expect(
        page.getByText(/including baked, manually placed/),
    ).toBeVisible();
    await expect(page.locator("body")).not.toHaveAttribute(
        "data-cleanup-calls",
    );
    await page.getByRole("button", { name: "Cancel", exact: true }).click();
    await expect(page.locator("body")).not.toHaveAttribute(
        "data-cleanup-calls",
    );
    await page
        .getByRole("button", {
            name: "DELETE ALL TREES AND AREAS",
            exact: true,
        })
        .click();
    await page
        .getByRole("button", {
            name: "Confirm DELETE ALL TREES AND AREAS",
            exact: true,
        })
        .click();
    await expect(page.locator("body")).toHaveAttribute(
        "data-cleanup-calls",
        "1",
    );
});

