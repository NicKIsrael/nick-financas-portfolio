"use client";

import { useState, type Dispatch, type FormEvent, type SetStateAction } from "react";
import { ArrowDownLeft, ArrowLeftRight, ArrowUpRight, Landmark, Pencil, Plus, Trash2, X } from "lucide-react";
import {
  accountBalance, currency, reserveBalance, todayLocal, totalReserved,
  type FinanceData, type Reserve, type ReserveMovement,
} from "@/lib/finance";
import { getSupabase, isDemo } from "@/lib/supabase";

type Props = {
  data: FinanceData;
  setData: Dispatch<SetStateAction<FinanceData>>;
  reload: () => Promise<void>;
};

type Modal = "reserve" | "movement" | "transfer" | null;

function errorText(cause: unknown) {
  return cause instanceof Error ? cause.message : "Não foi possível concluir a operação.";
}

export default function ReservesPage({ data, setData, reload }: Props) {
  const [modal, setModal] = useState<Modal>(null);
  const [editing, setEditing] = useState<Reserve | null>(null);
  const [selectedReserveId, setSelectedReserveId] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [movementKind, setMovementKind] = useState<"deposit" | "withdrawal">("deposit");

  const reserved = totalReserved(data.reserve_movements);
  const bankBalance = data.accounts.reduce((sum, account) =>
    sum + accountBalance(account, data.transactions, data.card_payments, data.payable_payments), 0);
  const sortedMovements = [...data.reserve_movements]
    .sort((a, b) => b.occurred_on.localeCompare(a.occurred_on) || b.id.localeCompare(a.id))
    .filter((item) => !item.transfer_group_id || item.kind === "withdrawal");

  function openReserve(reserve?: Reserve) {
    setEditing(reserve ?? null);
    setError("");
    setModal("reserve");
  }

  function openMovement(reserveId?: string) {
    setSelectedReserveId(reserveId ?? data.reserves[0]?.id ?? "");
    setMovementKind("deposit");
    setError("");
    setModal("movement");
  }

  async function saveReserve(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const name = String(new FormData(event.currentTarget).get("name") ?? "").trim();
    if (!name || name.length > 120) { setError("Informe o nome da reserva."); return; }
    setBusy(true);
    setError("");
    try {
      if (isDemo) setData((current) => ({
        ...current,
        reserves: editing
          ? current.reserves.map((item) => item.id === editing.id ? { ...item, name } : item)
          : [...current.reserves, { id: crypto.randomUUID(), name }],
      }));
      else {
        const { error: saveError } = editing
          ? await getSupabase()!.rpc("rename_reserve", { p_reserve_id: editing.id, p_name: name })
          : await getSupabase()!.rpc("create_reserve", { p_name: name });
        if (saveError) throw saveError;
        await reload();
      }
      setModal(null);
      setNotice(editing ? "Reserva atualizada." : "Reserva criada.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function deleteReserve(reserve: Reserve) {
    if (data.goals.some((item) => item.reserve_id === reserve.id)) {
      setError("Desvincule ou exclua o objetivo antes de remover esta reserva.");
      return;
    }
    if (data.reserve_movements.some((item) => item.reserve_id === reserve.id)) {
      setError("Exclua os movimentos desta reserva antes de removê-la.");
      return;
    }
    if (!window.confirm(`Excluir a reserva “${reserve.name}”?`)) return;
    setBusy(true);
    setError("");
    try {
      if (isDemo) setData((current) => ({ ...current, reserves: current.reserves.filter((item) => item.id !== reserve.id) }));
      else {
        const { error: deleteError } = await getSupabase()!.rpc("delete_empty_reserve", { p_reserve_id: reserve.id });
        if (deleteError) throw deleteError;
        await reload();
      }
      setNotice("Reserva excluída.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function saveMovement(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const reserve_id = String(form.get("reserve_id") ?? "");
    const account_id = String(form.get("account_id") ?? "");
    const amount = Number(form.get("amount"));
    const occurred_on = String(form.get("occurred_on") ?? "");
    if (!data.reserves.some((item) => item.id === reserve_id) || !data.accounts.some((item) => item.id === account_id) ||
        !Number.isFinite(amount) || amount <= 0 || !/^\d{4}-\d{2}-\d{2}$/.test(occurred_on)) {
      setError("Confira os dados do movimento.");
      return;
    }
    if (movementKind === "withdrawal" && amount > reserveBalance(reserve_id, data.reserve_movements)) {
      setError("A retirada não pode ser maior que o saldo da reserva.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      if (isDemo) {
        const movement: ReserveMovement = { id: crypto.randomUUID(), reserve_id, account_id, amount, kind: movementKind, occurred_on, transfer_group_id: null };
        setData((current) => ({ ...current, reserve_movements: [movement, ...current.reserve_movements] }));
      } else {
        const { error: saveError } = await getSupabase()!.rpc("move_reserve", {
          p_reserve_id: reserve_id, p_account_id: account_id, p_amount: amount,
          p_kind: movementKind, p_occurred_on: occurred_on,
        });
        if (saveError) throw saveError;
        await reload();
      }
      setModal(null);
      setNotice(movementKind === "deposit" ? "Aporte registrado." : "Retirada registrada.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function saveTransfer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const source_id = String(form.get("source_id") ?? "");
    const destination_id = String(form.get("destination_id") ?? "");
    const account_id = String(form.get("account_id") ?? "");
    const amount = Number(form.get("amount"));
    const occurred_on = String(form.get("occurred_on") ?? "");
    if (source_id === destination_id || !data.reserves.some((item) => item.id === source_id) ||
        !data.reserves.some((item) => item.id === destination_id) || !data.accounts.some((item) => item.id === account_id) ||
        !Number.isFinite(amount) || amount <= 0 || !/^\d{4}-\d{2}-\d{2}$/.test(occurred_on)) {
      setError("Escolha duas reservas diferentes e confira o valor.");
      return;
    }
    if (amount > reserveBalance(source_id, data.reserve_movements)) {
      setError("A transferência não pode ser maior que o saldo da reserva de origem.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      if (isDemo) {
        const group = crypto.randomUUID();
        const movements: ReserveMovement[] = [
          { id: crypto.randomUUID(), reserve_id: source_id, account_id, amount, kind: "withdrawal", occurred_on, transfer_group_id: group },
          { id: crypto.randomUUID(), reserve_id: destination_id, account_id, amount, kind: "deposit", occurred_on, transfer_group_id: group },
        ];
        setData((current) => ({ ...current, reserve_movements: [...movements, ...current.reserve_movements] }));
      } else {
        const { error: saveError } = await getSupabase()!.rpc("transfer_reserve", {
          p_source_id: source_id, p_destination_id: destination_id,
          p_account_id: account_id, p_amount: amount, p_occurred_on: occurred_on,
        });
        if (saveError) throw saveError;
        await reload();
      }
      setModal(null);
      setNotice("Transferência entre reservas concluída.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function deleteMovement(item: ReserveMovement) {
    const group = item.transfer_group_id;
    const linked = group ? data.reserve_movements.filter((entry) => entry.transfer_group_id === group) : [item];
    const deposit = linked.find((entry) => entry.kind === "deposit");
    if (deposit && reserveBalance(deposit.reserve_id, data.reserve_movements) < deposit.amount) {
      setError("Este aporte já foi usado por uma retirada ou transferência.");
      return;
    }
    if (!window.confirm(group ? "Desfazer esta transferência entre reservas?" : "Excluir este movimento da reserva?")) return;
    setBusy(true);
    setError("");
    try {
      if (isDemo) setData((current) => ({ ...current, reserve_movements: current.reserve_movements.filter((entry) =>
        group ? entry.transfer_group_id !== group : entry.id !== item.id) }));
      else {
        const { error: deleteError } = await getSupabase()!.rpc("delete_reserve_movement", { p_movement_id: item.id });
        if (deleteError) throw deleteError;
        await reload();
      }
      setNotice(group ? "Transferência desfeita." : "Movimento excluído.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  return <>
    <div className="page-heading reserves-heading"><div><p className="eyebrow">DINHEIRO SEPARADO</p><h1>Reservas</h1><p className="muted">Organize o que vocês guardaram sem contar como despesa.</p></div><button className="primary-button" onClick={() => openReserve()}><Plus size={17} /> Nova reserva</button></div>
    {notice && <div className="notice" role="status">{notice}<button aria-label="Fechar aviso" onClick={() => setNotice("")}><X size={15} /></button></div>}
    {error && <div className="error-banner" role="alert">{error}<button aria-label="Fechar erro" onClick={() => setError("")}><X size={15} /></button></div>}
    <div className="reserve-stats"><div className="panel"><small>Total reservado</small><strong>{currency(reserved)}</strong></div><div className="panel"><small>Disponível para usar</small><strong>{currency(bankBalance - reserved)}</strong></div><div className="panel"><small>Saldo nas contas</small><strong>{currency(bankBalance)}</strong></div></div>
    <div className="reserve-actions"><button className="secondary-button" disabled={!data.reserves.length || !data.accounts.length} onClick={() => openMovement()}><Plus size={16} /> Aporte ou retirada</button><button className="secondary-button" disabled={data.reserves.length < 2 || !data.accounts.length} onClick={() => { setError(""); setModal("transfer"); }}><ArrowLeftRight size={16} /> Transferir entre reservas</button></div>
    <section className="reserve-grid">{data.reserves.length ? data.reserves.map((reserve) => <div className="panel reserve-card" key={reserve.id}><div className="reserve-card-top"><div className="reserve-symbol"><Landmark size={20} /></div><div className="reserve-card-actions"><button className="icon-button" title="Editar nome" aria-label={`Editar reserva ${reserve.name}`} disabled={busy} onClick={() => openReserve(reserve)}><Pencil size={16} /></button><button className="delete-button" title="Excluir reserva vazia" aria-label={`Excluir reserva ${reserve.name}`} disabled={busy} onClick={() => void deleteReserve(reserve)}><Trash2 size={16} /></button></div></div><h2>{reserve.name}</h2><small>Saldo reservado</small><strong>{currency(reserveBalance(reserve.id, data.reserve_movements))}</strong><button className="text-button" disabled={busy || !data.accounts.length} onClick={() => openMovement(reserve.id)}>Movimentar <Plus size={14} /></button></div>) : <div className="panel empty-state">Nenhuma reserva cadastrada. Crie uma para separar dinheiro do saldo disponível.</div>}</section>
    <section className="panel reserve-history"><div className="section-title"><div><h2>Histórico de reservas</h2><p>{sortedMovements.length} {sortedMovements.length === 1 ? "movimento" : "movimentos"}</p></div></div>{sortedMovements.length ? sortedMovements.map((item) => {
      const destination = item.transfer_group_id ? data.reserve_movements.find((entry) => entry.transfer_group_id === item.transfer_group_id && entry.kind === "deposit") : null;
      const sourceName = data.reserves.find((reserve) => reserve.id === item.reserve_id)?.name ?? "Reserva removida";
      const destinationName = destination ? data.reserves.find((reserve) => reserve.id === destination.reserve_id)?.name ?? "Reserva removida" : "";
      const accountName = data.accounts.find((account) => account.id === item.account_id)?.name ?? "Conta removida";
      return <div className="reserve-history-row" key={item.id}><div className={`reserve-history-icon ${item.kind}`}>{item.transfer_group_id ? <ArrowLeftRight size={17} /> : item.kind === "deposit" ? <ArrowDownLeft size={17} /> : <ArrowUpRight size={17} />}</div><div className="reserve-history-main"><strong>{item.transfer_group_id ? `${sourceName} → ${destinationName}` : `${item.kind === "deposit" ? "Aporte" : "Retirada"} · ${sourceName}`}</strong><small>{item.occurred_on.split("-").reverse().join("/")} · {accountName}</small></div><b>{currency(item.amount)}</b><button className="delete-button" disabled={busy} aria-label={`Excluir movimento de ${currency(item.amount)}`} onClick={() => void deleteMovement(item)}><Trash2 size={16} /></button></div>;
    }) : <div className="empty-state">Nenhum movimento registrado.</div>}</section>
    {modal && <div className="modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setModal(null); }}><div className="modal" role="dialog" aria-modal="true" aria-labelledby="reserve-modal-title"><div className="modal-head"><div><p className="eyebrow">{modal === "reserve" ? "ORGANIZAÇÃO" : modal === "movement" ? "DINHEIRO SEPARADO" : "ENTRE RESERVAS"}</p><h2 id="reserve-modal-title">{modal === "reserve" ? editing ? "Editar reserva" : "Nova reserva" : modal === "movement" ? "Movimentar reserva" : "Transferir valor"}</h2></div><button className="icon-button" aria-label="Fechar" onClick={() => setModal(null)}><X size={21} /></button></div>{error && <p className="form-error modal-error" role="alert">{error}</p>}
      {modal === "reserve" ? <form className="stack-form" onSubmit={saveReserve}><label>Nome da reserva<input name="name" maxLength={120} defaultValue={editing?.name} placeholder="Ex.: Emergência" required /></label><button className="primary-button" disabled={busy}>Salvar reserva</button></form> : modal === "movement" ? <form className="stack-form" onSubmit={saveMovement}><div className="segmented"><label><input type="radio" name="kind" value="deposit" checked={movementKind === "deposit"} onChange={() => setMovementKind("deposit")} /><span>Aporte</span></label><label><input type="radio" name="kind" value="withdrawal" checked={movementKind === "withdrawal"} onChange={() => setMovementKind("withdrawal")} /><span>Retirada</span></label></div><label>Reserva<select name="reserve_id" value={selectedReserveId} onChange={(event) => setSelectedReserveId(event.target.value)} required>{data.reserves.map((reserve) => <option value={reserve.id} key={reserve.id}>{reserve.name}</option>)}</select></label><label>Conta de referência<select name="account_id" required>{data.accounts.map((account) => <option value={account.id} key={account.id}>{account.name}</option>)}</select></label><div className="form-row"><label>Valor<input name="amount" type="number" min="0.01" step="0.01" inputMode="decimal" required /></label><label>Data<input name="occurred_on" type="date" defaultValue={todayLocal()} required /></label></div><p className="form-help">{movementKind === "deposit" ? "O aporte reduz o saldo disponível, sem criar uma despesa nem alterar o saldo bancário." : "A retirada devolve o valor ao saldo disponível."}</p><button className="primary-button" disabled={busy}>Registrar {movementKind === "deposit" ? "aporte" : "retirada"}</button></form> : <form className="stack-form" onSubmit={saveTransfer}><div className="form-row"><label>De<select name="source_id" required>{data.reserves.map((reserve) => <option value={reserve.id} key={reserve.id}>{reserve.name}</option>)}</select></label><label>Para<select name="destination_id" defaultValue={data.reserves[1]?.id} required>{data.reserves.map((reserve) => <option value={reserve.id} key={reserve.id}>{reserve.name}</option>)}</select></label></div><label>Conta de referência<select name="account_id" required>{data.accounts.map((account) => <option value={account.id} key={account.id}>{account.name}</option>)}</select></label><div className="form-row"><label>Valor<input name="amount" type="number" min="0.01" step="0.01" inputMode="decimal" required /></label><label>Data<input name="occurred_on" type="date" defaultValue={todayLocal()} required /></label></div><p className="form-help">A transferência muda as duas reservas, mas preserva o total reservado.</p><button className="primary-button" disabled={busy}>Transferir</button></form>}
    </div></div>}
  </>;
}
