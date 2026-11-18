"use client";

import { useState, type Dispatch, type FormEvent, type SetStateAction } from "react";
import { ChevronLeft, ChevronRight, CreditCard as CardIcon, Plus, Trash2, X } from "lucide-react";
import {
  cardUsedLimit, currency, dateLabel, invoiceMonthForDate, monthLabel, safeMonthDay,
  shiftInvoiceMonth, todayLocal, type CardCharge, type CardPayment, type CreditCard,
  type FinanceData,
} from "@/lib/finance";
import { getSupabase, isDemo } from "@/lib/supabase";

type Props = {
  data: FinanceData;
  setData: Dispatch<SetStateAction<FinanceData>>;
  reload: () => Promise<void>;
};

function errorText(cause: unknown) {
  return cause instanceof Error ? cause.message : "Não foi possível concluir a operação.";
}

export default function CardsPage({ data, setData, reload }: Props) {
  const [month, setMonth] = useState(() => todayLocal().slice(0, 7));
  const [modal, setModal] = useState<"card" | "purchase" | null>(null);
  const [selectedCardId, setSelectedCardId] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const invoiceMonth = `${month}-01`;
  const expenseCategories = data.categories.filter((item) => item.kind === "expense");

  function openPurchase(cardId?: string) {
    setSelectedCardId(cardId ?? data.cards[0]?.id ?? "");
    setError("");
    setModal("purchase");
  }

  async function saveCard(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const name = String(form.get("name") ?? "").trim();
    const payment_account_id = String(form.get("payment_account_id") ?? "");
    const credit_limit = Number(form.get("credit_limit"));
    const closing_day = Number(form.get("closing_day"));
    const due_day = Number(form.get("due_day"));
    if (!name || !data.accounts.some((item) => item.id === payment_account_id) ||
        !Number.isFinite(credit_limit) || credit_limit <= 0 ||
        !Number.isInteger(closing_day) || closing_day < 1 || closing_day > 31 ||
        !Number.isInteger(due_day) || due_day < 1 || due_day > 31) {
      setError("Confira os dados do cartão.");
      return;
    }
    if (data.cards.some((item) => item.name.toLocaleLowerCase("pt-BR") === name.toLocaleLowerCase("pt-BR"))) {
      setError("Já existe um cartão com esse nome.");
      return;
    }
    const card: CreditCard = {
      id: crypto.randomUUID(), name, payment_account_id, credit_limit,
      spending_limit: credit_limit, closing_day, due_day,
    };
    setBusy(true);
    setError("");
    try {
      if (isDemo) setData((current) => ({ ...current, cards: [...current.cards, card] }));
      else {
        const { error: saveError } = await getSupabase()!.from("credit_cards").insert(card);
        if (saveError) throw saveError;
        await reload();
      }
      setModal(null);
      setNotice("Cartão criado com sucesso.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function savePurchase(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const card_id = String(form.get("card_id") ?? "");
    const category_id = String(form.get("category_id") ?? "");
    const description = String(form.get("description") ?? "").trim();
    const amount = Number(form.get("amount"));
    const purchased_on = String(form.get("purchased_on") ?? "");
    const installments = Number(form.get("installments"));
    const card = data.cards.find((item) => item.id === card_id);
    if (!card || !expenseCategories.some((item) => item.id === category_id) || !description ||
        !Number.isFinite(amount) || amount <= 0 || !Number.isInteger(installments) ||
        installments < 1 || installments > 36 || Math.round(amount * 100) < installments ||
        !/^\d{4}-\d{2}-\d{2}$/.test(purchased_on)) {
      setError("Confira os dados da compra. Cada parcela precisa ter pelo menos R$ 0,01.");
      return;
    }
    if (cardUsedLimit(card_id, data.card_charges, data.card_payments) + amount > card.spending_limit + 0.001) {
      setError("A compra ultrapassa o limite disponível do cartão.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      if (isDemo) {
        const group = crypto.randomUUID();
        const cents = Math.round(amount * 100);
        const base = Math.floor(cents / installments);
        const extra = cents % installments;
        const firstInvoice = invoiceMonthForDate(purchased_on, card.closing_day, card.due_day);
        const charges: CardCharge[] = Array.from({ length: installments }, (_, index) => ({
          id: crypto.randomUUID(), card_id, category_id, description,
          amount: (base + (index < extra ? 1 : 0)) / 100, purchased_on,
          invoice_month: shiftInvoiceMonth(firstInvoice, index), purchase_group_id: group,
          installment_number: index + 1, installment_count: installments,
        }));
        if (charges.some((charge) => data.card_payments.some((payment) => payment.card_id === card_id && payment.invoice_month === charge.invoice_month)))
          throw new Error("Uma das faturas desta compra já foi paga.");
        setData((current) => ({ ...current, card_charges: [...current.card_charges, ...charges] }));
      } else {
        const { error: saveError } = await getSupabase()!.rpc("create_card_purchase", {
          p_card_id: card_id, p_category_id: category_id, p_description: description,
          p_amount: amount, p_purchased_on: purchased_on, p_installments: installments,
        });
        if (saveError) throw saveError;
        await reload();
      }
      setModal(null);
      setNotice("Compra registrada na fatura.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function payInvoice(card: CreditCard) {
    const charges = data.card_charges.filter((item) => item.card_id === card.id && item.invoice_month === invoiceMonth);
    const total = charges.reduce((sum, item) => sum + item.amount, 0);
    if (total <= 0 || data.card_payments.some((item) => item.card_id === card.id && item.invoice_month === invoiceMonth)) return;
    if (!window.confirm(`Pagar a fatura de ${card.name} no valor de ${currency(total)} pela conta vinculada?`)) return;
    setBusy(true);
    setError("");
    try {
      if (isDemo) {
        const payment: CardPayment = {
          id: crypto.randomUUID(), card_id: card.id, invoice_month: invoiceMonth,
          account_id: card.payment_account_id, amount: Math.round(total * 100) / 100, paid_on: todayLocal(),
        };
        setData((current) => ({ ...current, card_payments: [...current.card_payments, payment] }));
      } else {
        const { error: payError } = await getSupabase()!.rpc("pay_card_invoice", {
          p_card_id: card.id, p_invoice_month: invoiceMonth, p_paid_on: todayLocal(),
        });
        if (payError) throw payError;
        await reload();
      }
      setNotice("Fatura paga. O saldo da conta foi atualizado.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function undoPayment(card: CreditCard) {
    const payment = data.card_payments.find((item) => item.card_id === card.id && item.invoice_month === invoiceMonth);
    if (!payment || !window.confirm(`Desfazer o pagamento da fatura de ${card.name}?`)) return;
    setBusy(true);
    setError("");
    try {
      if (isDemo) {
        if (cardUsedLimit(card.id, data.card_charges, data.card_payments) + payment.amount > card.spending_limit + 0.001)
          throw new Error("Não é possível desfazer: o limite ficaria ultrapassado.");
        setData((current) => ({ ...current, card_payments: current.card_payments.filter((item) => item.id !== payment.id) }));
      } else {
        const { error: undoError } = await getSupabase()!.rpc("undo_card_invoice_payment", {
          p_card_id: card.id, p_invoice_month: invoiceMonth,
        });
        if (undoError) throw undoError;
        await reload();
      }
      setNotice("Pagamento desfeito.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function deletePurchase(charge: CardCharge) {
    if (!window.confirm(`Excluir a compra “${charge.description}” e todas as suas parcelas?`)) return;
    setBusy(true);
    setError("");
    try {
      if (isDemo) {
        const group = data.card_charges.filter((item) => item.purchase_group_id === charge.purchase_group_id);
        if (group.some((item) => data.card_payments.some((payment) => payment.card_id === item.card_id && payment.invoice_month === item.invoice_month)))
          throw new Error("Desfaça o pagamento da fatura antes de excluir a compra.");
        setData((current) => ({ ...current, card_charges: current.card_charges.filter((item) => item.purchase_group_id !== charge.purchase_group_id) }));
      } else {
        const { error: deleteError } = await getSupabase()!.rpc("delete_card_purchase", { p_purchase_group_id: charge.purchase_group_id });
        if (deleteError) throw deleteError;
        await reload();
      }
      setNotice("Compra e parcelas excluídas.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function deleteCard(card: CreditCard) {
    if (!window.confirm(`Excluir o cartão “${card.name}”? Só é possível excluir cartões sem compras ou pagamentos.`)) return;
    setBusy(true);
    setError("");
    try {
      if (isDemo) {
        if (data.card_charges.some((item) => item.card_id === card.id) || data.card_payments.some((item) => item.card_id === card.id))
          throw new Error("Este cartão ainda possui compras ou pagamentos.");
        setData((current) => ({ ...current, cards: current.cards.filter((item) => item.id !== card.id) }));
      } else {
        const { error: deleteError } = await getSupabase()!.from("credit_cards").delete().eq("id", card.id).select("id").single();
        if (deleteError) throw deleteError;
        await reload();
      }
      setNotice("Cartão excluído.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  return <>
    <div className="page-heading cards-page-heading"><div><p className="eyebrow">CRÉDITO COMPARTILHADO</p><h1>Cartões e faturas</h1><p className="muted">Acompanhe compras, parcelas, limite e pagamento das faturas.</p></div><button className="primary-button" onClick={() => { setError(""); setModal("card"); }}><Plus size={17} /> Novo cartão</button></div>
    {notice && <div className="notice" role="status">{notice}<button aria-label="Fechar aviso" onClick={() => setNotice("")}><X size={15} /></button></div>}
    {error && <div className="error-banner" role="alert">{error}<button aria-label="Fechar erro" onClick={() => setError("")}><X size={15} /></button></div>}
    <div className="toolbar"><div className="period-switch"><button aria-label="Fatura anterior" onClick={() => setMonth(shiftInvoiceMonth(invoiceMonth, -1).slice(0, 7))}><ChevronLeft size={17} /></button><span>{monthLabel(month)}</span><button aria-label="Próxima fatura" onClick={() => setMonth(shiftInvoiceMonth(invoiceMonth, 1).slice(0, 7))}><ChevronRight size={17} /></button></div><span className="period-caption">Mês de vencimento da fatura</span></div>
    {data.cards.length === 0 ? <section className="panel empty-state">Cadastre um cartão para acompanhar suas faturas. Antes, crie a conta que será usada para pagá-lo.</section> : <div className="cards-grid">{data.cards.map((card) => {
      const charges = data.card_charges.filter((item) => item.card_id === card.id && item.invoice_month === invoiceMonth);
      const payment = data.card_payments.find((item) => item.card_id === card.id && item.invoice_month === invoiceMonth);
      const total = Math.round(charges.reduce((sum, item) => sum + item.amount, 0) * 100) / 100;
      const used = cardUsedLimit(card.id, data.card_charges, data.card_payments);
      const account = data.accounts.find((item) => item.id === card.payment_account_id);
      return <section className="panel card-panel" key={card.id}>
        <div className="card-heading"><div className="card-symbol"><CardIcon size={22} /></div><div><h2>{card.name}</h2><p>Pagamento por {account?.name ?? "conta removida"}</p></div><button className="delete-button" aria-label={`Excluir cartão ${card.name}`} title="Excluir cartão vazio" disabled={busy} onClick={() => void deleteCard(card)}><Trash2 size={16} /></button></div>
        <div className="card-limits"><div><small>Limite disponível</small><strong>{currency(Math.max(0, card.spending_limit - used))}</strong></div><div><small>Limite total</small><strong>{currency(card.spending_limit)}</strong></div></div>
        <div className="invoice-box"><div><small>Fatura de {monthLabel(month)}</small><strong>{currency(total)}</strong></div><span className={payment ? "invoice-status paid" : total ? "invoice-status open" : "invoice-status"}>{payment ? "Paga" : total ? "Em aberto" : "Sem compras"}</span></div>
        <p className="card-dates">Fecha dia {card.closing_day} · Vence em {dateLabel(safeMonthDay(invoiceMonth, card.due_day))}</p>
        {charges.length > 0 && <div className="card-charge-list">{charges.map((charge) => <div className="card-charge" key={charge.id}><div><strong>{charge.description}</strong><small>{charge.installment_count > 1 ? `Parcela ${charge.installment_number}/${charge.installment_count} · ` : ""}{dateLabel(charge.purchased_on)}</small></div><b>{currency(charge.amount)}</b><button className="delete-button" aria-label={`Excluir compra ${charge.description}`} title="Excluir compra e parcelas" disabled={busy} onClick={() => void deletePurchase(charge)}><Trash2 size={15} /></button></div>)}</div>}
        {payment && <p className="payment-note">Paga em {dateLabel(payment.paid_on)} pela conta {data.accounts.find((item) => item.id === payment.account_id)?.name ?? "removida"}.</p>}
        <div className="card-actions"><button className="secondary-button" disabled={busy || !expenseCategories.length} onClick={() => openPurchase(card.id)}><Plus size={15} /> Registrar compra</button>{total > 0 && (payment ? <button className="text-button" disabled={busy} onClick={() => void undoPayment(card)}>Desfazer pagamento</button> : <button className="primary-button" disabled={busy} onClick={() => void payInvoice(card)}>Pagar fatura</button>)}</div>
      </section>;
    })}</div>}
    {modal && <div className="modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setModal(null); }}><div className="modal" role="dialog" aria-modal="true" aria-labelledby="card-modal-title"><div className="modal-head"><div><p className="eyebrow">{modal === "card" ? "NOVO CARTÃO" : "NOVA COMPRA"}</p><h2 id="card-modal-title">{modal === "card" ? "Cadastrar cartão" : "Registrar compra no cartão"}</h2></div><button className="icon-button" aria-label="Fechar" onClick={() => setModal(null)}><X size={21} /></button></div>{error && <p className="form-error modal-error" role="alert">{error}</p>}{modal === "card" ? <form className="stack-form" onSubmit={saveCard}><label>Nome<input name="name" maxLength={120} placeholder="Ex.: Cartão principal" required /></label><label>Conta para pagar a fatura<select name="payment_account_id" required>{data.accounts.map((account) => <option value={account.id} key={account.id}>{account.name}</option>)}</select></label><label>Limite total<input name="credit_limit" type="number" min="0.01" step="0.01" inputMode="decimal" required /></label><div className="form-row"><label>Dia do fechamento<input name="closing_day" type="number" min="1" max="31" required /></label><label>Dia do vencimento<input name="due_day" type="number" min="1" max="31" required /></label></div><button className="primary-button" disabled={busy || !data.accounts.length}>Salvar cartão</button></form> : <form className="stack-form" onSubmit={savePurchase}><label>Cartão<select name="card_id" defaultValue={selectedCardId} required>{data.cards.map((card) => <option value={card.id} key={card.id}>{card.name}</option>)}</select></label><label>Descrição<input name="description" maxLength={180} placeholder="Ex.: Compra do mês" required /></label><div className="form-row"><label>Valor total<input name="amount" type="number" min="0.01" step="0.01" inputMode="decimal" required /></label><label>Parcelas<input name="installments" type="number" min="1" max="36" defaultValue="1" required /></label></div><div className="form-row"><label>Data da compra<input name="purchased_on" type="date" defaultValue={todayLocal()} required /></label><label>Categoria<select name="category_id" required>{expenseCategories.map((category) => <option value={category.id} key={category.id}>{category.name}</option>)}</select></label></div><p className="form-help">A compra entra na fatura. O saldo da conta muda somente quando ela é paga.</p><button className="primary-button" disabled={busy || !expenseCategories.length}>Salvar compra</button></form>}</div></div>}
  </>;
}
