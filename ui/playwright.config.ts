import { defineConfig } from "@playwright/test";
const port = Number(process.env.TREEDISTRICT_TEST_PORT ?? 5173);
const baseURL = `http://127.0.0.1:${port}`;
export default defineConfig({
    testDir: "tests/browser",
    use: {
        baseURL,
        viewport: { width: 1440, height: 1000 },
    },
    outputDir: "../artifacts/browser-results",
    webServer: {
        command: `npm run dev -- --port ${port} --strictPort`,
        url: baseURL,
        reuseExistingServer: !process.env.CI,
    },
});
