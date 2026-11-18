import {
  accountBalance, goalForecast, shiftInvoiceMonth, totalReserved, type FinanceData,
} from "./finance";

export type ProjectionMonth = {
  month: string;
  opening: number;
  income: number;
  expenses: number;
  commitments: number;
  contributions: number;
  ending: number;
};

function sum(values: number[]) { return values.reduce((total, value) => total + value, 0); }

function invoiceBalances(data: FinanceData) {
  const invoices = new Map<string, { month: string; amount: number }>();
  for (const charge of data.card_charges) {
    const key = `${charge.card_id}:${charge.invoice_month}`;
    const current = invoices.get(key);
    invoices.set(key, { month: charge.invoice_month.slice(0, 7), amount: (current?.amount ?? 0) + charge.amount });
  }
  for (const payment of data.card_payments) {
    const key = `${payment.card_id}:${payment.invoice_month}`;
    const current = invoices.get(key);
    if (current) invoices.set(key, { ...current, amount: Math.max(0, current.amount - payment.amount) });
  }
  return [...invoices.values()];
}

export function buildPlanning(data: FinanceData, today: string) {
  const currentMonth = today.slice(0, 7);
  const oldestHistoryMonth = shiftInvoiceMonth(`${currentMonth}-01`, -6).slice(0, 7);
  const historical = new Map<string, { income: number; expenses: number }>();
  for (const item of data.transactions) {
    const month = item.occurred_on.slice(0, 7);
    if (month < oldestHistoryMonth || month >= currentMonth || item.kind === "transfer") continue;
    const amounts = historical.get(month) ?? { income: 0, expenses: 0 };
    if (item.kind === "income") amounts.income += item.amount;
    else amounts.expenses += item.amount;
    historical.set(month, amounts);
  }
  const activeMonths = [...historical.entries()].sort(([a], [b]) => b.localeCompare(a))
    .slice(0, 6).map(([, amounts]) => amounts);
  const averageIncome = activeMonths.length ? sum(activeMonths.map((item) => item.income)) / activeMonths.length : 0;
  const averageExpense = activeMonths.length ? sum(activeMonths.map((item) => item.expenses)) / activeMonths.length : 0;
  const accounts = data.accounts.map((account) => accountBalance(account,
    data.transactions.filter((item) => item.occurred_on <= today),
    data.card_payments.filter((item) => item.paid_on <= today),
    data.payable_payments.filter((item) => item.paid_on <= today)));
  const openingBalance = sum(accounts) - totalReserved(data.reserve_movements.filter((item) => item.occurred_on <= today));
  const paidPayables = new Set(data.payable_payments.map((item) => item.payable_id));
  const invoices = invoiceBalances(data);
  const contributions = sum(data.goals.filter((goal) => goalForecast(goal, data.reserve_movements).remaining > 0)
    .map((goal) => goal.planned_monthly_contribution));
  const months: ProjectionMonth[] = [];
  let opening = openingBalance;
  for (let index = 1; index <= 6; index++) {
    const month = shiftInvoiceMonth(`${currentMonth}-01`, index).slice(0, 7);
    const monthIncome = sum(data.transactions.filter((item) => item.kind === "income" && item.occurred_on.startsWith(month)).map((item) => item.amount));
    const monthExpense = sum(data.transactions.filter((item) => item.kind === "expense" && item.occurred_on.startsWith(month)).map((item) => item.amount));
    const income = monthIncome > 0 ? monthIncome : averageIncome;
    const expenses = monthExpense > 0 ? monthExpense : averageExpense;
    const commitments = sum(data.payables.filter((item) => !item.is_deleted && !paidPayables.has(item.id)
      && (index === 1 ? item.due_on.slice(0, 7) <= month : item.due_on.startsWith(month))).map((item) => item.amount))
      + sum(invoices.filter((item) => item.amount > 0 && (index === 1 ? item.month <= month : item.month === month))
        .map((item) => item.amount));
    const ending = opening + income - expenses - commitments - contributions;
    months.push({ month, opening, income, expenses, commitments, contributions, ending });
    opening = ending;
  }
  return { openingBalance, averageIncome, averageExpense, historyMonths: activeMonths.length, months };
}
