export type TransactionKind = "income" | "expense";
export type MovementKind = TransactionKind | "transfer";

export type Account = {
  id: string;
  name: string;
  type: "checking" | "savings" | "cash" | "investment";
  initial_balance: number;
};

export type Category = {
  id: string;
  name: string;
  kind: TransactionKind;
};

export type Transaction = {
  id: string;
  account_id: string;
  category_id: string | null;
  destination_account_id: string | null;
  kind: MovementKind;
  description: string;
  amount: number;
  occurred_on: string;
  created_by?: string;
};

export type CreditCard = {
  id: string;
  name: string;
  payment_account_id: string;
  credit_limit: number;
  spending_limit: number;
  closing_day: number;
  due_day: number;
};

export type CardCharge = {
  id: string;
  card_id: string;
  category_id: string;
  description: string;
  amount: number;
  purchased_on: string;
  invoice_month: string;
  purchase_group_id: string;
  installment_number: number;
  installment_count: number;
};

export type CardPayment = {
  id: string;
  card_id: string;
  invoice_month: string;
  account_id: string;
  amount: number;
  paid_on: string;
};

export type Payable = {
  id: string;
  description: string;
  amount: number;
  due_on: string;
  account_id: string;
  category_id: string;
  installment_group_id: string | null;
  installment_number: number | null;
  installment_count: number | null;
  recurring_payable_id: string | null;
  is_deleted?: boolean;
};

export type RecurringPayable = {
  id: string;
  description: string;
  amount: number;
  due_day: number;
  start_on: string;
  end_on: string | null;
  account_id: string;
  category_id: string;
  is_active: boolean;
};

export type PayablePayment = {
  id: string;
  payable_id: string;
  account_id: string;
  amount: number;
  paid_on: string;
};

export type Reserve = {
  id: string;
  name: string;
};

export type ReserveMovement = {
  id: string;
  reserve_id: string;
  account_id: string;
  amount: number;
  kind: "deposit" | "withdrawal";
  occurred_on: string;
  transfer_group_id: string | null;
};

export type Goal = {
  id: string;
  name: string;
  target_amount: number;
  target_on: string | null;
  planned_monthly_contribution: number;
  reserve_id: string | null;
  is_primary: boolean;
};

export type FinanceData = {
  accounts: Account[];
  categories: Category[];
  transactions: Transaction[];
  cards: CreditCard[];
  card_charges: CardCharge[];
  card_payments: CardPayment[];
  payables: Payable[];
  payable_payments: PayablePayment[];
  recurring_payables: RecurringPayable[];
  reserves: Reserve[];
  reserve_movements: ReserveMovement[];
  goals: Goal[];
};

export const demoData: FinanceData = {
  accounts: [
    { id: "demo-checking", name: "Conta principal", type: "checking", initial_balance: 4800 },
    { id: "demo-savings", name: "Poupança", type: "savings", initial_balance: 7200 },
    { id: "demo-cash", name: "Carteira", type: "cash", initial_balance: 180 },
  ],
  categories: [
    { id: "cat-salary", name: "Salário", kind: "income" },
    { id: "cat-extra", name: "Renda extra", kind: "income" },
    { id: "cat-home", name: "Moradia", kind: "expense" },
    { id: "cat-food", name: "Alimentação", kind: "expense" },
    { id: "cat-transport", name: "Transporte", kind: "expense" },
    { id: "cat-leisure", name: "Lazer", kind: "expense" },
  ],
  transactions: [
    { id: "tx-1", account_id: "demo-checking", destination_account_id: null, category_id: "cat-salary", kind: "income", description: "Salário", amount: 5200, occurred_on: "2026-10-05" },
    { id: "tx-2", account_id: "demo-checking", destination_account_id: null, category_id: "cat-home", kind: "expense", description: "Aluguel", amount: 1680, occurred_on: "2026-10-07" },
    { id: "tx-3", account_id: "demo-checking", destination_account_id: null, category_id: "cat-food", kind: "expense", description: "Mercado da semana", amount: 284.90, occurred_on: "2026-10-12" },
    { id: "tx-4", account_id: "demo-checking", destination_account_id: null, category_id: "cat-transport", kind: "expense", description: "Combustível", amount: 190, occurred_on: "2026-10-15" },
    { id: "tx-5", account_id: "demo-checking", destination_account_id: null, category_id: "cat-extra", kind: "income", description: "Freelance", amount: 850, occurred_on: "2026-10-16" },
    { id: "tx-6", account_id: "demo-checking", destination_account_id: null, category_id: "cat-leisure", kind: "expense", description: "Cinema", amount: 76, occurred_on: "2026-10-17" },
    { id: "tx-7", account_id: "demo-checking", destination_account_id: "demo-savings", category_id: null, kind: "transfer", description: "Guardar na poupança", amount: 500, occurred_on: "2026-10-17" },
  ],
  cards: [],
  card_charges: [],
  card_payments: [],
  payables: [],
  payable_payments: [],
  recurring_payables: [],
  reserves: [],
  reserve_movements: [],
  goals: [],
};

export const currency = (value: number) =>
  new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(value);

