import assert from "node:assert/strict";
import test from "node:test";
import { monthReport, reportEntries } from "../src/lib/reports.ts";

test("relatório atribui cada saída à data em que o dinheiro saiu", () => {
  const data = {
    transactions: [
      { id: "income", kind: "income", description: "Salário", amount: 100, occurred_on: "2026-10-02", account_id: "a", category_id: "salary" },
      { id: "expense", kind: "expense", description: "Mercado", amount: 10, occurred_on: "2026-10-03", account_id: "a", category_id: "food" },
      { id: "transfer", kind: "transfer", description: "Transferência", amount: 20, occurred_on: "2026-10-04", account_id: "a", category_id: null },
    ],
    card_charges: [{ id: "charge", card_id: "card", category_id: "food", description: "Compra", amount: 30, invoice_month: "2026-11-01" }],
    card_payments: [{ id: "card-payment", card_id: "card", account_id: "a", amount: 30, invoice_month: "2026-11-01", paid_on: "2026-11-05" }],
    payables: [{ id: "payable", description: "Internet", category_id: "home" }],
    payable_payments: [{ id: "payable-payment", payable_id: "payable", account_id: "a", amount: 15, paid_on: "2026-10-18" }],
  };
  const entries = reportEntries(data);
  assert.equal(entries.length, 4);
  assert.deepEqual({ income: monthReport(entries, "2026-10").income, expense: monthReport(entries, "2026-10").expense }, { income: 100, expense: 25 });
  assert.deepEqual({ income: monthReport(entries, "2026-11").income, expense: monthReport(entries, "2026-11").expense }, { income: 0, expense: 30 });
  assert.equal(entries.find((item) => item.source === "Fatura paga")?.category_id, "food");
  assert.equal(entries.find((item) => item.source === "Pendência paga")?.category_id, "home");
});
