-- Recorrencias mensais da Agenda. Execute depois de 202610180005_payables.sql.
-- As ocorrencias sao criadas quando um mes e aberto, sem cron ou servico pago.

create table public.recurring_payables (
  id uuid primary key default gen_random_uuid(),
  description text not null check (char_length(trim(description)) between 1 and 180),
  amount numeric(18,2) not null check (amount > 0),
  due_day integer not null check (due_day between 1 and 31),
  start_on date not null,
  end_on date,
  account_id uuid not null references public.accounts(id) on delete restrict,
  category_id uuid not null references public.categories(id) on delete restrict,
  is_active boolean not null default true,
  created_by uuid not null references auth.users(id),
  created_at timestamptz not null default now(),
  check (end_on is null or end_on >= start_on)
);

create index recurring_payables_active_idx on public.recurring_payables (is_active, start_on);
alter table public.recurring_payables enable row level security;
revoke all on public.recurring_payables from anon, authenticated;
grant select on public.recurring_payables to authenticated;
create policy "Allowed users read recurring payables" on public.recurring_payables for select to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));

alter table public.payables add column recurring_payable_id uuid references public.recurring_payables(id) on delete restrict;
alter table public.payables add column is_deleted boolean not null default false;
alter table public.payables add constraint payables_recurring_no_installment
  check (recurring_payable_id is null or installment_group_id is null);
create unique index payables_recurring_occurrence_idx on public.payables (recurring_payable_id, due_on)
  where recurring_payable_id is not null;

create function public.generate_recurring_payables(p_month date)
returns integer
language plpgsql security definer set search_path = ''
as $$
declare
  v_month date;
  v_due date;
  v_count integer := 0;
  v_inserted integer;
  v_rule public.recurring_payables%rowtype;
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  if p_month is null then raise exception 'Informe o mes.'; end if;
  v_month := date_trunc('month', p_month)::date;
  for v_rule in
    select * from public.recurring_payables
    where is_active and start_on < (v_month + interval '1 month')::date
      and (end_on is null or end_on >= v_month)
    for share
  loop
    v_due := make_date(extract(year from v_month)::integer, extract(month from v_month)::integer,
      least(v_rule.due_day,
        extract(day from (v_month + interval '1 month' - interval '1 day'))::integer));
    insert into public.payables
      (description, amount, due_on, account_id, category_id, recurring_payable_id, created_by)
    values (v_rule.description, v_rule.amount, v_due, v_rule.account_id,
      v_rule.category_id, v_rule.id, auth.uid())
    on conflict (recurring_payable_id, due_on) where recurring_payable_id is not null do nothing;
    get diagnostics v_inserted = row_count;
    v_count := v_count + v_inserted;
  end loop;
  return v_count;
end;
$$;

create function public.create_recurring_payable(
  p_description text, p_amount numeric, p_due_day integer, p_start_on date,
  p_end_on date, p_account_id uuid, p_category_id uuid
) returns uuid
language plpgsql security definer set search_path = ''
as $$
declare v_id uuid;
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  if p_description is null or char_length(trim(p_description)) not between 1 and 180 or
     p_amount is null or p_amount <= 0 or p_amount > 9999999999999999 or
     p_amount <> round(p_amount, 2) or p_due_day is null or p_due_day not between 1 and 31 or
     p_start_on is null or (p_end_on is not null and p_end_on < p_start_on) then
    raise exception 'Confira os dados da recorrencia.';
  end if;
  if not exists (select 1 from public.accounts where id = p_account_id) or
     not exists (select 1 from public.categories where id = p_category_id and kind = 'expense') then
    raise exception 'Conta ou categoria de saida nao encontrada.';
  end if;
  insert into public.recurring_payables
    (description, amount, due_day, start_on, end_on, account_id, category_id, created_by)
  values (trim(p_description), p_amount, p_due_day, p_start_on, p_end_on,
    p_account_id, p_category_id, auth.uid()) returning id into v_id;
  perform public.generate_recurring_payables(date_trunc('month', p_start_on)::date);
  return v_id;
end;
$$;

create function public.end_recurring_payable(p_recurring_payable_id uuid)
returns void
language plpgsql security definer set search_path = ''
as $$
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  update public.recurring_payables set is_active = false
    where id = p_recurring_payable_id and is_active;
  if not found then raise exception 'Recorrencia ativa nao encontrada.'; end if;
  delete from public.payables p where p.recurring_payable_id = p_recurring_payable_id
    and not exists (select 1 from public.payable_payments payment where payment.payable_id = p.id);
  if not exists (select 1 from public.payables where recurring_payable_id = p_recurring_payable_id) then
    delete from public.recurring_payables where id = p_recurring_payable_id;
  end if;
end;
$$;

create or replace function public.delete_payable(p_payable_id uuid)
returns void
language plpgsql security definer set search_path = ''
as $$
declare v_recurring_id uuid;
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  select recurring_payable_id into v_recurring_id from public.payables
    where id = p_payable_id and not is_deleted for update;
  if not found then raise exception 'Pendencia nao encontrada.'; end if;
  if exists (select 1 from public.payable_payments where payable_id = p_payable_id) then
    raise exception 'Desfaca o pagamento antes de excluir a pendencia.';
  end if;
  if v_recurring_id is null then
    delete from public.payables where id = p_payable_id;
  elsif exists (select 1 from public.recurring_payables where id = v_recurring_id and is_active) then
    update public.payables set is_deleted = true where id = p_payable_id;
  else
    delete from public.payables where id = p_payable_id;
    if not exists (select 1 from public.payables where recurring_payable_id = v_recurring_id) then
      delete from public.recurring_payables where id = v_recurring_id;
    end if;
  end if;
end;
$$;

create or replace function public.pay_payable(p_payable_id uuid, p_paid_on date)
returns uuid
language plpgsql security definer set search_path = ''
as $$
declare
  v_payable public.payables%rowtype;
  v_payment_id uuid;
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  if p_paid_on is null then raise exception 'Informe a data de pagamento.'; end if;
  select * into v_payable from public.payables where id = p_payable_id and not is_deleted for update;
  if not found then raise exception 'Pendencia nao encontrada.'; end if;
  if exists (select 1 from public.payable_payments where payable_id = p_payable_id) then
    raise exception 'Esta pendencia ja foi paga.';
  end if;
  insert into public.payable_payments (payable_id, account_id, amount, paid_on, created_by)
  values (p_payable_id, v_payable.account_id, v_payable.amount, p_paid_on, auth.uid())
  returning id into v_payment_id;
  return v_payment_id;
end;
$$;

revoke all on function public.generate_recurring_payables(date) from public;
revoke all on function public.create_recurring_payable(text, numeric, integer, date, date, uuid, uuid) from public;
revoke all on function public.end_recurring_payable(uuid) from public;
grant execute on function public.generate_recurring_payables(date) to authenticated;
grant execute on function public.create_recurring_payable(text, numeric, integer, date, date, uuid, uuid) to authenticated;
grant execute on function public.end_recurring_payable(uuid) to authenticated;
