"use client";

import { useMemo, useState } from "react";
import { ArrowDownLeft, ArrowRight, ArrowUpRight, ChevronLeft, ChevronRight, ChartNoAxesCombined, Landmark, Target } from "lucide-react";
import { currency, monthLabel, shiftInvoiceMonth, todayLocal, totalReserved, type FinanceData } from "@/lib/finance";
import { monthReport, reportEntries, type ReportEntry } from "@/lib/reports";

type Props = { data: FinanceData; onReserves: () => void; onGoals: () => void };
type Group = { id: string; name: string; amount: number; entries: ReportEntry[] };

function groups(entries: ReportEntry[], keyFor: (item: ReportEntry) => string, nameFor: (id: string) => string): Group[] {
  const map = new Map<string, ReportEntry[]>();
  for (const item of entries) {
    const id = keyFor(item);
    map.set(id, [...(map.get(id) ?? []), item]);
  }
  return [...map.entries()].map(([id, items]) => ({
    id, name: nameFor(id), amount: items.reduce((sum, item) => sum + item.amount, 0), entries: items,
  })).sort((a, b) => b.amount - a.amount || a.name.localeCompare(b.name));
}

function DetailRows({ entries, data }: { entries: ReportEntry[]; data: FinanceData }) {
  return <div className="report-details">{[...entries].sort((a, b) => b.date.localeCompare(a.date)).map((item) => <div className="report-detail" key={item.id}>
    <div><strong>{item.description}</strong><small>{item.date.split("-").reverse().join("/")} · {item.source} · {data.accounts.find((account) => account.id === item.account_id)?.name ?? "Conta removida"}</small></div>
    <b>{currency(item.amount)}</b>
  </div>)}</div>;
}

function Breakdown({ title, subtitle, items, data, expanded, setExpanded }: {
  title: string; subtitle: string; items: Group[]; data: FinanceData;
  expanded: string | null; setExpanded: (value: string | null) => void;
}) {
  return <section className="panel report-breakdown"><div className="section-title"><div><h2>{title}</h2><p>{subtitle}</p></div></div>
    {items.length ? items.map((group) => {
      const key = `${title}:${group.id}`;
      const open = expanded === key;
      return <div className="report-group" key={group.id}><button className="report-group-button" aria-expanded={open} onClick={() => setExpanded(open ? null : key)}>
        <span><strong>{group.name}</strong><small>{group.entries.length} {group.entries.length === 1 ? "registro" : "registros"}</small></span><b>{currency(group.amount)}</b><ArrowRight size={15} className={open ? "expanded" : ""} />
      </button>{open && <DetailRows entries={group.entries} data={data} />}</div>;
    }) : <div className="empty-state">Nenhum registro neste mês.</div>}
  </section>;
}

export default function ReportsPage({ data, onReserves, onGoals }: Props) {
  const [month, setMonth] = useState(() => todayLocal().slice(0, 7));
  const [expanded, setExpanded] = useState<string | null>(null);
  const entries = useMemo(() => reportEntries(data), [data]);
  const report = monthReport(entries, month);
  const expenses = report.entries.filter((item) => item.kind === "expense");
  const categories = groups(expenses, (item) => item.category_id ?? "none",
    (id) => data.categories.find((item) => item.id === id)?.name ?? "Sem categoria");
  const accounts = groups(expenses, (item) => item.account_id,
    (id) => data.accounts.find((item) => item.id === id)?.name ?? "Conta removida");
  const cards = groups(expenses.filter((item) => item.card_id), (item) => item.card_id!,
    (id) => data.cards.find((item) => item.id === id)?.name ?? "Cartão removido");
  const end = `${month}-01`;
  const months = Array.from({ length: 6 }, (_, index) => shiftInvoiceMonth(end, index - 5).slice(0, 7));
  const history = months.map((value) => ({ month: value, ...monthReport(entries, value) }));
  const peak = Math.max(1, ...history.flatMap((item) => [item.income, item.expense]));

  function changeMonth(offset: number) {
    setMonth(shiftInvoiceMonth(`${month}-01`, offset).slice(0, 7));
    setExpanded(null);
  }

  return <>
    <div className="page-heading"><div><p className="eyebrow">ENTENDA OS NÚMEROS</p><h1>Relatórios</h1><p className="muted">Compare meses e abra os registros que formam cada total.</p></div></div>
    <div className="toolbar"><div className="period-switch"><button aria-label="Mês anterior" onClick={() => changeMonth(-1)}><ChevronLeft size={17} /></button><span>{monthLabel(month)}</span><button aria-label="Próximo mês" onClick={() => changeMonth(1)}><ChevronRight size={17} /></button></div><span className="period-caption">Data do lançamento ou pagamento</span></div>
    <div className="report-stats"><div className="panel"><ArrowDownLeft size={18} /><small>Entradas</small><strong>{currency(report.income)}</strong></div><div className="panel"><ArrowUpRight size={18} /><small>Saídas</small><strong>{currency(report.expense)}</strong></div><div className="panel"><ChartNoAxesCombined size={18} /><small>Resultado</small><strong>{currency(report.income - report.expense)}</strong></div></div>
    <p className="report-note">Compras no cartão entram no mês em que a fatura foi paga; pendências entram na data do pagamento. Aportes em reservas não são despesas.</p>
    <section className="panel report-chart"><div className="section-title"><div><h2>Últimos seis meses</h2><p>Entradas e saídas por mês</p></div></div><div className="report-chart-legend"><span><i className="legend-green" /> Entradas</span><span><i className="legend-orange" /> Saídas</span></div><div className="report-bars">{history.map((item) => <div className="report-bar-month" key={item.month}><div className="report-bar-pair"><div className="report-bar-income" title={`${monthLabel(item.month)}: entradas ${currency(item.income)}`} style={{ height: `${item.income / peak * 100}%` }} /><div className="report-bar-expense" title={`${monthLabel(item.month)}: saídas ${currency(item.expense)}`} style={{ height: `${item.expense / peak * 100}%` }} /></div><span>{monthLabel(item.month).slice(0, 3)}</span><small>{item.month.slice(2, 4)}</small></div>)}</div></section>
    <div className="report-grid"><Breakdown title="Despesas por categoria" subtitle="Toque para ver os registros" items={categories} data={data} expanded={expanded} setExpanded={setExpanded} /><Breakdown title="Saídas por conta" subtitle="Pagamentos e despesas diretas" items={accounts} data={data} expanded={expanded} setExpanded={setExpanded} /><Breakdown title="Faturas pagas por cartão" subtitle="Compras das faturas pagas neste mês" items={cards} data={data} expanded={expanded} setExpanded={setExpanded} /><section className="panel report-wealth"><div className="section-title"><div><h2>Reservas e objetivos</h2><p>Planejamento além do fluxo do mês</p></div></div><div><Landmark size={18} /><span><strong>{currency(totalReserved(data.reserve_movements))}</strong><small>em {data.reserves.length} {data.reserves.length === 1 ? "reserva" : "reservas"}</small></span></div><div><Target size={18} /><span><strong>{data.goals.length}</strong><small>{data.goals.length === 1 ? "objetivo" : "objetivos"}</small></span></div><div className="report-wealth-links"><button className="text-button" onClick={onReserves}>Ver reservas <ArrowRight size={14} /></button><button className="text-button" onClick={onGoals}>Ver objetivos <ArrowRight size={14} /></button></div></section></div>
  </>;
}
