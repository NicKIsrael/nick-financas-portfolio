import assert from "node:assert/strict";
import test from "node:test";
import { prepareRestore } from "../scripts/prepare-restore.mjs";

const tables = Object.fromEntries([
  "accounts", "categories", "transactions", "credit_cards", "card_charges", "card_payments",
  "payables", "payable_payments", "recurring_payables", "reserves", "reserve_movements", "goals",
].map((name) => [name, []]));

test("restauração mantém ordem das dependências e embute texto como JSON", () => {
  const exported = { format: "nickfinancas-export", version: 1, tables: {
    ...tables,
    accounts: [{ id: "11111111-1111-1111-1111-111111111111", name: "Conta ' especial $nickfinancas_0$" }],
    payables: [{ id: "22222222-2222-2222-2222-222222222222", description: "Luz" }],
  } };
  const sql = prepareRestore(exported);
  assert.ok(sql.indexOf("insert into public.accounts") < sql.indexOf("insert into public.payables"));
  assert.match(sql, /Conta ' especial \$nickfinancas_0\$/);
  assert.ok(sql.includes("begin;") && sql.includes("commit;"));
  assert.ok(!sql.includes("$nickfinancas_0$[{"));
});

test("restauração rejeita formato inesperado", () => {
  assert.throws(() => prepareRestore({ version: 99, tables }), /inválido/);
});
