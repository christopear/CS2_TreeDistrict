import { writeFileSync } from "node:fs";
import { z } from "zod";
import { catalogSchema, workspaceSchema } from "../src/domain/schema.ts";
for (const [name, version, schema] of [
    ["catalog", 2, catalogSchema],
    ["workspace", 1, workspaceSchema],
]) {
    writeFileSync(
        new URL(`../../data/${name}.v${version}.schema.json`, import.meta.url),
        JSON.stringify(z.toJSONSchema(schema), null, 2) + "\n",
    );
}
