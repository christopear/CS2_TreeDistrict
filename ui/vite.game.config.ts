import { defineConfig } from "vite";
// Contract smoke build only. The installed official CS2 template owns release builds.
export default defineConfig({
    build: {
        outDir: "../artifacts/ui-contract",
        emptyOutDir: true,
        lib: {
            entry: "src/game/index.tsx",
            formats: ["es"],
            fileName: () => "TreeDistrict.mjs",
            cssFileName: "TreeDistrict",
        },
        rollupOptions: {
            external: [
                "react",
                "react-dom",
                "cs2/api",
                "cs2/ui",
                "cs2/modding",
            ],
        },
    },
});