export const monthLabel = (month: string) => {
  const [year, number] = month.split("-").map(Number);
  const label = new Intl.DateTimeFormat("pt-BR", { month: "long", year: "numeric", timeZone: "UTC" })
    .format(new Date(Date.UTC(year, number - 1, 1)));
  return label.charAt(0).toLocaleUpperCase("pt-BR") + label.slice(1);
};

export const dateLabel = (date: string) => {
  const [year, month, day] = date.split("-").map(Number);
  return new Intl.DateTimeFormat("pt-BR", { day: "2-digit", month: "short", timeZone: "UTC" })
    .format(new Date(Date.UTC(year, month - 1, day)));
};

export const accountBalance = (account: Account, transactions: Transaction[], cardPayments: CardPayment[] = [], payablePayments: PayablePayment[] = []) =>
  account.initial_balance - [...cardPayments, ...payablePayments]
    .filter((item) => item.account_id === account.id)
    .reduce((total, item) => total + item.amount, 0) + transactions
    .filter((item) => item.account_id === account.id || item.destination_account_id === account.id)
    .reduce((total, item) => {
      if (item.kind === "transfer") return total + (item.destination_account_id === account.id ? item.amount : -item.amount);
      return total + (item.kind === "income" ? item.amount : -item.amount);
    }, 0);

export const reserveBalance = (reserveId: string, movements: ReserveMovement[]) =>
  movements.filter((item) => item.reserve_id === reserveId)
    .reduce((sum, item) => sum + (item.kind === "deposit" ? item.amount : -item.amount), 0);

export const totalReserved = (movements: ReserveMovement[]) =>
  movements.reduce((sum, item) => sum + (item.kind === "deposit" ? item.amount : -item.amount), 0);

export function goalForecast(goal: Goal, movements: ReserveMovement[]) {
  const current = goal.reserve_id ? reserveBalance(goal.reserve_id, movements) : 0;
  const remaining = Math.max(0, goal.target_amount - current);
  const monthlyDeposits = new Map<string, number>();
  for (const item of movements) {
    if (item.reserve_id === goal.reserve_id && item.kind === "deposit") {
      const month = item.occurred_on.slice(0, 7);
      monthlyDeposits.set(month, (monthlyDeposits.get(month) ?? 0) + item.amount);
    }
  }
  const average = monthlyDeposits.size
    ? [...monthlyDeposits.values()].reduce((sum, amount) => sum + amount, 0) / monthlyDeposits.size
    : 0;
  const monthly = goal.planned_monthly_contribution > 0 ? goal.planned_monthly_contribution : average;
  const source = goal.planned_monthly_contribution > 0 ? "aporte planejado" : "média real dos aportes";
  const months = remaining === 0 ? 0 : monthly > 0 ? Math.ceil(remaining / monthly) : null;
  return { current, remaining, average, months, source, percentage: Math.min(100, current / goal.target_amount * 100) };
}

export const cardUsedLimit = (cardId: string, charges: CardCharge[], payments: CardPayment[]) =>
  Math.max(0, charges.filter((item) => item.card_id === cardId).reduce((sum, item) => sum + item.amount, 0) -
    payments.filter((item) => item.card_id === cardId).reduce((sum, item) => sum + item.amount, 0));

export function invoiceMonthForDate(purchasedOn: string, closingDay: number, dueDay: number) {
  const [year, month, day] = purchasedOn.split("-").map(Number);
  const offset = (day > closingDay ? 1 : 0) + (dueDay <= closingDay ? 1 : 0);
  const invoice = new Date(Date.UTC(year, month - 1 + offset, 1));
  return `${invoice.getUTCFullYear()}-${String(invoice.getUTCMonth() + 1).padStart(2, "0")}-01`;
}

export function shiftInvoiceMonth(invoiceMonth: string, offset: number) {
  const [year, month] = invoiceMonth.split("-").map(Number);
  const result = new Date(Date.UTC(year, month - 1 + offset, 1));
  return `${result.getUTCFullYear()}-${String(result.getUTCMonth() + 1).padStart(2, "0")}-01`;
}

export function safeMonthDay(invoiceMonth: string, day: number) {
  const [year, month] = invoiceMonth.split("-").map(Number);
  const lastDay = new Date(Date.UTC(year, month, 0)).getUTCDate();
  return `${year}-${String(month).padStart(2, "0")}-${String(Math.min(day, lastDay)).padStart(2, "0")}`;
}

export function materializeDemoRecurrences(month: string, current: FinanceData): FinanceData {
  const monthStart = `${month}-01`;
  const nextMonth = shiftInvoiceMonth(monthStart, 1);
  const additions: Payable[] = current.recurring_payables
    .filter((rule) => rule.is_active && rule.start_on < nextMonth && (!rule.end_on || rule.end_on >= monthStart))
    .filter((rule) => !current.payables.some((item) => item.recurring_payable_id === rule.id && item.due_on.startsWith(month)))
    .map((rule) => ({
      id: crypto.randomUUID(), description: rule.description, amount: rule.amount,
      due_on: safeMonthDay(monthStart, rule.due_day), account_id: rule.account_id,
      category_id: rule.category_id, installment_group_id: null,
      installment_number: null, installment_count: null, recurring_payable_id: rule.id,
    }));
  return additions.length ? { ...current, payables: [...current.payables, ...additions] } : current;
}

export const todayLocal = () => {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
};
