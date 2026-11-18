"use client";

import { useState, type Dispatch, type FormEvent, type SetStateAction } from "react";
import { Pencil, Plus, Star, Target, Trash2, X } from "lucide-react";
import { currency, goalForecast, type FinanceData, type Goal } from "@/lib/finance";
import { getSupabase, isDemo } from "@/lib/supabase";

type Props = {
  data: FinanceData;
  setData: Dispatch<SetStateAction<FinanceData>>;
  reload: () => Promise<void>;
  onReserves: () => void;
};

function errorText(cause: unknown) {
  return cause instanceof Error ? cause.message : "Não foi possível concluir a operação.";
}

export default function GoalsPage({ data, setData, reload, onReserves }: Props) {
  const [editing, setEditing] = useState<Goal | null>(null);
  const [modal, setModal] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const goals = [...data.goals].sort((a, b) => Number(b.is_primary) - Number(a.is_primary) ||
    (a.target_on ?? "9999-12-31").localeCompare(b.target_on ?? "9999-12-31") || a.name.localeCompare(b.name));

  function openGoal(goal?: Goal) {
    setEditing(goal ?? null);
    setError("");
    setModal(true);
  }

  async function saveGoal(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const name = String(form.get("name") ?? "").trim();
    const target_amount = Number(form.get("target_amount"));
    const planned_monthly_contribution = Number(form.get("planned_monthly_contribution"));
    const target_on = String(form.get("target_on") ?? "") || null;
    const reserve_id = String(form.get("reserve_id") ?? "") || null;
    if (!name || name.length > 120 || !Number.isFinite(target_amount) || target_amount <= 0 ||
        !Number.isFinite(planned_monthly_contribution) || planned_monthly_contribution < 0 ||
        (target_on !== null && !/^\d{4}-\d{2}-\d{2}$/.test(target_on)) ||
        (reserve_id !== null && !data.reserves.some((item) => item.id === reserve_id))) {
      setError("Confira nome, valor alvo, prazo, aporte mensal e reserva.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      if (isDemo) {
        const goal: Goal = {
          id: editing?.id ?? crypto.randomUUID(), name, target_amount, target_on,
          planned_monthly_contribution, reserve_id, is_primary: editing?.is_primary ?? data.goals.length === 0,
        };
        setData((current) => ({ ...current, goals: editing
          ? current.goals.map((item) => item.id === editing.id ? goal : item)
          : [...current.goals, goal] }));
      } else {
        const { error: saveError } = await getSupabase()!.rpc("save_goal", {
          p_goal_id: editing?.id ?? null, p_name: name, p_target_amount: target_amount,
          p_target_on: target_on, p_planned_monthly_contribution: planned_monthly_contribution,
          p_reserve_id: reserve_id,
        });
        if (saveError) throw saveError;
        await reload();
      }
      setModal(false);
      setNotice(editing ? "Objetivo atualizado." : "Objetivo criado.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function makePrimary(goal: Goal) {
    if (goal.is_primary) return;
    setBusy(true);
    setError("");
    try {
      if (isDemo) setData((current) => ({ ...current, goals: current.goals.map((item) =>
        ({ ...item, is_primary: item.id === goal.id })) }));
      else {
        const { error: saveError } = await getSupabase()!.rpc("set_primary_goal", { p_goal_id: goal.id });
        if (saveError) throw saveError;
        await reload();
      }
      setNotice("Objetivo principal atualizado.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  async function deleteGoal(goal: Goal) {
    if (!window.confirm(`Excluir o objetivo “${goal.name}”? A reserva vinculada será mantida.`)) return;
    setBusy(true);
    setError("");
    try {
      if (isDemo) setData((current) => {
        const remaining = current.goals.filter((item) => item.id !== goal.id);
        if (goal.is_primary && remaining.length) remaining[0] = { ...remaining[0], is_primary: true };
        return { ...current, goals: remaining };
      });
      else {
        const { error: deleteError } = await getSupabase()!.rpc("delete_goal", { p_goal_id: goal.id });
        if (deleteError) throw deleteError;
        await reload();
      }
      setNotice("Objetivo excluído. A reserva foi mantida.");
    } catch (cause) { setError(errorText(cause)); }
    finally { setBusy(false); }
  }

  return <>
    <div className="page-heading goals-heading"><div><p className="eyebrow">PLANOS DO CASAL</p><h1>Objetivos</h1><p className="muted">Acompanhe o progresso do que vocês querem conquistar.</p></div><button className="primary-button" onClick={() => openGoal()}><Plus size={17} /> Novo objetivo</button></div>
    {notice && <div className="notice" role="status">{notice}<button aria-label="Fechar aviso" onClick={() => setNotice("")}><X size={15} /></button></div>}
    {error && <div className="error-banner" role="alert">{error}<button aria-label="Fechar erro" onClick={() => setError("")}><X size={15} /></button></div>}
    <p className="goals-intro">Um objetivo pode acompanhar uma reserva. A meta e o aporte planejado não movimentam dinheiro; o progresso muda quando vocês fazem um aporte na reserva vinculada.</p>
    <div className="goals-grid">{goals.length ? goals.map((goal) => {
      const forecast = goalForecast(goal, data.reserve_movements);
      const reserve = data.reserves.find((item) => item.id === goal.reserve_id);
      return <section className="panel goal-card" key={goal.id}>
        <div className="goal-card-head"><div className="goal-symbol"><Target size={20} /></div><div className="goal-card-actions"><button className="icon-button" title="Editar objetivo" aria-label={`Editar objetivo ${goal.name}`} disabled={busy} onClick={() => openGoal(goal)}><Pencil size={16} /></button><button className="delete-button" title="Excluir objetivo" aria-label={`Excluir objetivo ${goal.name}`} disabled={busy} onClick={() => void deleteGoal(goal)}><Trash2 size={16} /></button></div></div>
        <div className="goal-title"><h2>{goal.name}</h2>{goal.is_primary && <span><Star size={12} /> Principal</span>}</div>
        <p className="goal-reserve">{reserve ? `Reserva: ${reserve.name}` : "Sem reserva vinculada"}{goal.target_on ? ` · Prazo: ${goal.target_on.split("-").reverse().join("/")}` : ""}</p>
        <div className="goal-amounts"><strong>{currency(forecast.current)}</strong><small>de {currency(goal.target_amount)}</small></div>
        <div className="goal-progress" role="progressbar" aria-valuenow={Math.round(forecast.percentage)} aria-valuemin={0} aria-valuemax={100} aria-label={`Progresso de ${goal.name}`}><span style={{ width: `${forecast.percentage}%` }} /></div>
        <div className="goal-progress-label"><span>{Math.round(forecast.percentage)}% concluído</span><span>Faltam {currency(forecast.remaining)}</span></div>
        <p className="goal-forecast">{forecast.months === 0 ? "Objetivo concluído" : forecast.months === null ? "Defina um aporte mensal para obter previsão" : `Previsão: ${forecast.months} ${forecast.months === 1 ? "mês" : "meses"} (${forecast.source})`}</p>
        <small className="goal-planned">{goal.planned_monthly_contribution > 0 ? `Aporte planejado: ${currency(goal.planned_monthly_contribution)} por mês` : `Média real dos aportes: ${currency(forecast.average)} por mês`}</small>
        <div className="goal-card-footer">{!goal.is_primary && <button className="text-button" disabled={busy} onClick={() => void makePrimary(goal)}><Star size={14} /> Tornar principal</button>}{reserve && <button className="text-button" onClick={onReserves}>Ver reserva</button>}</div>
      </section>;
    }) : <div className="panel empty-state goals-empty">Nenhum objetivo cadastrado. Comece com uma meta que faça sentido para vocês.</div>}</div>
    {modal && <div className="modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setModal(false); }}><div className="modal" role="dialog" aria-modal="true" aria-labelledby="goal-modal-title"><div className="modal-head"><div><p className="eyebrow">PLANOS DO CASAL</p><h2 id="goal-modal-title">{editing ? "Editar objetivo" : "Novo objetivo"}</h2></div><button className="icon-button" aria-label="Fechar" onClick={() => setModal(false)}><X size={21} /></button></div>{error && <p className="form-error modal-error" role="alert">{error}</p>}<form className="stack-form" onSubmit={saveGoal}><label>Nome<input name="name" maxLength={120} defaultValue={editing?.name} placeholder="Ex.: Viagem" required /></label><div className="form-row"><label>Valor alvo<input name="target_amount" type="number" min="0.01" step="0.01" inputMode="decimal" defaultValue={editing?.target_amount} required /></label><label>Prazo (opcional)<input name="target_on" type="date" defaultValue={editing?.target_on ?? ""} /></label></div><label>Aporte mensal planejado<input name="planned_monthly_contribution" type="number" min="0" step="0.01" inputMode="decimal" defaultValue={editing?.planned_monthly_contribution ?? 0} required /></label><label>Reserva vinculada<select name="reserve_id" defaultValue={editing?.reserve_id ?? ""}><option value="">Nenhuma por enquanto</option>{data.reserves.map((reserve) => <option value={reserve.id} key={reserve.id}>{reserve.name}</option>)}</select></label><p className="form-help">Se o aporte planejado ficar em zero, a previsão usa a média dos meses em que houve aporte na reserva.</p><button className="primary-button" disabled={busy}>Salvar objetivo</button></form></div></div>}
  </>;
}
