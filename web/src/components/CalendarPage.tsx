"use client";

import { useState, type Dispatch, type SetStateAction } from "react";
import { CalendarDays, ChevronLeft, ChevronRight, Plus } from "lucide-react";
import {
  currency, materializeDemoRecurrences, monthLabel, safeMonthDay, shiftInvoiceMonth, todayLocal,
  type FinanceData,
} from "@/lib/finance";
import { getSupabase, isDemo } from "@/lib/supabase";

type Props = {
  data: FinanceData;
  setData: Dispatch<SetStateAction<FinanceData>>;
  reload: () => Promise<void>;
  onSchedule: (date: string) => void;
};

type CalendarEvent = {
  id: string;
  date: string;
  title: string;
  detail: string;
  amount: number;
  tone: "income" | "expense" | "pending" | "neutral";
};

function eventsForMonth(data: FinanceData, month: string): Map<string, CalendarEvent[]> {
  const result = new Map<string, CalendarEvent[]>();
  const add = (event: CalendarEvent) => {
    if (!event.date.startsWith(month)) return;
    result.set(event.date, [...(result.get(event.date) ?? []), event]);
  };
  const accountName = (id: string) => data.accounts.find((item) => item.id === id)?.name ?? "Conta removida";
  for (const item of data.transactions) add({ id: `transaction:${item.id}`, date: item.occurred_on,
    title: item.description, detail: `${item.kind === "income" ? "Receita" : item.kind === "expense" ? "Despesa" : "Transferência"} · ${accountName(item.account_id)}`,
    amount: item.amount, tone: item.kind === "transfer" ? "neutral" : item.kind });
  for (const item of data.payables.filter((entry) => !entry.is_deleted)) {
    const payment = data.payable_payments.find((entry) => entry.payable_id === item.id);
    add({ id: `payable:${item.id}`, date: item.due_on, title: item.description,
      detail: `${payment ? "Pendência paga" : "Conta a vencer"} · ${accountName(item.account_id)}${item.recurring_payable_id ? " · mensal" : ""}`,
      amount: item.amount, tone: payment ? "neutral" : "pending" });
  }
  for (const payment of data.payable_payments) {
    const payable = data.payables.find((item) => item.id === payment.payable_id);
    add({ id: `payable-payment:${payment.id}`, date: payment.paid_on,
      title: payable?.description ?? "Pendência paga", detail: `Pagamento · ${accountName(payment.account_id)}`,
      amount: payment.amount, tone: "expense" });
  }
  for (const payment of data.card_payments) {
    const card = data.cards.find((item) => item.id === payment.card_id);
    add({ id: `card-payment:${payment.id}`, date: payment.paid_on,
      title: `Fatura ${card?.name ?? "Cartão removido"}`, detail: `Pagamento · ${accountName(payment.account_id)}`,
      amount: payment.amount, tone: "expense" });
  }
  for (const card of data.cards) {
    const invoiceMonth = `${month}-01`;
    if (data.card_payments.some((item) => item.card_id === card.id && item.invoice_month === invoiceMonth)) continue;
    const amount = data.card_charges.filter((item) => item.card_id === card.id && item.invoice_month === invoiceMonth)
      .reduce((sum, item) => sum + item.amount, 0);
    if (amount > 0) add({ id: `invoice:${card.id}:${month}`, date: safeMonthDay(invoiceMonth, card.due_day),
      title: `Fatura ${card.name}`, detail: `Fatura a vencer · ${accountName(card.payment_account_id)}`,
      amount, tone: "pending" });
  }
  return result;
}

