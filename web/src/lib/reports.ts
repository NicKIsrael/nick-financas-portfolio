import type { FinanceData, TransactionKind } from "@/lib/finance";

export type ReportEntry = {
  id: string;
  date: string;
  description: string;
  amount: number;
  kind: TransactionKind;
  account_id: string;
  category_id: string | null;
  card_id: string | null;
  source: "Lançamento" | "Fatura paga" | "Pendência paga";
};

export function reportEntries(data: FinanceData): ReportEntry[] {
  const entries: ReportEntry[] = data.transactions
    .filter((item) => item.kind === "income" || item.kind === "expense")
    .map((item) => ({
      id: `transaction:${item.id}`, date: item.occurred_on, description: item.description,
      amount: item.amount, kind: item.kind as TransactionKind, account_id: item.account_id,
      category_id: item.category_id, card_id: null, source: "Lançamento" as const,
    }));

  for (const payment of data.card_payments) {
    const charges = data.card_charges.filter((item) =>
      item.card_id === payment.card_id && item.invoice_month === payment.invoice_month);
    if (charges.length === 0) {
      entries.push({ id: `card-payment:${payment.id}`, date: payment.paid_on,
        description: "Fatura do cartão", amount: payment.amount, kind: "expense",
        account_id: payment.account_id, category_id: null, card_id: payment.card_id,
        source: "Fatura paga" });
      continue;
    }
    for (const charge of charges) entries.push({
      id: `card-charge:${payment.id}:${charge.id}`, date: payment.paid_on,
      description: charge.description, amount: charge.amount, kind: "expense",
      account_id: payment.account_id, category_id: charge.category_id,
      card_id: payment.card_id, source: "Fatura paga",
    });
  }

  for (const payment of data.payable_payments) {
    const payable = data.payables.find((item) => item.id === payment.payable_id);
    entries.push({ id: `payable-payment:${payment.id}`, date: payment.paid_on,
      description: payable?.description ?? "Pendência paga", amount: payment.amount,
      kind: "expense", account_id: payment.account_id,
      category_id: payable?.category_id ?? null, card_id: null, source: "Pendência paga" });
  }
  return entries;
}

export function monthReport(entries: ReportEntry[], month: string) {
  const current = entries.filter((item) => item.date.startsWith(month));
  return {
    entries: current,
    income: current.filter((item) => item.kind === "income").reduce((sum, item) => sum + item.amount, 0),
    expense: current.filter((item) => item.kind === "expense").reduce((sum, item) => sum + item.amount, 0),
  };
}
