-- Pendencias e parcelas. O saldo da conta muda apenas no pagamento.

create table public.payables (
  id uuid primary key default gen_random_uuid(),
  description text not null check (char_length(trim(description)) between 1 and 180),
  amount numeric(18,2) not null check (amount > 0),
  due_on date not null,
  account_id uuid not null references public.accounts(id) on delete restrict,
  category_id uuid not null references public.categories(id) on delete restrict,
  installment_group_id uuid,
  installment_number integer,
  installment_count integer,
  created_by uuid not null references auth.users(id),
  created_at timestamptz not null default now(),
  check (
    (installment_group_id is null and installment_number is null and installment_count is null)
    or (installment_group_id is not null and installment_count between 2 and 60 and
        installment_number between 1 and installment_count)
  ),
  unique (installment_group_id, installment_number)
);

create index payables_due_on_idx on public.payables (due_on);

create table public.payable_payments (
  id uuid primary key default gen_random_uuid(),
  payable_id uuid not null unique references public.payables(id) on delete restrict,
  account_id uuid not null references public.accounts(id) on delete restrict,
  amount numeric(18,2) not null check (amount > 0),
  paid_on date not null,
  created_by uuid not null references auth.users(id),
  created_at timestamptz not null default now()
);

create index payable_payments_account_idx on public.payable_payments (account_id);
create index payable_payments_paid_on_idx on public.payable_payments (paid_on);

alter table public.payables enable row level security;
alter table public.payable_payments enable row level security;
revoke all on public.payables, public.payable_payments from anon, authenticated;
grant select on public.payables, public.payable_payments to authenticated;

create policy "Allowed users read payables" on public.payables for select to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));
create policy "Allowed users read payable payments" on public.payable_payments for select to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));

create function public.create_payables(
  p_description text, p_amount numeric, p_due_on date, p_account_id uuid,
  p_category_id uuid, p_installment_count integer default 1,
  p_installment_start integer default 1
) returns uuid
language plpgsql security definer set search_path = ''
as $$
declare
  v_group uuid;
  v_index integer;
  v_month date;
  v_due date;
  v_first_id uuid;
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  if p_description is null or char_length(trim(p_description)) not between 1 and 180 or
     p_amount is null or p_amount <= 0 or p_amount > 9999999999999999 or
     p_amount <> round(p_amount, 2) or p_due_on is null or
     p_installment_count is null or p_installment_count not between 1 and 60 or
     p_installment_start is null or p_installment_start not between 1 and p_installment_count then
    raise exception 'Confira os dados da pendencia.';
  end if;
  if not exists (select 1 from public.accounts where id = p_account_id) or
     not exists (select 1 from public.categories where id = p_category_id and kind = 'expense') then
    raise exception 'Conta ou categoria de saida nao encontrada.';
  end if;
  if p_installment_count > 1 then v_group := gen_random_uuid(); end if;
  for v_index in p_installment_start..p_installment_count loop
    v_month := (date_trunc('month', p_due_on)::date +
                make_interval(months => v_index - p_installment_start))::date;
    v_due := make_date(extract(year from v_month)::integer, extract(month from v_month)::integer,
      least(extract(day from p_due_on)::integer,
        extract(day from (v_month + interval '1 month' - interval '1 day'))::integer));
    insert into public.payables
      (description, amount, due_on, account_id, category_id, installment_group_id,
       installment_number, installment_count, created_by)
    values (trim(p_description), p_amount, v_due, p_account_id, p_category_id,
      v_group, case when v_group is null then null else v_index end,
      case when v_group is null then null else p_installment_count end, auth.uid())
    returning id into v_first_id;
  end loop;
  return coalesce(v_group, v_first_id);
end;
$$;

create function public.pay_payable(p_payable_id uuid, p_paid_on date)
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
  select * into v_payable from public.payables where id = p_payable_id for update;
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

create function public.undo_payable_payment(p_payable_id uuid)
returns void
language plpgsql security definer set search_path = ''
as $$
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  perform 1 from public.payables where id = p_payable_id for update;
  if not found then raise exception 'Pendencia nao encontrada.'; end if;
  if not exists (select 1 from public.payable_payments where payable_id = p_payable_id) then
    raise exception 'Pagamento nao encontrado.';
  end if;
  delete from public.payable_payments where payable_id = p_payable_id;
end;
$$;

create function public.delete_payable(p_payable_id uuid)
returns void
language plpgsql security definer set search_path = ''
as $$
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  perform 1 from public.payables where id = p_payable_id for update;
  if not found then raise exception 'Pendencia nao encontrada.'; end if;
  if exists (select 1 from public.payable_payments where payable_id = p_payable_id) then
    raise exception 'Desfaca o pagamento antes de excluir a pendencia.';
  end if;
  delete from public.payables where id = p_payable_id;
end;
$$;

create function public.delete_unpaid_installments(p_installment_group_id uuid)
returns integer
language plpgsql security definer set search_path = ''
as $$
declare
  v_count integer;
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  if p_installment_group_id is null then raise exception 'Grupo de parcelas invalido.'; end if;
  perform 1 from public.payables where installment_group_id = p_installment_group_id for update;
  delete from public.payables p where p.installment_group_id = p_installment_group_id
    and not exists (select 1 from public.payable_payments payment where payment.payable_id = p.id);
  get diagnostics v_count = row_count;
  if v_count = 0 then raise exception 'Nenhuma parcela pendente encontrada.'; end if;
  return v_count;
end;
$$;

revoke all on function public.create_payables(text, numeric, date, uuid, uuid, integer, integer) from public;
revoke all on function public.pay_payable(uuid, date) from public;
revoke all on function public.undo_payable_payment(uuid) from public;
revoke all on function public.delete_payable(uuid) from public;
revoke all on function public.delete_unpaid_installments(uuid) from public;
grant execute on function public.create_payables(text, numeric, date, uuid, uuid, integer, integer) to authenticated;
grant execute on function public.pay_payable(uuid, date) to authenticated;
grant execute on function public.undo_payable_payment(uuid) to authenticated;
grant execute on function public.delete_payable(uuid) to authenticated;
grant execute on function public.delete_unpaid_installments(uuid) to authenticated;
