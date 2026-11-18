"use client";

import { useState, type Dispatch, type FormEvent, type SetStateAction } from "react";
import { CalendarDays, ChevronLeft, ChevronRight, Plus, Trash2, X } from "lucide-react";
import {
  currency, dateLabel, materializeDemoRecurrences, monthLabel, safeMonthDay, shiftInvoiceMonth, todayLocal,
  type FinanceData, type Payable, type PayablePayment, type RecurringPayable,
} from "@/lib/finance";
import { getSupabase, isDemo } from "@/lib/supabase";

type Props = {
  data: FinanceData;
  setData: Dispatch<SetStateAction<FinanceData>>;
  reload: () => Promise<void>;
  initialDueOn?: string | null;
  onCalendar?: () => void;
};

function errorText(cause: unknown) {
  return cause instanceof Error ? cause.message : "Não foi possível concluir a operação.";
}

export default function PayablesPage({ data, setData, reload, initialDueOn, onCalendar }: Props) {
  const [month, setMonth] = useState(() => initialDueOn?.slice(0, 7) ?? todayLocal().slice(0, 7));
  const [modal, setModal] = useState<"new" | "pay" | "recurring" | null>(initialDueOn ? "new" : null);
  const [selected, setSelected] = useState<Payable | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const items = data.payables.filter((item) => !item.is_deleted && item.due_on.startsWith(month)).sort((a, b) => a.due_on.localeCompare(b.due_on) || a.description.localeCompare(b.description));
  const expenseCategories = data.categories.filter((item) => item.kind === "expense");
  const paymentsByPayable = new Map(data.payable_payments.map((item) => [item.payable_id, item]));
  const pendingTotal = items.filter((item) => !paymentsByPayable.has(item.id)).reduce((sum, item) => sum + item.amount, 0);
  const paidTotal = items.filter((item) => paymentsByPayable.has(item.id)).reduce((sum, item) => sum + item.amount, 0);
  const overdueCount = items.filter((item) => !paymentsByPayable.has(item.id) && item.due_on < todayLocal()).length;

  async function changeMonth(offset: number) {
    const nextMonth = shiftInvoiceMonth(`${month}-01`, offset).slice(0, 7);
    setError("");
    setBusy(true);
    try {
      if (isDemo) setData((current) => materializeDemoRecurrences(nextMonth, current));
      else {
        const { error: generationError } = await getSupabase()!.rpc("generate_recurring_payables", { p_month: `${nextMonth}-01` });
        if (generationError) throw generationError;
        await reload();
      }
      setMonth(nextMonth);
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function createPayable(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const description = String(form.get("description") ?? "").trim();
    const amount = Number(form.get("amount"));
    const due_on = String(form.get("due_on") ?? "");
    const account_id = String(form.get("account_id") ?? "");
    const category_id = String(form.get("category_id") ?? "");
    const installment_count = Number(form.get("installment_count"));
    const installment_start = Number(form.get("installment_start"));
    if (!description || !Number.isFinite(amount) || amount <= 0 || !/^\d{4}-\d{2}-\d{2}$/.test(due_on) ||
        !data.accounts.some((item) => item.id === account_id) || !expenseCategories.some((item) => item.id === category_id) ||
        !Number.isInteger(installment_count) || installment_count < 1 || installment_count > 60 ||
        !Number.isInteger(installment_start) || installment_start < 1 || installment_start > installment_count) {
      setError("Confira os dados da pendência e das parcelas.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      if (isDemo) {
        const group = installment_count > 1 ? crypto.randomUUID() : null;
        const day = Number(due_on.slice(8, 10));
        const payables: Payable[] = Array.from({ length: installment_count - installment_start + 1 }, (_, index) => ({
          id: crypto.randomUUID(), description, amount,
          due_on: safeMonthDay(shiftInvoiceMonth(`${due_on.slice(0, 7)}-01`, index), day),
          account_id, category_id, installment_group_id: group,
          installment_number: group ? installment_start + index : null,
          installment_count: group ? installment_count : null, recurring_payable_id: null,
        }));
        setData((current) => ({ ...current, payables: [...current.payables, ...payables] }));
      } else {
        const { error: saveError } = await getSupabase()!.rpc("create_payables", {
          p_description: description, p_amount: amount, p_due_on: due_on,
          p_account_id: account_id, p_category_id: category_id,
          p_installment_count: installment_count, p_installment_start: installment_start,
        });
        if (saveError) throw saveError;
        await reload();
      }
      setMonth(due_on.slice(0, 7));
      setModal(null);
      setNotice(installment_count > 1 ? "Parcelas criadas com sucesso." : "Pendência criada com sucesso.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function createRecurrence(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const description = String(form.get("description") ?? "").trim();
    const amount = Number(form.get("amount"));
    const due_day = Number(form.get("due_day"));
    const start_on = String(form.get("start_on") ?? "");
    const end_on = String(form.get("end_on") ?? "") || null;
    const account_id = String(form.get("account_id") ?? "");
    const category_id = String(form.get("category_id") ?? "");
    if (!description || !Number.isFinite(amount) || amount <= 0 || !Number.isInteger(due_day) || due_day < 1 || due_day > 31 ||
        !/^\d{4}-\d{2}-\d{2}$/.test(start_on) || (end_on !== null && (end_on < start_on || !/^\d{4}-\d{2}-\d{2}$/.test(end_on))) ||
        !data.accounts.some((item) => item.id === account_id) || !expenseCategories.some((item) => item.id === category_id)) {
      setError("Confira os dados da recorrência.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      if (isDemo) {
        const rule: RecurringPayable = { id: crypto.randomUUID(), description, amount, due_day, start_on, end_on, account_id, category_id, is_active: true };
        setData((current) => materializeDemoRecurrences(start_on.slice(0, 7), { ...current, recurring_payables: [...current.recurring_payables, rule] }));
      } else {
        const { error: saveError } = await getSupabase()!.rpc("create_recurring_payable", {
          p_description: description, p_amount: amount, p_due_day: due_day,
          p_start_on: start_on, p_end_on: end_on, p_account_id: account_id, p_category_id: category_id,
        });
        if (saveError) throw saveError;
        await reload();
      }
      setMonth(start_on.slice(0, 7));
      setModal(null);
      setNotice("Recorrência criada. A pendência aparece ao abrir cada mês.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function endRecurrence(rule: RecurringPayable) {
    if (!window.confirm(`Encerrar “${rule.description}”? As pendências não pagas desta recorrência serão removidas. Os pagamentos feitos continuarão no histórico.`)) return;
    setBusy(true);
    setError("");
    try {
      if (isDemo) setData((current) => ({
        ...current,
        recurring_payables: current.recurring_payables.filter((item) => item.id !== rule.id),
        payables: current.payables.filter((item) => item.recurring_payable_id !== rule.id ||
          current.payable_payments.some((payment) => payment.payable_id === item.id)),
      }));
      else {
        const { error: endError } = await getSupabase()!.rpc("end_recurring_payable", { p_recurring_payable_id: rule.id });
        if (endError) throw endError;
        await reload();
      }
      setNotice("Recorrência encerrada. Os pagamentos feitos foram mantidos.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function payPayable(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) return;
    const paid_on = String(new FormData(event.currentTarget).get("paid_on") ?? "");
    if (!/^\d{4}-\d{2}-\d{2}$/.test(paid_on)) { setError("Informe a data do pagamento."); return; }
    setBusy(true);
    setError("");
    try {
      if (isDemo) {
        if (data.payable_payments.some((item) => item.payable_id === selected.id)) throw new Error("Esta pendência já foi paga.");
        const payment: PayablePayment = {
          id: crypto.randomUUID(), payable_id: selected.id, account_id: selected.account_id,
          amount: selected.amount, paid_on,
        };
        setData((current) => ({ ...current, payable_payments: [...current.payable_payments, payment] }));
      } else {
        const { error: payError } = await getSupabase()!.rpc("pay_payable", {
          p_payable_id: selected.id, p_paid_on: paid_on,
        });
        if (payError) throw payError;
        await reload();
      }
      setModal(null);
      setSelected(null);
      setNotice("Pendência paga. O saldo da conta foi atualizado.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function undoPayment(item: Payable) {
    if (!window.confirm(`Desfazer o pagamento de “${item.description}”?`)) return;
    setBusy(true);
    setError("");
    try {
      if (isDemo) setData((current) => ({ ...current, payable_payments: current.payable_payments.filter((payment) => payment.payable_id !== item.id) }));
      else {
        const { error: undoError } = await getSupabase()!.rpc("undo_payable_payment", { p_payable_id: item.id });
        if (undoError) throw undoError;
        await reload();
      }
      setNotice("Pagamento desfeito.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function deletePayable(item: Payable) {
    if (paymentsByPayable.has(item.id)) { setError("Desfaça o pagamento antes de excluir a pendência."); return; }
    if (!window.confirm(`Excluir a pendência “${item.description}”?`)) return;
    setBusy(true);
    setError("");
    try {
      if (isDemo) setData((current) => ({ ...current, payables: current.payables.map((entry) =>
        entry.id === item.id && entry.recurring_payable_id ? { ...entry, is_deleted: true } : entry)
        .filter((entry) => entry.id !== item.id || !!entry.recurring_payable_id) }));
      else {
        const { error: deleteError } = await getSupabase()!.rpc("delete_payable", { p_payable_id: item.id });
        if (deleteError) throw deleteError;
        await reload();
      }
      setNotice("Pendência excluída.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function deleteRemaining(item: Payable) {
    if (!item.installment_group_id || !window.confirm(`Excluir todas as parcelas ainda não pagas de “${item.description}”?`)) return;
    setBusy(true);
    setError("");
    try {
      if (isDemo) {
        const group = item.installment_group_id;
        setData((current) => ({ ...current, payables: current.payables.filter((entry) =>
          entry.installment_group_id !== group || current.payable_payments.some((payment) => payment.payable_id === entry.id)) }));
      } else {
        const { error: deleteError } = await getSupabase()!.rpc("delete_unpaid_installments", { p_installment_group_id: item.installment_group_id });
        if (deleteError) throw deleteError;
        await reload();
      }
      setNotice("Parcelas pendentes excluídas.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  return <>
    <div className="page-heading payables-heading"><div><p className="eyebrow">COMPROMISSOS DO MÊS</p><h1>Pendências</h1><p className="muted">Veja vencimentos e registre o pagamento quando ele acontecer.</p></div><div className="heading-actions"><button className="secondary-button" onClick={onCalendar}><CalendarDays size={16} /> Calendário</button><button className="primary-button" onClick={() => { setError(""); setModal("new"); }}><Plus size={17} /> Nova pendência</button></div></div>
    {notice && <div className="notice" role="status">{notice}<button aria-label="Fechar aviso" onClick={() => setNotice("")}><X size={15} /></button></div>}
    {error && <div className="error-banner" role="alert">{error}<button aria-label="Fechar erro" onClick={() => setError("")}><X size={15} /></button></div>}
    <div className="toolbar"><div className="period-switch"><button aria-label="Mês anterior" disabled={busy} onClick={() => void changeMonth(-1)}><ChevronLeft size={17} /></button><span>{monthLabel(month)}</span><button aria-label="Próximo mês" disabled={busy} onClick={() => void changeMonth(1)}><ChevronRight size={17} /></button></div><span className="period-caption">Mês de vencimento</span></div>
    <div className="payable-stats"><div className="panel"><small>A pagar no mês</small><strong>{currency(pendingTotal)}</strong></div><div className="panel"><small>Pago</small><strong>{currency(paidTotal)}</strong></div><div className="panel"><small>Vencidas</small><strong>{overdueCount}</strong></div></div>
    <section className="panel payable-panel"><div className="section-title"><div><h2>Vencimentos</h2><p>{items.length} {items.length === 1 ? "pendência" : "pendências"} em {monthLabel(month)}</p></div><CalendarDays size={18} /></div>{items.length === 0 ? <div className="empty-state">Nenhuma pendência neste mês. Cadastre uma conta para acompanhar o próximo vencimento.</div> : <div className="payable-list">{items.map((item) => {
      const payment = paymentsByPayable.get(item.id);
      const status = payment ? "Paga" : item.due_on < todayLocal() ? "Vencida" : "Pendente";
      const account = data.accounts.find((entry) => entry.id === item.account_id);
      const category = data.categories.find((entry) => entry.id === item.category_id);
      return <div className="payable-row" key={item.id}><div className="payable-date"><strong>{item.due_on.slice(8, 10)}</strong><small>{monthLabel(item.due_on.slice(0, 7)).split(" ")[0]}</small></div><div className="payable-info"><strong>{item.description}{item.installment_count && ` · ${item.installment_number}/${item.installment_count}`}</strong><small>{category?.name ?? "Categoria removida"} · {account?.name ?? "Conta removida"}{item.recurring_payable_id ? " · Mensal" : ""}</small><span className={`payable-status ${status === "Paga" ? "paid" : status === "Vencida" ? "overdue" : "pending"}`}>{status}{payment ? ` em ${dateLabel(payment.paid_on)}` : ""}</span></div><div className="payable-side"><b>{currency(item.amount)}</b><div className="payable-actions">{payment ? <button className="text-button" disabled={busy} onClick={() => void undoPayment(item)}>Desfazer</button> : <button className="primary-button" disabled={busy} onClick={() => { setSelected(item); setError(""); setModal("pay"); }}>Marcar paga</button>}<button className="delete-button" disabled={busy || !!payment} title="Excluir somente esta pendência" aria-label={`Excluir pendência ${item.description}`} onClick={() => void deletePayable(item)}><Trash2 size={16} /></button></div>{item.installment_group_id && !payment && <button className="text-button installments-delete" disabled={busy} onClick={() => void deleteRemaining(item)}>Excluir parcelas pendentes</button>}</div></div>;
    })}</div>}</section>
    <section className="panel recurring-panel">
      <div className="section-title"><div><h2>Pagamentos recorrentes</h2><p>Contas que voltam a cada mês</p></div><button className="round-add" aria-label="Adicionar recorrência" disabled={busy} onClick={() => { setError(""); setModal("recurring"); }}><Plus size={18} /></button></div>
      {data.recurring_payables.length === 0 ? <div className="empty-state">Nenhuma recorrência cadastrada.</div> :
        <div className="recurring-list">{data.recurring_payables.map((rule) => <div className="recurring-row" key={rule.id}>
          <div className="recurring-main"><strong>{rule.description}</strong><small>Dia {rule.due_day} · {data.accounts.find((account) => account.id === rule.account_id)?.name ?? "Conta removida"} · {data.categories.find((category) => category.id === rule.category_id)?.name ?? "Categoria removida"}</small><small>Desde {rule.start_on.split("-").reverse().join("/")}{rule.end_on ? ` até ${rule.end_on.split("-").reverse().join("/")}` : " · sem data final"}</small></div>
          <b>{currency(rule.amount)}</b><button className="delete-button" disabled={busy} title="Encerrar recorrência" aria-label={`Encerrar recorrência ${rule.description}`} onClick={() => void endRecurrence(rule)}><Trash2 size={16} /></button>
        </div>)}</div>}
      <button className="secondary-button recurring-add" disabled={busy || !data.accounts.length || !expenseCategories.length} onClick={() => { setError(""); setModal("recurring"); }}><Plus size={16} /> Nova recorrência</button>
    </section>
    {modal && <div className="modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setModal(null); }}><div className="modal" role="dialog" aria-modal="true" aria-labelledby="payable-modal-title"><div className="modal-head"><div><p className="eyebrow">{modal === "new" ? "NOVO COMPROMISSO" : modal === "recurring" ? "TODO MÊS" : "PAGAMENTO"}</p><h2 id="payable-modal-title">{modal === "new" ? "Cadastrar pendência" : modal === "recurring" ? "Nova recorrência" : `Pagar ${selected?.description ?? "pendência"}`}</h2></div><button className="icon-button" aria-label="Fechar" onClick={() => setModal(null)}><X size={21} /></button></div>{error && <p className="form-error modal-error" role="alert">{error}</p>}{modal === "new" ? <form className="stack-form" onSubmit={createPayable}><label>Descrição<input name="description" maxLength={180} placeholder="Ex.: Conta de luz" required /></label><div className="form-row"><label>Valor de cada parcela<input name="amount" type="number" min="0.01" step="0.01" inputMode="decimal" required /></label><label>Primeiro vencimento<input name="due_on" type="date" defaultValue={initialDueOn ?? todayLocal()} required /></label></div><div className="form-row"><label>Conta de pagamento<select name="account_id" required>{data.accounts.map((account) => <option value={account.id} key={account.id}>{account.name}</option>)}</select></label><label>Categoria<select name="category_id" required>{expenseCategories.map((category) => <option value={category.id} key={category.id}>{category.name}</option>)}</select></label></div><div className="form-row"><label>Total de parcelas<input name="installment_count" type="number" min="1" max="60" defaultValue="1" required /></label><label>Primeira em aberto<input name="installment_start" type="number" min="1" max="60" defaultValue="1" required /></label></div><p className="form-help">Para uma conta sem parcelas, deixe os dois campos em 1. O saldo da conta muda somente quando você marcar o pagamento.</p><button className="primary-button" disabled={busy || !data.accounts.length || !expenseCategories.length}>Salvar pendência</button></form> : modal === "recurring" ? <form className="stack-form" onSubmit={createRecurrence}><label>Descrição<input name="description" maxLength={180} placeholder="Ex.: Internet" required /></label><div className="form-row"><label>Valor mensal<input name="amount" type="number" min="0.01" step="0.01" inputMode="decimal" required /></label><label>Dia do vencimento<input name="due_day" type="number" min="1" max="31" defaultValue={Number(todayLocal().slice(8, 10))} required /></label></div><div className="form-row"><label>Começa em<input name="start_on" type="date" defaultValue={todayLocal()} required /></label><label>Termina em (opcional)<input name="end_on" type="date" /></label></div><div className="form-row"><label>Conta de pagamento<select name="account_id" required>{data.accounts.map((account) => <option value={account.id} key={account.id}>{account.name}</option>)}</select></label><label>Categoria<select name="category_id" required>{expenseCategories.map((category) => <option value={category.id} key={category.id}>{category.name}</option>)}</select></label></div><p className="form-help">Quando o mês for aberto, a pendência aparece uma vez. Em meses curtos, o vencimento cai no último dia. O saldo muda somente quando você marcar o pagamento.</p><button className="primary-button" disabled={busy || !data.accounts.length || !expenseCategories.length}>Salvar recorrência</button></form> : <form className="stack-form" onSubmit={payPayable}><p className="form-help">Valor: {currency(selected?.amount ?? 0)}. O pagamento será descontado da conta vinculada.</p><label>Data do pagamento<input name="paid_on" type="date" defaultValue={todayLocal()} required /></label><button className="primary-button" disabled={busy}>Confirmar pagamento</button></form>}</div></div>}
  </>;
}
