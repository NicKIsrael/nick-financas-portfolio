"use client";

import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import {
  ArrowDownLeft, ArrowLeftRight, ArrowRight, ArrowUpRight, CalendarDays, ChartNoAxesCombined, ChartSpline,
  ChevronLeft, ChevronRight, CircleHelp, CreditCard, Download, House, Landmark,
  LayoutGrid, LogOut, Menu, Pencil, Plus, Search, ShieldCheck, Target,
  Trash2, Wallet, X,
} from "lucide-react";
import type { User } from "@supabase/supabase-js";
import {
  accountBalance, currency, dateLabel, demoData, goalForecast, monthLabel, todayLocal, totalReserved,
  type Account, type CardPayment, type Category, type FinanceData, type PayablePayment, type Transaction, type TransactionKind,
} from "@/lib/finance";
import { getSupabase, initialAuthRedirectType, isDemo } from "@/lib/supabase";
import CardsPage from "@/components/CardsPage";
import PayablesPage from "@/components/PayablesPage";
import ReservesPage from "@/components/ReservesPage";
import GoalsPage from "@/components/GoalsPage";
import ReportsPage from "@/components/ReportsPage";
import CalendarPage from "@/components/CalendarPage";
import PlanningPage from "@/components/PlanningPage";
import { collectFinanceExport, downloadFinanceExport } from "@/lib/export-data";

type Page = "overview" | "transactions" | "accounts" | "cards" | "payables" | "reserves" | "goals" | "reports" | "calendar" | "planning" | "more";
type Modal = "transaction" | "transfer" | "account" | "category" | null;

const emptyData: FinanceData = { accounts: [], categories: [], transactions: [], cards: [], card_charges: [], card_payments: [], payables: [], payable_payments: [], recurring_payables: [], reserves: [], reserve_movements: [], goals: [] };
const accountNames: Record<Account["type"], string> = {
  checking: "Conta corrente", savings: "Poupança", cash: "Dinheiro", investment: "Investimento",
};

function shiftMonth(month: string, delta: number) {
  const [year, number] = month.split("-").map(Number);
  const next = new Date(Date.UTC(year, number - 1 + delta, 1));
  return `${next.getUTCFullYear()}-${String(next.getUTCMonth() + 1).padStart(2, "0")}`;
}

