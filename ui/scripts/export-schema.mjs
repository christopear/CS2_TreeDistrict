import { writeFileSync } from "node:fs";
import { z } from "zod";
import { catalogSchema, workspaceSchema } from "../src/domain/schema.ts";
for (const [name, schema] of [
    ["catalog", catalogSchema],
    ["workspace", workspaceSchema],
]) {
    writeFileSync(
        new URL(`../../data/${name}.v1.schema.json`, import.meta.url),
        JSON.stringify(z.toJSONSchema(schema), null, 2) + "\n",
    );
}
