import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";
import test from "node:test";
import ts from "typescript";

const financeUrl = pathToFileURL(resolve("src/lib/finance.ts")).href;
const source = readFileSync(resolve("src/lib/planning.ts"), "utf8").replace('from "./finance"', `from "${financeUrl}"`);
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText;
const { buildPlanning } = await import(`data:text/javascript,${encodeURIComponent(compiled)}`);

test("planejamento separa despesas diretas, compromissos e aportes sem descontar pagamentos duas vezes", () => {
  const data = {
    accounts: [{ id: "a", initial_balance: 100, name: "Conta", type: "checking" }],
    categories: [],
    transactions: [
      { id: "past", kind: "expense", amount: 10, occurred_on: "2026-09-10", account_id: "a", destination_account_id: null },
      { id: "future", kind: "income", amount: 20, occurred_on: "2026-11-05", account_id: "a", destination_account_id: null },
    ],
    cards: [],
    card_charges: [{ id: "charge", card_id: "card", invoice_month: "2026-11-01", amount: 15 }],
    card_payments: [],
    payables: [{ id: "p", amount: 5, due_on: "2026-11-18" }],
    payable_payments: [],
    recurring_payables: [],
    reserves: [], reserve_movements: [],
    goals: [{ id: "g", target_amount: 50, reserve_id: null, planned_monthly_contribution: 2 }],
  };
  const result = buildPlanning(data, "2026-10-18");
  assert.equal(result.openingBalance, 90);
  assert.equal(result.averageExpense, 10);
  assert.deepEqual(result.months[0], {
    month: "2026-11", opening: 90, income: 20, expenses: 10, commitments: 20, contributions: 2, ending: 78,
  });
  assert.equal(result.months[1].commitments, 0);
});

test("planejamento inclui vencimento atrasado uma vez e ignora pendência paga", () => {
  const data = {
    accounts: [{ id: "a", initial_balance: 50, name: "Conta", type: "checking" }],
    categories: [], transactions: [], cards: [], card_charges: [], card_payments: [],
    payables: [{ id: "overdue", amount: 15, due_on: "2026-10-01" }, { id: "paid", amount: 5, due_on: "2026-11-01" }],
    payable_payments: [{ id: "payment", payable_id: "paid", account_id: "a", amount: 5, paid_on: "2026-10-10" }],
    recurring_payables: [], reserves: [], reserve_movements: [], goals: [],
  };
  const result = buildPlanning(data, "2026-10-18");
  assert.equal(result.openingBalance, 45);
  assert.equal(result.months[0].commitments, 15);
  assert.equal(result.months[1].commitments, 0);
});
