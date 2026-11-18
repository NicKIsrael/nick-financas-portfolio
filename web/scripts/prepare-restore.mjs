import { readFileSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const order = [
  "accounts", "categories", "credit_cards", "recurring_payables", "reserves", "payables",
  "goals", "transactions", "card_charges", "card_payments", "payable_payments", "reserve_movements",
];

function quotedJson(value, index) {
  const json = JSON.stringify(value);
  let tag = `$nickfinancas_${index}$`;
  while (json.includes(tag)) tag = `$nickfinancas_${index}_${Math.random().toString(36).slice(2)}$`;
  return `${tag}${json}${tag}::jsonb`;
}

export function prepareRestore(exported) {
  if (exported?.format !== "nickfinancas-export" || exported.version !== 1 || !exported.tables)
    throw new Error("Arquivo de exportação inválido ou versão não reconhecida.");
  for (const table of order) {
    if (!Array.isArray(exported.tables[table]) || exported.tables[table].some((row) => !row || typeof row !== "object" || typeof row.id !== "string"))
      throw new Error(`Tabela ${table} ausente ou inválida.`);
  }
  const sql = [
    "-- Restauracao NickFinancas v1. Execute somente em um projeto novo com migracoes 001 a 008 e usuarios autorizados.",
    "-- A operacao para se qualquer tabela financeira ja contiver registros. Identificadores e datas sao preservados.",
    "-- Autores existentes sao preservados. Outros autores sao atribuidos ao primeiro usuario autorizado do projeto novo.",
    "begin;",
    "do $$ begin",
    "  if not exists (select 1 from public.allowed_users) then raise exception 'Autorize ao menos um usuario antes de restaurar'; end if;",
    ...order.map((table) => `  if exists (select 1 from public.${table} limit 1) then raise exception 'Tabela ${table} nao esta vazia'; end if;`),
    "end $$;",
  ];
  for (const [index, table] of order.entries()) {
    const rows = exported.tables[table];
    if (!rows.length) continue;
    sql.push(`-- ${table}: ${rows.length} registro(s)`);
    sql.push(`with payload as (select ${quotedJson(rows, index)} as data),`);
    sql.push("mapped as (select coalesce(jsonb_agg(case when item ? 'created_by' then jsonb_set(item, '{created_by}', to_jsonb(coalesce((select id::text from auth.users where id::text = item->>'created_by'), (select user_id::text from public.allowed_users order by user_id limit 1)))) else item end), '[]'::jsonb) as data from payload, jsonb_array_elements(payload.data) as rows(item))");
    sql.push(`insert into public.${table} select * from jsonb_populate_recordset(null::public.${table}, (select data from mapped));`);
  }
  sql.push("commit;");
  return sql.join("\n") + "\n";
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [, , sourcePath, outputPath] = process.argv;
  if (!sourcePath || !outputPath) {
    process.stderr.write("Uso: node scripts/prepare-restore.mjs <exportacao.json> <restauracao.sql>\n");
    process.exitCode = 1;
  } else {
    try {
      const exported = JSON.parse(readFileSync(resolve(sourcePath), "utf8"));
      writeFileSync(resolve(outputPath), prepareRestore(exported), { flag: "wx" });
      process.stdout.write(`SQL de restauracao salvo em ${resolve(outputPath)}\n`);
    } catch (error) {
      process.stderr.write(`${error instanceof Error ? error.message : "Erro desconhecido"}\n`);
      process.exitCode = 1;
    }
  }
}
