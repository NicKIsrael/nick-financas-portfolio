import { readFileSync, readdirSync, writeFileSync } from "node:fs";
import { resolve, join } from "node:path";
import { fileURLToPath } from "node:url";

const base = resolve(fileURLToPath(new URL("../supabase/migrations/", import.meta.url)));
const migrations = readdirSync(base).filter((name) => /^\d+_.*\.sql$/.test(name)).sort();
const output = migrations.map((name) => `-- ${name}\n${readFileSync(join(base, name), "utf8")}`).join("\n\n");

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const target = process.argv[2];
  if (!target) {
    process.stderr.write("Uso: node scripts/prepare-schema.mjs <schema-completo.sql>\n");
    process.exitCode = 1;
  } else {
    writeFileSync(resolve(target), output, { flag: "wx" });
    process.stdout.write(`Esquema completo salvo em ${resolve(target)} (${migrations.length} migracoes)\n`);
  }
}
