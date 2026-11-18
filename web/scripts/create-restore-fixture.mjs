import { writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const id = (digit) => `${digit.repeat(8)}-${digit.repeat(4)}-${digit.repeat(4)}-${digit.repeat(4)}-${digit.repeat(12)}`;
const author = id("d");
const created_at = "2026-10-18T12:00:00+00:00";
const account = id("1");
const incomeCategory = id("2");
const expenseCategory = id("3");
const card = id("4");
const recurrence = id("5");
const payable = id("6");
const reserve = id("7");

export const fixture = {
  format: "nickfinancas-export",
  version: 1,
  exported_at: "2026-10-18T12:00:00.000Z",
  tables: {
    accounts: [{ id: account, name: "Conta teste", type: "checking", initial_balance: "100.00", created_at }],
    categories: [
      { id: incomeCategory, name: "Entrada teste", kind: "income", created_at },
      { id: expenseCategory, name: "Saída teste", kind: "expense", created_at },
    ],
    transactions: [
      { id: id("8"), account_id: account, category_id: incomeCategory, destination_account_id: null, kind: "income", description: "Entrada teste", amount: "20.00", occurred_on: "2026-10-18", created_by: author, created_at },
      { id: id("9"), account_id: account, category_id: expenseCategory, destination_account_id: null, kind: "expense", description: "Saída teste", amount: "10.00", occurred_on: "2026-10-18", created_by: author, created_at },
    ],
    credit_cards: [{ id: card, name: "Cartão teste", payment_account_id: account, credit_limit: "100.00", spending_limit: "100.00", closing_day: 25, due_day: 5, created_at }],
    card_charges: [{ id: id("a"), card_id: card, category_id: expenseCategory, description: "Compra teste", amount: "15.00", purchased_on: "2026-10-18", invoice_month: "2026-11-01", purchase_group_id: id("e"), installment_number: 1, installment_count: 1, created_by: author, created_at }],
    card_payments: [{ id: id("b"), card_id: card, invoice_month: "2026-11-01", account_id: account, amount: "15.00", paid_on: "2026-11-05", created_by: author, created_at }],
    recurring_payables: [{ id: recurrence, description: "Recorrência teste", amount: "5.00", due_day: 18, start_on: "2026-10-18", end_on: "2026-10-18", account_id: account, category_id: expenseCategory, is_active: false, created_by: author, created_at }],
    payables: [{ id: payable, description: "Pendência teste", amount: "5.00", due_on: "2026-10-18", account_id: account, category_id: expenseCategory, installment_group_id: null, installment_number: null, installment_count: null, created_by: author, created_at, recurring_payable_id: recurrence, is_deleted: false }],
    payable_payments: [{ id: id("c"), payable_id: payable, account_id: account, amount: "5.00", paid_on: "2026-10-18", created_by: author, created_at }],
    reserves: [{ id: reserve, name: "Reserva teste", created_by: author, created_at }],
    reserve_movements: [{ id: id("f"), reserve_id: reserve, account_id: account, amount: "20.00", kind: "deposit", occurred_on: "2026-10-18", transfer_group_id: null, created_by: author, created_at }],
    goals: [{ id: id("0"), name: "Objetivo teste", target_amount: "40.00", target_on: null, planned_monthly_contribution: "10.00", reserve_id: reserve, is_primary: true, created_by: author, created_at }],
  },
};

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const target = process.argv[2];
  if (!target) {
    process.stderr.write("Uso: node scripts/create-restore-fixture.mjs <amostra.json>\n");
    process.exitCode = 1;
  } else {
    writeFileSync(resolve(target), JSON.stringify(fixture, null, 2), { flag: "wx" });
    process.stdout.write(`Amostra salva em ${resolve(target)}\n`);
  }
}