export default function CalendarPage({ data, setData, reload, onSchedule }: Props) {
  const [month, setMonth] = useState(() => todayLocal().slice(0, 7));
  const [selected, setSelected] = useState(() => todayLocal());
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const events = eventsForMonth(data, month);
  const selectedEvents = events.get(selected) ?? [];
  const [year, number] = month.split("-").map(Number);
  const firstDay = new Date(Date.UTC(year, number - 1, 1));
  const leading = (firstDay.getUTCDay() + 6) % 7;
  const daysInMonth = new Date(Date.UTC(year, number, 0)).getUTCDate();
  const dates: (string | null)[] = [
    ...Array.from({ length: leading }, () => null),
    ...Array.from({ length: daysInMonth }, (_, index) => `${month}-${String(index + 1).padStart(2, "0")}`),
  ];
  while (dates.length % 7) dates.push(null);

  async function changeMonth(offset: number) {
    const next = shiftInvoiceMonth(`${month}-01`, offset).slice(0, 7);
    setBusy(true);
    setError("");
    try {
      if (isDemo) setData((current) => materializeDemoRecurrences(next, current));
      else {
        const { error: generationError } = await getSupabase()!.rpc("generate_recurring_payables", { p_month: `${next}-01` });
        if (generationError) throw generationError;
        await reload();
      }
      setMonth(next);
      setSelected(todayLocal().startsWith(next) ? todayLocal() : `${next}-01`);
    } catch (cause) { setError(cause instanceof Error ? cause.message : "Não foi possível abrir o mês."); }
    finally { setBusy(false); }
  }

  return <>
    <div className="page-heading calendar-heading"><div><p className="eyebrow">DIA A DIA</p><h1>Calendário</h1><p className="muted">Veja lançamentos, vencimentos e faturas em cada dia.</p></div><button className="primary-button" disabled={!data.accounts.length || !data.categories.some((item) => item.kind === "expense")} onClick={() => onSchedule(selected)}><Plus size={17} /> Agendar para este dia</button></div>
    {error && <div className="error-banner" role="alert">{error}</div>}
    <div className="toolbar"><div className="period-switch"><button aria-label="Mês anterior" disabled={busy} onClick={() => void changeMonth(-1)}><ChevronLeft size={17} /></button><span>{monthLabel(month)}</span><button aria-label="Próximo mês" disabled={busy} onClick={() => void changeMonth(1)}><ChevronRight size={17} /></button></div><span className="period-caption">Segunda a domingo</span></div>
    <div className="calendar-layout"><section className="panel calendar-panel"><div className="calendar-grid">{["Seg", "Ter", "Qua", "Qui", "Sex", "Sáb", "Dom"].map((day) => <div className="calendar-weekday" key={day}>{day}</div>)}{dates.map((date, index) => date ? <button key={date} className={`calendar-day ${date === selected ? "selected" : ""} ${date === todayLocal() ? "today" : ""}`} aria-label={`${date.split("-").reverse().join("/")}, ${(events.get(date) ?? []).length} compromissos`} aria-pressed={date === selected} onClick={() => setSelected(date)}><strong>{date.slice(8, 10)}</strong>{(events.get(date) ?? []).length > 0 && <span className="calendar-count">{events.get(date)!.length}</span>}</button> : <div className="calendar-empty" key={`empty-${index}`} />)}</div></section>
      <section className="panel calendar-detail"><div className="section-title"><div><h2>{selected.split("-").reverse().join("/")}</h2><p>{selectedEvents.length === 0 ? "Nenhum compromisso neste dia" : `${selectedEvents.length} ${selectedEvents.length === 1 ? "compromisso" : "compromissos"}`}</p></div><CalendarDays size={18} /></div>{selectedEvents.length ? <div className="calendar-events">{selectedEvents.map((event) => <div className={`calendar-event ${event.tone}`} key={event.id}><div><strong>{event.title}</strong><small>{event.detail}</small></div><b>{currency(event.amount)}</b></div>)}</div> : <div className="empty-state">Dia livre de registros e vencimentos.</div>}<button className="text-button calendar-schedule" disabled={!data.accounts.length || !data.categories.some((item) => item.kind === "expense")} onClick={() => onSchedule(selected)}><Plus size={15} /> Agendar pendência neste dia</button></section></div>
  </>;
}