export default function FinanceApp() {
  const [page, setPage] = useState<Page>("overview");
  const [month, setMonth] = useState(() => todayLocal().slice(0, 7));
  const [data, setData] = useState<FinanceData>(isDemo ? demoData : emptyData);
  const [user, setUser] = useState<User | null>(null);
  const [checkingAuth, setCheckingAuth] = useState(!isDemo && !!getSupabase());
  const [accessDenied, setAccessDenied] = useState(false);
  const [settingPassword, setSettingPassword] = useState(
    initialAuthRedirectType === "invite" || initialAuthRedirectType === "recovery",
  );
  const [busy, setBusy] = useState(false);
  const [modal, setModal] = useState<Modal>(null);
  const [editingTransaction, setEditingTransaction] = useState<Transaction | null>(null);
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [mobileMenu, setMobileMenu] = useState(false);
  const [calendarDueOn, setCalendarDueOn] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState("");

  const loadData = useCallback(async () => {
    const supabase = getSupabase();
    if (!supabase || !user) return;
    try {
      const access = await supabase.from("allowed_users").select("user_id").eq("user_id", user.id).maybeSingle();
      if (access.error) throw access.error;
      setBusy(true);
      setError("");
      if (!access.data) {
        setAccessDenied(true);
        setData(emptyData);
        return;
      }
      setAccessDenied(false);
      const { error: generationError } = await supabase.rpc("generate_recurring_payables", { p_month: todayLocal().slice(0, 7) + "-01" });
      if (generationError) throw generationError;
      const [accounts, categories, transactions, cards, charges, payments, payables, payablePayments, recurrences, reserves, reserveMovements, goals] = await Promise.all([
        supabase.from("accounts").select("id,name,type,initial_balance").order("name"),
        supabase.from("categories").select("id,name,kind").order("name"),
        supabase.from("transactions").select("id,account_id,destination_account_id,category_id,kind,description,amount,occurred_on,created_by").order("occurred_on", { ascending: false }),
        supabase.from("credit_cards").select("id,name,payment_account_id,credit_limit,spending_limit,closing_day,due_day").order("name"),
        supabase.from("card_charges").select("id,card_id,category_id,description,amount,purchased_on,invoice_month,purchase_group_id,installment_number,installment_count").order("purchased_on", { ascending: false }),
        supabase.from("card_payments").select("id,card_id,invoice_month,account_id,amount,paid_on").order("paid_on", { ascending: false }),
        supabase.from("payables").select("id,description,amount,due_on,account_id,category_id,installment_group_id,installment_number,installment_count,recurring_payable_id").eq("is_deleted", false).order("due_on"),
        supabase.from("payable_payments").select("id,payable_id,account_id,amount,paid_on").order("paid_on", { ascending: false }),
        supabase.from("recurring_payables").select("id,description,amount,due_day,start_on,end_on,account_id,category_id,is_active").eq("is_active", true).order("due_day"),
        supabase.from("reserves").select("id,name").order("name"),
        supabase.from("reserve_movements").select("id,reserve_id,account_id,amount,kind,occurred_on,transfer_group_id").order("occurred_on", { ascending: false }),
        supabase.from("goals").select("id,name,target_amount,target_on,planned_monthly_contribution,reserve_id,is_primary").order("target_on"),
      ]);
      if (accounts.error) throw accounts.error;
      if (categories.error) throw categories.error;
      if (transactions.error) throw transactions.error;
      if (cards.error) throw cards.error;
      if (charges.error) throw charges.error;
      if (payments.error) throw payments.error;
      if (payables.error) throw payables.error;
      if (payablePayments.error) throw payablePayments.error;
      if (recurrences.error) throw recurrences.error;
      if (reserves.error) throw reserves.error;
      if (reserveMovements.error) throw reserveMovements.error;
      if (goals.error) throw goals.error;
      setData({
        accounts: accounts.data.map((item) => ({ ...item, initial_balance: Number(item.initial_balance) })) as Account[],
        categories: categories.data as Category[],
        transactions: transactions.data.map((item) => ({ ...item, amount: Number(item.amount) })) as Transaction[],
        cards: cards.data.map((item) => ({ ...item, credit_limit: Number(item.credit_limit), spending_limit: Number(item.spending_limit) })),
        card_charges: charges.data.map((item) => ({ ...item, amount: Number(item.amount) })),
        card_payments: payments.data.map((item) => ({ ...item, amount: Number(item.amount) })),
        payables: payables.data.map((item) => ({ ...item, amount: Number(item.amount) })),
        payable_payments: payablePayments.data.map((item) => ({ ...item, amount: Number(item.amount) })),
        recurring_payables: recurrences.data.map((item) => ({ ...item, amount: Number(item.amount) })),
        reserves: reserves.data,
        reserve_movements: reserveMovements.data.map((item) => ({ ...item, amount: Number(item.amount) })),
        goals: goals.data.map((item) => ({ ...item, target_amount: Number(item.target_amount), planned_monthly_contribution: Number(item.planned_monthly_contribution) })),
      });
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Não foi possível carregar os dados.");
    } finally {
      setBusy(false);
    }
  }, [user]);

  useEffect(() => {
    if (isDemo) return;
    const supabase = getSupabase();
    if (!supabase) return;
    void supabase.auth.getUser().then(({ data: result, error: authError }) => {
      if (authError && authError.name !== "AuthSessionMissingError") {
        setError("Não foi possível verificar a sessão. Tente entrar novamente.");
      }
      setUser(result.user);
      setCheckingAuth(false);
    });
    const { data: listener } = supabase.auth.onAuthStateChange((event, session) => {
      if (event === "PASSWORD_RECOVERY") setSettingPassword(true);
      setUser(session?.user ?? null);
      setCheckingAuth(false);
    });
    return () => listener.subscription.unsubscribe();
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => { void loadData(); }, 0);
    return () => window.clearTimeout(timer);
  }, [loadData]);

  const monthlyTransactions = useMemo(() => data.transactions.filter((item) => item.occurred_on.startsWith(month)), [data.transactions, month]);
  const monthlyCardPayments = data.card_payments.filter((item) => item.paid_on.startsWith(month));
  const monthlyPayablePayments = data.payable_payments.filter((item) => item.paid_on.startsWith(month));
  const income = monthlyTransactions.filter((item) => item.kind === "income").reduce((sum, item) => sum + item.amount, 0);
  const expense = monthlyTransactions.filter((item) => item.kind === "expense").reduce((sum, item) => sum + item.amount, 0) +
    monthlyCardPayments.reduce((sum, item) => sum + item.amount, 0) +
    monthlyPayablePayments.reduce((sum, item) => sum + item.amount, 0);
  const balance = data.accounts.reduce((sum, account) => sum + accountBalance(account, data.transactions, data.card_payments, data.payable_payments), 0);
  const reserved = totalReserved(data.reserve_movements);
  const availableBalance = balance - reserved;
  const primaryGoal = data.goals.find((item) => item.is_primary) ?? data.goals[0];
  const primaryForecast = primaryGoal ? goalForecast(primaryGoal, data.reserve_movements) : null;
  const recent = [...monthlyTransactions].sort((a, b) => b.occurred_on.localeCompare(a.occurred_on));
  const filtered = recent.filter((item) => {
    const text = `${item.description} ${data.categories.find((category) => category.id === item.category_id)?.name ?? ""}`.toLocaleLowerCase("pt-BR");
    return text.includes(search.toLocaleLowerCase("pt-BR"));
  });

  async function signIn(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const supabase = getSupabase();
    if (!supabase) return;
    const form = new FormData(event.currentTarget);
    setBusy(true);
    setError("");
    const { error: signInError } = await supabase.auth.signInWithPassword({
      email: String(form.get("email") ?? ""), password: String(form.get("password") ?? ""),
    });
    if (signInError) setError("E-mail ou senha inválidos.");
    setBusy(false);
  }

  async function signOut() {
    await getSupabase()?.auth.signOut();
    setUser(null);
    setData(emptyData);
    setPage("overview");
    setSettingPassword(false);
    setEditingTransaction(null);
  }

  function openTransactionForm(item?: Transaction) {
    setEditingTransaction(item ?? null);
    setError("");
    setModal(item?.kind === "transfer" ? "transfer" : "transaction");
  }

  function openTransferForm(item?: Transaction) {
    setEditingTransaction(item ?? null);
    setError("");
    setModal("transfer");
  }

  async function savePassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const password = String(form.get("password") ?? "");
    const confirmation = String(form.get("confirmation") ?? "");
    if (password.length < 12 || password !== confirmation) {
      setError("Use pelo menos 12 caracteres e confirme a mesma senha.");
      return;
    }
    setBusy(true);
    setError("");
    const { error: updateError } = await getSupabase()!.auth.updateUser({ password });
    if (updateError) {
      setError("Não foi possível definir a senha. Abra um novo link de convite.");
    } else {
      setSettingPassword(false);
      setNotice("Senha definida com sucesso.");
    }
    setBusy(false);
  }

  async function saveTransaction(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const kind = String(form.get("kind")) as TransactionKind;
    const amount = Number(form.get("amount"));
    const description = String(form.get("description") ?? "").trim();
    const account_id = String(form.get("account_id") ?? "");
    const category_id = String(form.get("category_id") ?? "");
    const occurred_on = String(form.get("occurred_on") ?? "");
    if (!description || !Number.isFinite(amount) || amount <= 0 || !account_id || !category_id || !occurred_on ||
      data.categories.find((item) => item.id === category_id)?.kind !== kind) {
      setError("Confira os campos do lançamento.");
      return;
    }
    setBusy(true);
    setError("");
    const item: Transaction = { id: editingTransaction?.id ?? crypto.randomUUID(), account_id, destination_account_id: null, category_id, kind, description, amount, occurred_on, created_by: editingTransaction?.created_by ?? user?.id };
    if (isDemo) {
      setData((current) => ({ ...current, transactions: editingTransaction
        ? current.transactions.map((entry) => entry.id === item.id ? item : entry)
        : [item, ...current.transactions] }));
    } else {
      const { error: saveError } = editingTransaction
        ? await getSupabase()!.from("transactions").update({ account_id, destination_account_id: null, category_id, kind, description, amount, occurred_on }).eq("id", item.id).select("id").single()
        : await getSupabase()!.from("transactions").insert(item);
      if (saveError) { setError(saveError.message); setBusy(false); return; }
      await loadData();
    }
    setMonth(occurred_on.slice(0, 7));
    setModal(null);
    setEditingTransaction(null);
    setNotice(editingTransaction ? "Lançamento alterado com sucesso." : "Lançamento salvo com sucesso.");
    setBusy(false);
  }

  async function saveAccount(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const name = String(form.get("name") ?? "").trim();
    const type = String(form.get("type")) as Account["type"];
    const initial_balance = Number(form.get("initial_balance"));
    if (!name || !Number.isFinite(initial_balance)) { setError("Confira os dados da conta."); return; }
    setBusy(true);
    setError("");
    const account: Account = { id: crypto.randomUUID(), name, type, initial_balance };
    if (isDemo) {
      setData((current) => ({ ...current, accounts: [...current.accounts, account] }));
    } else {
      const { error: saveError } = await getSupabase()!.from("accounts").insert(account);
      if (saveError) { setError(saveError.message); setBusy(false); return; }
      await loadData();
    }
    setModal(null);
    setNotice("Conta criada com sucesso.");
    setBusy(false);
  }

  async function saveTransfer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const account_id = String(form.get("account_id") ?? "");
    const destination_account_id = String(form.get("destination_account_id") ?? "");
    const amount = Number(form.get("amount"));
    const description = String(form.get("description") ?? "").trim();
    const occurred_on = String(form.get("occurred_on") ?? "");
    if (!account_id || !destination_account_id || account_id === destination_account_id ||
      !description || !Number.isFinite(amount) || amount <= 0 || !occurred_on) {
      setError("Confira os dados da transferência. As contas precisam ser diferentes.");
      return;
    }
    setBusy(true);
    setError("");
    const item: Transaction = {
      id: editingTransaction?.id ?? crypto.randomUUID(), account_id, destination_account_id, category_id: null,
      kind: "transfer", description, amount, occurred_on, created_by: editingTransaction?.created_by ?? user?.id,
    };
    if (isDemo) {
      setData((current) => ({ ...current, transactions: editingTransaction
        ? current.transactions.map((entry) => entry.id === item.id ? item : entry)
        : [item, ...current.transactions] }));
    } else {
      const { error: saveError } = editingTransaction
        ? await getSupabase()!.from("transactions").update({ account_id, destination_account_id, category_id: null, kind: "transfer", description, amount, occurred_on }).eq("id", item.id).select("id").single()
        : await getSupabase()!.from("transactions").insert(item);
      if (saveError) { setError(saveError.message); setBusy(false); return; }
      await loadData();
    }
    setMonth(occurred_on.slice(0, 7));
    setModal(null);
    setEditingTransaction(null);
    setNotice(editingTransaction ? "Transferência alterada com sucesso." : "Transferência salva com sucesso.");
    setBusy(false);
  }

  async function saveCategory(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const name = String(form.get("name") ?? "").trim();
    const kind = String(form.get("kind")) as TransactionKind;
    if (!name) { setError("Informe o nome da categoria."); return; }
    setBusy(true);
    setError("");
    const category: Category = { id: crypto.randomUUID(), name, kind };
    if (isDemo) {
      setData((current) => ({ ...current, categories: [...current.categories, category] }));
    } else {
      const { error: saveError } = await getSupabase()!.from("categories").insert(category);
      if (saveError) { setError(saveError.message); setBusy(false); return; }
      await loadData();
    }
    setModal(null);
    setNotice("Categoria criada com sucesso.");
    setBusy(false);
  }

  async function removeTransaction(item: Transaction) {
    if (!window.confirm(`Excluir o lançamento “${item.description}”?`)) return;
    setBusy(true);
    setError("");
    if (isDemo) {
      setData((current) => ({ ...current, transactions: current.transactions.filter((entry) => entry.id !== item.id) }));
    } else {
      const { error: deleteError } = await getSupabase()!.from("transactions").delete().eq("id", item.id);
      if (deleteError) { setError(deleteError.message); setBusy(false); return; }
      await loadData();
    }
    setNotice("Lançamento excluído.");
    setBusy(false);
  }

  async function removeAccount(account: Account) {
    if (data.transactions.some((item) => item.account_id === account.id || item.destination_account_id === account.id) ||
        data.cards.some((item) => item.payment_account_id === account.id) ||
        data.card_payments.some((item) => item.account_id === account.id) ||
        data.payables.some((item) => item.account_id === account.id) ||
        data.payable_payments.some((item) => item.account_id === account.id) ||
        data.recurring_payables.some((item) => item.account_id === account.id) ||
        data.reserve_movements.some((item) => item.account_id === account.id)) {
      setError("Exclua os lançamentos, cartões, pendências, recorrências e movimentos de reserva vinculados a esta conta antes de removê-la.");
      return;
    }
    if (!window.confirm(`Excluir a conta “${account.name}”?`)) return;
    setBusy(true);
    setError("");
    if (isDemo) {
      setData((current) => ({ ...current, accounts: current.accounts.filter((item) => item.id !== account.id) }));
    } else {
      const { error: deleteError } = await getSupabase()!.from("accounts").delete().eq("id", account.id).select("id").single();
      if (deleteError) {
        setError("Não foi possível excluir esta conta. Verifique se há lançamentos, cartões ou pendências vinculados.");
        setBusy(false);
        return;
      }
      await loadData();
    }
    setNotice("Conta excluída.");
    setBusy(false);
  }

  async function removeCategory(category: Category) {
    if (data.transactions.some((item) => item.category_id === category.id) ||
        data.card_charges.some((item) => item.category_id === category.id) ||
        data.payables.some((item) => item.category_id === category.id) ||
        data.recurring_payables.some((item) => item.category_id === category.id)) {
      setError("Exclua os lançamentos, compras, pendências e recorrências desta categoria antes de removê-la.");
      return;
    }
    if (!window.confirm(`Excluir a categoria “${category.name}”?`)) return;
    setBusy(true);
    setError("");
    if (isDemo) {
      setData((current) => ({ ...current, categories: current.categories.filter((item) => item.id !== category.id) }));
    } else {
      const { error: deleteError } = await getSupabase()!.from("categories").delete().eq("id", category.id).select("id").single();
      if (deleteError) {
        setError("Não foi possível excluir esta categoria. Verifique se há lançamentos, compras ou pendências vinculados.");
        setBusy(false);
        return;
      }
      await loadData();
    }
    setNotice("Categoria excluída.");
    setBusy(false);
  }

  function navigate(next: Page) {
    setCalendarDueOn(null);
    setPage(next);
    setMobileMenu(false);
    setNotice("");
  }

  async function exportData() {
    setExporting(true);
    setExportError("");
    try { downloadFinanceExport(await collectFinanceExport()); }
    catch (cause) { setExportError(cause instanceof Error ? cause.message : "Não foi possível exportar os dados."); }
    finally { setExporting(false); }
  }

  const nav = [
    { id: "overview" as Page, label: "Visão geral", icon: LayoutGrid },
    { id: "transactions" as Page, label: "Lançamentos", icon: ArrowRight },
    { id: "accounts" as Page, label: "Contas", icon: Wallet },
    { id: "cards" as Page, label: "Cartões", icon: CreditCard },
    { id: "payables" as Page, label: "Agenda", icon: CalendarDays },
    { id: "reserves" as Page, label: "Reservas", icon: Landmark },
    { id: "goals" as Page, label: "Objetivos", icon: Target },
    { id: "reports" as Page, label: "Relatórios", icon: ChartNoAxesCombined },
    { id: "calendar" as Page, label: "Calendário", icon: CalendarDays },
    { id: "planning" as Page, label: "Planejamento", icon: ChartSpline },
    { id: "more" as Page, label: "Mais", icon: Menu },
  ];
  const mobileNav = nav.filter((item) => item.id !== "more" && item.id !== "goals" && item.id !== "reports" && item.id !== "calendar" && item.id !== "planning");

  if (checkingAuth) return <div className="splash"><div className="brand-mark">N</div><span>Preparando seu financeiro...</span></div>;
  if (!isDemo && !getSupabase()) return <div className="splash"><div className="brand-mark">N</div><h1>Configuração pendente</h1><p>Conecte o projeto Supabase para usar o financeiro.</p></div>;
  if (!isDemo && !user) return <div className="auth-page"><div className="auth-card"><div className="brand-mark">N</div><p className="eyebrow">NICK FINANÃAS</p><h1>Bom ter você aqui.</h1><p className="muted">Entre para acompanhar o financeiro de vocês em um só lugar.</p><form onSubmit={signIn} className="stack-form"><label>E-mail<input type="email" name="email" autoComplete="email" required /></label><label>Senha<input type="password" name="password" autoComplete="current-password" required /></label>{error && <p className="form-error" role="alert">{error}</p>}<button className="primary-button" disabled={busy}>Entrar</button></form><p className="auth-footnote"><ShieldCheck size={15} /> Acesso restrito aos dois usuários autorizados.</p></div></div>;
  if (settingPassword) return <div className="auth-page"><div className="auth-card"><div className="brand-mark">N</div><p className="eyebrow">ACESSO AO FINANCEIRO</p><h1>Defina sua senha.</h1><p className="muted">Escolha uma senha para entrar nos próximos acessos.</p><form onSubmit={savePassword} className="stack-form"><label>Nova senha<input type="password" name="password" autoComplete="new-password" minLength={12} required /></label><label>Confirme a senha<input type="password" name="confirmation" autoComplete="new-password" minLength={12} required /></label>{error && <p className="form-error" role="alert">{error}</p>}<button className="primary-button" disabled={busy}>Salvar senha</button></form></div></div>;
  if (accessDenied) return <div className="splash"><ShieldCheck size={32} /><h1>Acesso não autorizado</h1><p>Esta conta ainda não foi liberada para o financeiro.</p><button className="secondary-button" onClick={signOut}>Sair</button></div>;

  return <div className="app-shell">
    <aside className={`sidebar ${mobileMenu ? "sidebar-open" : ""}`}>
      <div className="sidebar-top"><div className="brand-mark">N</div><div><strong>Financeiro</strong><small>FinanÃ§as pessoais</small></div><button className="icon-button sidebar-close" aria-label="Fechar menu" onClick={() => setMobileMenu(false)}><X size={20} /></button></div>
      <div className="space-card"><div className="space-icon"><House size={18} /></div><div><small>FINANCEIRO DO CASAL</small><strong>Contas compartilhadas</strong></div></div>
      <p className="nav-caption">MENU PRINCIPAL</p>
      <nav className="sidebar-nav" aria-label="Menu principal">{nav.map(({ id, label, icon: Icon }) => <button key={id} className={page === id ? "nav-link active" : "nav-link"} onClick={() => navigate(id)}><Icon size={18} />{label}</button>)}</nav>
      <div className="sidebar-bottom"><div className="sidebar-help"><CircleHelp size={18} /><span>Seu financeiro, do seu jeito.<small>Primeira versão web</small></span></div><div className="user-block"><div className="avatar">{isDemo ? "D" : user?.email?.[0]?.toUpperCase()}</div><div><strong>{isDemo ? "Demonstração" : user?.email?.split("@")[0]}</strong><small>{isDemo ? "Dados fictícios" : "Conta conectada"}</small></div>{!isDemo && <button className="icon-button" aria-label="Sair" onClick={signOut}><LogOut size={17} /></button>}</div></div>
    </aside>

    <main className="main-area">
      <header className="topbar"><button className="icon-button menu-toggle" aria-label="Abrir menu" onClick={() => setMobileMenu(true)}><Menu size={22} /></button><div className="topbar-path">FINANCEIRO <span>/</span> <strong>{nav.find((item) => item.id === page)?.label}</strong></div><div className="topbar-right">{isDemo && <span className="demo-badge">MODO DEMONSTRAÇÃO</span>}<span className="topbar-avatar">{isDemo ? "D" : user?.email?.[0]?.toUpperCase()}</span></div></header>
      <div className="content">
        {notice && <div className="notice" role="status"><ShieldCheck size={16} />{notice}<button aria-label="Fechar aviso" onClick={() => setNotice("")}><X size={15} /></button></div>}
        {error && <div className="error-banner" role="alert">{error}<button aria-label="Fechar erro" onClick={() => setError("")}><X size={15} /></button></div>}
        {page === "overview" && <>
          <div className="page-heading"><div><p className="eyebrow">NOSSO PAINEL</p><h1>Visão geral</h1><p className="muted">Tudo que importa para cuidar das finanças de vocês.</p></div><button className="primary-button desktop-action" onClick={() => openTransactionForm()}><Plus size={17} /> Novo lançamento</button></div>
          <div className="period-row"><div className="period-switch"><button aria-label="Mês anterior" onClick={() => setMonth(shiftMonth(month, -1))}><ChevronLeft size={17} /></button><span>{monthLabel(month)}</span><button aria-label="Próximo mês" onClick={() => setMonth(shiftMonth(month, 1))}><ChevronRight size={17} /></button></div><span className="period-caption"><CalendarDays size={15} /> Resumo do período</span></div>
          <div className="stat-grid"><Stat title="Saldo disponível" value={currency(availableBalance)} tone="dark" icon={Wallet} hint={`${currency(reserved)} em reservas`} /><Stat title="Entradas do mês" value={currency(income)} tone="green" icon={ArrowDownLeft} hint={`${monthlyTransactions.filter((item) => item.kind === "income").length} lançamentos`} /><Stat title="Saídas do mês" value={currency(expense)} tone="orange" icon={ArrowUpRight} hint={`${monthlyTransactions.filter((item) => item.kind === "expense").length + monthlyCardPayments.length + monthlyPayablePayments.length} movimentações`} /><Stat title="Resultado do mês" value={currency(income - expense)} tone="light" icon={ChartNoAxesCombined} hint="Entradas menos saídas" /></div>
          <div className="overview-grid"><section className="panel chart-panel"><div className="section-title"><div><h2>Movimentação do mês</h2><p>Entradas e saídas em perspectiva</p></div><button className="text-button" onClick={() => navigate("reports")}>Ver relatórios <ArrowRight size={15} /></button></div><div className="chart-legend"><span><i className="legend-green" /> Entradas</span><span><i className="legend-orange" /> Saídas</span></div><div className="bar-chart"><div className="chart-lines"><span>{currency(Math.max(income, expense))}</span><span>{currency(Math.max(income, expense) / 2)}</span><span>R$ 0</span></div><div className="chart-bars"><div className="bar-set"><div className="bar green" style={{ height: `${Math.max(4, income / Math.max(income, expense, 1) * 100)}%` }} /><span>Entradas</span></div><div className="bar-set"><div className="bar orange" style={{ height: `${Math.max(4, expense / Math.max(income, expense, 1) * 100)}%` }} /><span>Saídas</span></div></div></div></section><section className="panel accounts-panel"><div className="section-title"><div><h2>Suas contas</h2><p>Saldo atualizado de cada conta</p></div><Landmark size={18} /></div><div className="account-list">{data.accounts.length ? data.accounts.map((account) => <div className="account-mini" key={account.id}><div className="account-symbol"><Landmark size={18} /></div><div><strong>{account.name}</strong><small>{accountNames[account.type]}</small></div><b>{currency(accountBalance(account, data.transactions, data.card_payments, data.payable_payments))}</b></div>) : <Empty text="Nenhuma conta cadastrada." />}</div><button className="text-button" onClick={() => navigate("accounts")}>Ver todas as contas <ArrowRight size={15} /></button></section></div>
          <section className="panel goal-preview"><div className="goal-preview-symbol"><Target size={20} /></div><div><small>PRÓXIMO OBJETIVO</small><h2>{primaryGoal?.name ?? "Definam um objetivo"}</h2><p>{primaryGoal && primaryForecast ? `${currency(primaryForecast.current)} de ${currency(primaryGoal.target_amount)} · ${Math.round(primaryForecast.percentage)}% concluído` : "Acompanhem a meta e o dinheiro reservado em um só lugar."}</p></div><button className="text-button" onClick={() => navigate("goals")}>Ver objetivos <ArrowRight size={15} /></button></section>
          <section className="panel recent-panel"><div className="section-title"><div><h2>Últimos lançamentos</h2><p>Atividade em {monthLabel(month)}</p></div><button className="text-button" onClick={() => navigate("transactions")}>Ver extrato <ArrowRight size={15} /></button></div><TransactionList items={recent.slice(0, 5)} data={data} onEdit={openTransactionForm} onDelete={removeTransaction} /></section>
        </>}
        {page === "transactions" && <><div className="page-heading"><div><p className="eyebrow">MOVIMENTAÇÕES</p><h1>Lançamentos</h1><p className="muted">Acompanhe cada entrada e saída do mês.</p></div><button className="primary-button desktop-action" onClick={() => openTransactionForm()}><Plus size={17} /> Novo lançamento</button></div><div className="toolbar"><div className="period-switch"><button aria-label="Mês anterior" onClick={() => setMonth(shiftMonth(month, -1))}><ChevronLeft size={17} /></button><span>{monthLabel(month)}</span><button aria-label="Próximo mês" onClick={() => setMonth(shiftMonth(month, 1))}><ChevronRight size={17} /></button></div><label className="search-box"><Search size={17} /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar lançamento" aria-label="Buscar lançamento" /></label></div><section className="panel ledger-panel"><div className="section-title"><div><h2>Extrato do mês</h2><p>{filtered.length} {filtered.length === 1 ? "lançamento" : "lançamentos"}</p></div><span className="ledger-total">Resultado: <strong>{currency(income - expense)}</strong></span></div><TransactionList items={filtered} data={data} onEdit={openTransactionForm} onDelete={removeTransaction} /></section>{monthlyCardPayments.length > 0 && <CardPaymentList payments={monthlyCardPayments} data={data} />}{monthlyPayablePayments.length > 0 && <PayablePaymentList payments={monthlyPayablePayments} data={data} />}</>}
        {page === "accounts" && <><div className="page-heading"><div><p className="eyebrow">ORGANIZAÇÃO</p><h1>Contas e categorias</h1><p className="muted">Organize onde o dinheiro está e como ele se movimenta.</p></div><button className="primary-button desktop-action" onClick={() => setModal("account")}><Plus size={17} /> Nova conta</button></div><div className="account-summary"><span>Saldo somado de todas as contas</span><strong>{currency(balance)}</strong><small>{data.accounts.length} {data.accounts.length === 1 ? "conta cadastrada" : "contas cadastradas"}</small></div><button className="secondary-button transfer-action" onClick={() => openTransferForm()} disabled={data.accounts.length < 2}><ArrowLeftRight size={16} /> Transferir entre contas</button><div className="management-grid"><section className="panel"><div className="section-title"><div><h2>Contas</h2><p>Saldo inicial mais movimentações</p></div><button className="round-add" aria-label="Adicionar conta" onClick={() => setModal("account")}><Plus size={18} /></button></div><div className="account-list large">{data.accounts.length ? data.accounts.map((account) => <div className="account-mini" key={account.id}><div className="account-symbol"><Landmark size={18} /></div><div><strong>{account.name}</strong><small>{accountNames[account.type]}</small></div><b>{currency(accountBalance(account, data.transactions, data.card_payments, data.payable_payments))}</b><button className="delete-button" title="Excluir conta vazia" aria-label={`Excluir conta ${account.name}`} disabled={busy} onClick={() => removeAccount(account)}><Trash2 size={16} /></button></div>) : <Empty text="Crie sua primeira conta para começar." />}</div></section><section className="panel"><div className="section-title"><div><h2>Categorias</h2><p>Classifique receitas e despesas</p></div><button className="round-add" aria-label="Adicionar categoria" onClick={() => setModal("category")}><Plus size={18} /></button></div><div className="category-list">{data.categories.length ? data.categories.map((category) => <div className="category-item" key={category.id}><div className={`category-dot ${category.kind}`} /><span>{category.name}</span><small>{category.kind === "income" ? "Entrada" : "Saída"}</small><button className="delete-button" title="Excluir categoria vazia" aria-label={`Excluir categoria ${category.name}`} disabled={busy} onClick={() => removeCategory(category)}><Trash2 size={16} /></button></div>) : <Empty text="Cadastre as categorias que vocês usam." />}</div></section></div></>}
        {page === "cards" && <CardsPage data={data} setData={setData} reload={loadData} />}
        {page === "payables" && <PayablesPage data={data} setData={setData} reload={loadData} initialDueOn={calendarDueOn} onCalendar={() => navigate("calendar")} />}
        {page === "reserves" && <ReservesPage data={data} setData={setData} reload={loadData} />}
        {page === "goals" && <GoalsPage data={data} setData={setData} reload={loadData} onReserves={() => navigate("reserves")} />}
        {page === "reports" && <ReportsPage data={data} onReserves={() => navigate("reserves")} onGoals={() => navigate("goals")} />}
        {page === "calendar" && <CalendarPage data={data} setData={setData} reload={loadData} onSchedule={(date) => { setCalendarDueOn(date); setPage("payables"); setMobileMenu(false); }} />}
        {page === "planning" && <PlanningPage data={data} setData={setData} reload={loadData} onAgenda={() => navigate("payables")} />}
        {page === "more" && <><div className="page-heading"><div><p className="eyebrow">FINANCEIRO COMPLETO</p><h1>Mais do financeiro</h1><p className="muted">Acompanhem os próximos meses e guardem uma cópia dos dados.</p></div></div><div className="feature-grid"><button className="feature-card" onClick={() => navigate("planning")}><ChartSpline size={20} /><strong>Planejamento</strong><span>Projeções dos próximos seis meses.</span></button></div><section className="panel export-panel"><div><Download size={20} /><div><h2>Exportar dados</h2><p>Baixe um arquivo JSON com contas, lançamentos, faturas, pendências, reservas e objetivos. Guarde o arquivo em um lugar privado.</p></div></div>{exportError && <p className="form-error" role="alert">{exportError}</p>}<button className="secondary-button" disabled={exporting || isDemo} onClick={() => void exportData()}><Download size={16} /> {exporting ? "Preparando arquivo..." : "Baixar cópia dos dados"}</button>{isDemo && <small>Disponível no site conectado ao Supabase.</small>}</section></>}
      </div>
      <button className="mobile-fab" aria-label="Novo lançamento" onClick={() => openTransactionForm()}><Plus size={25} /></button>
      <nav className="mobile-nav" aria-label="Navegação móvel">{mobileNav.map(({ id, label, icon: Icon }) => <button key={id} className={page === id ? "active" : ""} onClick={() => navigate(id)}><Icon size={21} /><span>{label}</span></button>)}</nav>
    </main>

    {modal && <div className="modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setModal(null); }}><div className="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title"><div className="modal-head"><div><p className="eyebrow">{editingTransaction && (modal === "transaction" || modal === "transfer") ? "ALTERAR REGISTRO" : "NOVO CADASTRO"}</p><h2 id="modal-title">{modal === "transaction" ? (editingTransaction ? "Editar lançamento" : "Novo lançamento") : modal === "transfer" ? (editingTransaction ? "Editar transferência" : "Nova transferência") : modal === "account" ? "Nova conta" : "Nova categoria"}</h2></div><button className="icon-button" aria-label="Fechar" onClick={() => setModal(null)}><X size={21} /></button></div>{error && <p className="form-error modal-error" role="alert">{error}</p>}{modal === "transaction" ? <TransactionForm key={editingTransaction?.id ?? "new"} initial={editingTransaction} accounts={data.accounts} categories={data.categories} busy={busy} onSubmit={saveTransaction} onCategory={() => setModal("category")} /> : modal === "transfer" ? <TransferForm key={editingTransaction?.id ?? "new"} initial={editingTransaction} accounts={data.accounts} busy={busy} onSubmit={saveTransfer} /> : modal === "account" ? <form className="stack-form" onSubmit={saveAccount}><label>Nome da conta<input name="name" maxLength={120} placeholder="Ex.: Conta principal" required /></label><label>Tipo<select name="type"><option value="checking">Conta corrente</option><option value="savings">Poupança</option><option value="cash">Dinheiro</option><option value="investment">Investimento</option></select></label><label>Saldo inicial<input name="initial_balance" type="number" step="0.01" inputMode="decimal" defaultValue="0" required /></label><button className="primary-button" disabled={busy}>Salvar conta</button></form> : <form className="stack-form" onSubmit={saveCategory}><label>Nome da categoria<input name="name" maxLength={80} placeholder="Ex.: Mercado" required /></label><label>Tipo<select name="kind"><option value="expense">Saída</option><option value="income">Entrada</option></select></label><button className="primary-button" disabled={busy}>Salvar categoria</button></form>}</div></div>}
  </div>;
}

function TransactionForm({ initial, accounts, categories, busy, onSubmit, onCategory }: { initial: Transaction | null; accounts: Account[]; categories: Category[]; busy: boolean; onSubmit: (event: FormEvent<HTMLFormElement>) => void; onCategory: () => void }) {
  const [kind, setKind] = useState<TransactionKind>(initial?.kind === "income" ? "income" : "expense");
  const available = categories.filter((item) => item.kind === kind);
  return <form className="stack-form" onSubmit={onSubmit}>
    <div className="segmented"><label><input type="radio" name="kind" value="expense" checked={kind === "expense"} onChange={() => setKind("expense")} /><span>Saída</span></label><label><input type="radio" name="kind" value="income" checked={kind === "income"} onChange={() => setKind("income")} /><span>Entrada</span></label></div>
    <label>Descrição<input name="description" maxLength={180} placeholder="Ex.: Mercado da semana" defaultValue={initial?.description} required /></label>
    <div className="form-row"><label>Valor<input name="amount" type="number" step="0.01" min="0.01" inputMode="decimal" placeholder="0,00" defaultValue={initial?.amount} required /></label><label>Data<input name="occurred_on" type="date" defaultValue={initial?.occurred_on ?? todayLocal()} required /></label></div>
    <label>Conta<select name="account_id" defaultValue={initial?.account_id ?? accounts[0]?.id} required>{accounts.length === 0 && <option value="">Cadastre uma conta primeiro</option>}{accounts.map((account) => <option value={account.id} key={account.id}>{account.name}</option>)}</select></label>
    <label>Categoria<select name="category_id" key={kind} defaultValue={initial?.kind === kind ? initial.category_id ?? undefined : available[0]?.id} required>{available.length === 0 && <option value="">Cadastre uma categoria</option>}{available.map((category) => <option value={category.id} key={category.id}>{category.name}</option>)}</select></label>
    <button className="inline-link" type="button" onClick={onCategory}><Plus size={15} /> Nova categoria</button>
    <button className="primary-button" disabled={busy || !accounts.length || !available.length}>{initial ? "Salvar alterações" : "Salvar lançamento"}</button>
  </form>;
}

function TransferForm({ initial, accounts, busy, onSubmit }: { initial: Transaction | null; accounts: Account[]; busy: boolean; onSubmit: (event: FormEvent<HTMLFormElement>) => void }) {
  return <form className="stack-form" onSubmit={onSubmit}>
    <label>Descrição<input name="description" maxLength={180} placeholder="Ex.: Guardar na poupança" defaultValue={initial?.description} required /></label>
    <div className="form-row"><label>Valor<input name="amount" type="number" step="0.01" min="0.01" inputMode="decimal" defaultValue={initial?.amount} required /></label><label>Data<input name="occurred_on" type="date" defaultValue={initial?.occurred_on ?? todayLocal()} required /></label></div>
    <label>De qual conta?<select name="account_id" required defaultValue={initial?.account_id ?? accounts[0]?.id}>{accounts.map((account) => <option value={account.id} key={account.id}>{account.name}</option>)}</select></label>
    <label>Para qual conta?<select name="destination_account_id" required defaultValue={initial?.destination_account_id ?? accounts[1]?.id}>{accounts.map((account) => <option value={account.id} key={account.id}>{account.name}</option>)}</select></label>
    <p className="form-help">O valor sai da primeira conta e entra na segunda. O total do financeiro permanece igual.</p>
    <button className="primary-button" disabled={busy || accounts.length < 2}>{initial ? "Salvar alterações" : "Salvar transferência"}</button>
  </form>;
}

function Stat({ title, value, tone, icon: Icon, hint }: { title: string; value: string; tone: string; icon: typeof Wallet; hint: string }) {
  return <div className={`stat-card ${tone}`}><div className="stat-top"><span>{title}</span><Icon size={19} /></div><strong>{value}</strong><small>{hint}</small></div>;
}

function TransactionList({ items, data, onEdit, onDelete }: { items: Transaction[]; data: FinanceData; onEdit: (item: Transaction) => void; onDelete: (item: Transaction) => void }) {
  if (!items.length) return <Empty text="Ainda não há lançamentos neste período." />;
  return <div className="transaction-list">{items.map((item) => {
    const category = data.categories.find((entry) => entry.id === item.category_id);
    const account = data.accounts.find((entry) => entry.id === item.account_id);
    const destination = data.accounts.find((entry) => entry.id === item.destination_account_id);
    const Icon = item.kind === "income" ? ArrowDownLeft : item.kind === "expense" ? ArrowUpRight : ArrowLeftRight;
    const detail = item.kind === "transfer"
      ? `${account?.name ?? "Conta removida"} → ${destination?.name ?? "Conta removida"}`
      : `${category?.name ?? "Sem categoria"} · ${account?.name ?? "Conta removida"}`;
    return <div className="transaction-row" key={item.id}>
      <div className={`transaction-icon ${item.kind}`}><Icon size={19} /></div>
      <div className="transaction-main"><strong>{item.description}</strong><small>{detail}</small></div>
      <div className="transaction-right"><strong className={item.kind}>{item.kind === "income" ? "+ " : item.kind === "expense" ? "− " : ""}{currency(item.amount)}</strong><small>{dateLabel(item.occurred_on)}</small></div>
      <button className="delete-button edit-button" title="Editar lançamento" aria-label={`Editar ${item.description}`} onClick={() => onEdit(item)}><Pencil size={16} /></button>
      <button className="delete-button" title="Excluir lançamento" aria-label={`Excluir ${item.description}`} onClick={() => onDelete(item)}><Trash2 size={16} /></button>
    </div>;
  })}</div>;
}

function CardPaymentList({ payments, data }: { payments: CardPayment[]; data: FinanceData }) {
  return <section className="panel ledger-panel payment-ledger"><div className="section-title"><div><h2>Faturas pagas no mês</h2><p>Saídas já descontadas do saldo das contas</p></div></div><div className="transaction-list">{payments.map((payment) => {
    const card = data.cards.find((item) => item.id === payment.card_id);
    const account = data.accounts.find((item) => item.id === payment.account_id);
    return <div className="transaction-row" key={payment.id}><div className="transaction-icon expense"><CreditCard size={19} /></div><div className="transaction-main"><strong>Pagamento da fatura {card?.name ?? "Cartão removido"}</strong><small>{account?.name ?? "Conta removida"} · {monthLabel(payment.invoice_month.slice(0, 7))}</small></div><div className="transaction-right"><strong className="expense">− {currency(payment.amount)}</strong><small>{dateLabel(payment.paid_on)}</small></div></div>;
  })}</div></section>;
}

function PayablePaymentList({ payments, data }: { payments: PayablePayment[]; data: FinanceData }) {
  return <section className="panel ledger-panel payment-ledger"><div className="section-title"><div><h2>Pendências pagas no mês</h2><p>Saídas já descontadas do saldo das contas</p></div></div><div className="transaction-list">{payments.map((payment) => {
    const payable = data.payables.find((item) => item.id === payment.payable_id);
    const account = data.accounts.find((item) => item.id === payment.account_id);
    return <div className="transaction-row" key={payment.id}><div className="transaction-icon expense"><CalendarDays size={19} /></div><div className="transaction-main"><strong>{payable?.description ?? "Pendência removida"}</strong><small>{account?.name ?? "Conta removida"} · Venceu em {payable ? dateLabel(payable.due_on) : "—"}</small></div><div className="transaction-right"><strong className="expense">− {currency(payment.amount)}</strong><small>{dateLabel(payment.paid_on)}</small></div></div>;
  })}</div></section>;
}

function Empty({ text }: { text: string }) { return <div className="empty-state">{text}</div>; }

