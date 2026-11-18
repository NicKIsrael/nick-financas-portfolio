"use client";

import { useEffect, useRef, useState, type Dispatch, type SetStateAction } from "react";
import { ArrowDownLeft, ArrowUpRight, CalendarDays, Info, Target, TriangleAlert } from "lucide-react";
import {
  currency, materializeDemoRecurrences, monthLabel, shiftInvoiceMonth, todayLocal, type FinanceData,
} from "@/lib/finance";
import { buildPlanning } from "@/lib/planning";
import { getSupabase, isDemo } from "@/lib/supabase";

type Props = {
  data: FinanceData;
  setData: Dispatch<SetStateAction<FinanceData>>;
  reload: () => Promise<void>;
  onAgenda: () => void;
};

export default function PlanningPage({ data, setData, reload, onAgenda }: Props) {
  const requested = useRef(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const today = todayLocal();
  const projection = buildPlanning(data, today);
  const riskMonths = projection.months.filter((item) => item.ending < 0).length;

  useEffect(() => {
    if (requested.current) return;
    requested.current = true;
    const month = todayLocal().slice(0, 7);
    async function prepare() {
      try {
        if (isDemo) {
          setData((current) => {
            let next = current;
            for (let index = 1; index <= 6; index++)
              next = materializeDemoRecurrences(shiftInvoiceMonth(`${month}-01`, index).slice(0, 7), next);
            return next;
          });
        } else {
          const supabase = getSupabase()!;
          for (let index = 1; index <= 6; index++) {
            const p_month = shiftInvoiceMonth(`${month}-01`, index);
            const { error: generationError } = await supabase.rpc("generate_recurring_payables", { p_month });
            if (generationError) throw generationError;
          }
          await reload();
        }
      } catch (cause) { setError(cause instanceof Error ? cause.message : "Não foi possível carregar todas as recorrências."); }
      finally { setLoading(false); }
    }
    void prepare();
  }, [reload, setData]);

  return <>
    <div className="page-heading"><div><p className="eyebrow">PRÓXIMOS SEIS MESES</p><h1>Planejamento</h1><p className="muted">Uma previsão para acompanhar o saldo disponível de vocês.</p></div></div>
    {error && <div className="error-banner" role="alert">{error}</div>}
    {loading && <p className="form-help" role="status">Preparando os compromissos futuros...</p>}
    {!loading && <>
      <div className="planning-summary"><div className="panel"><small>SALDO DISPONÍVEL HOJE</small><strong>{currency(projection.openingBalance)}</strong></div><div className="panel"><small>MÉDIA DE ENTRADAS</small><strong>{currency(projection.averageIncome)}</strong><span>por mês</span></div><div className="panel"><small>MÉDIA DE DESPESAS DIRETAS</small><strong>{currency(projection.averageExpense)}</strong><span>por mês</span></div><div className="panel"><small>SALDO PREVISTO EM SEIS MESES</small><strong className={projection.months.at(-1)!.ending < 0 ? "planning-negative" : ""}>{currency(projection.months.at(-1)!.ending)}</strong></div></div>
      {riskMonths > 0 && <div className="planning-warning"><TriangleAlert size={19} /><div><strong>{riskMonths} {riskMonths === 1 ? "mês" : "meses"} com saldo previsto negativo</strong><p>Revise os compromissos ou os aportes dos meses destacados.</p></div></div>}
      <div className="planning-list">{projection.months.map((item) => <section className={`panel planning-month ${item.ending < 0 ? "risk" : ""}`} key={item.month}>
        <div className="planning-month-head"><div><CalendarDays size={18} /><h2>{monthLabel(item.month)}</h2></div><span>{item.ending < 0 ? "Atenção ao saldo" : "Saldo previsto positivo"}</span></div>
        <div className="planning-flow"><div><small>Saldo inicial</small><strong>{currency(item.opening)}</strong></div><div><small><ArrowDownLeft size={13} /> Entradas</small><strong className="planning-income">+ {currency(item.income)}</strong></div><div><small><ArrowUpRight size={13} /> Despesas diretas</small><strong>- {currency(item.expenses)}</strong></div><div><small><CalendarDays size={13} /> Pendências e faturas</small><strong>- {currency(item.commitments)}</strong></div><div><small><Target size={13} /> Aportes planejados</small><strong>- {currency(item.contributions)}</strong></div></div>
        <div className="planning-ending"><span>Saldo ao final do mês</span><strong>{currency(item.ending)}</strong></div>
      </section>)}</div>
      <div className="panel planning-explanation"><Info size={18} /><div><strong>Como calculamos</strong><p>Entradas e despesas diretas usam a média dos últimos seis meses completos com movimentação. Se já houver lançamento no mês futuro, usamos o valor lançado para aquele tipo. Somamos pendências e faturas ainda não pagas e subtraímos os aportes mensais planejados dos objetivos. Valores futuros são estimativas e não alteram as contas.</p><p>{projection.historyMonths === 0 ? "Ainda não há histórico de meses completos; as médias são R$ 0 até vocês registrarem movimentações." : `Médias calculadas com ${projection.historyMonths} ${projection.historyMonths === 1 ? "mês" : "meses"} com movimentação.`}</p><button className="text-button" onClick={onAgenda}>Ver compromissos na Agenda</button></div></div>
    </>}
  </>;
}
