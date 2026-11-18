-- Confira os registros da amostra no projeto de teste depois da restauracao.
with counts as (
  select 'accounts' as tabela, count(*) as registros from public.accounts
  union all select 'categories', count(*) from public.categories
  union all select 'transactions', count(*) from public.transactions
  union all select 'credit_cards', count(*) from public.credit_cards
  union all select 'card_charges', count(*) from public.card_charges
  union all select 'card_payments', count(*) from public.card_payments
  union all select 'payables', count(*) from public.payables
  union all select 'payable_payments', count(*) from public.payable_payments
  union all select 'recurring_payables', count(*) from public.recurring_payables
  union all select 'reserves', count(*) from public.reserves
  union all select 'reserve_movements', count(*) from public.reserve_movements
  union all select 'goals', count(*) from public.goals
), totals as (
  select
    (select coalesce(sum(initial_balance), 0) from public.accounts)
    + (select coalesce(sum(case when kind = 'income' then amount when kind = 'expense' then -amount else 0 end), 0) from public.transactions)
    - (select coalesce(sum(amount), 0) from public.card_payments)
    - (select coalesce(sum(amount), 0) from public.payable_payments) as saldo_bancario,
    (select coalesce(sum(case when kind = 'deposit' then amount else -amount end), 0) from public.reserve_movements) as reservado
)
select
  (select jsonb_object_agg(tabela, registros) from counts) as registros_por_tabela,
  saldo_bancario,
  reservado,
  saldo_bancario - reservado as disponivel
from totals;
