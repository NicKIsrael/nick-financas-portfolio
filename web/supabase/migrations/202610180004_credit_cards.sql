-- Cartoes, parcelas e pagamentos de fatura para o conjunto financeiro compartilhado.
-- Compras nao movimentam a conta; o pagamento da fatura movimenta.

create table public.credit_cards (
  id uuid primary key default gen_random_uuid(),
  name text not null unique check (char_length(trim(name)) between 1 and 120),
  payment_account_id uuid not null references public.accounts(id) on delete restrict,
  credit_limit numeric(18,2) not null check (credit_limit > 0),
  spending_limit numeric(18,2) not null check (spending_limit > 0 and spending_limit <= credit_limit),
  closing_day integer not null check (closing_day between 1 and 31),
  due_day integer not null check (due_day between 1 and 31),
  created_at timestamptz not null default now()
);

create table public.card_charges (
  id uuid primary key default gen_random_uuid(),
  card_id uuid not null references public.credit_cards(id) on delete restrict,
  category_id uuid not null references public.categories(id) on delete restrict,
  description text not null check (char_length(trim(description)) between 1 and 180),
  amount numeric(18,2) not null check (amount > 0),
  purchased_on date not null,
  invoice_month date not null check (extract(day from invoice_month) = 1),
  purchase_group_id uuid not null,
  installment_number integer not null check (installment_number between 1 and 36),
  installment_count integer not null check (installment_count between 1 and 36 and installment_number <= installment_count),
  created_by uuid not null references auth.users(id),
  created_at timestamptz not null default now(),
  unique (purchase_group_id, installment_number)
);

create index card_charges_invoice_idx on public.card_charges (card_id, invoice_month);

create table public.card_payments (
  id uuid primary key default gen_random_uuid(),
  card_id uuid not null references public.credit_cards(id) on delete restrict,
  invoice_month date not null check (extract(day from invoice_month) = 1),
  account_id uuid not null references public.accounts(id) on delete restrict,
  amount numeric(18,2) not null check (amount > 0),
  paid_on date not null,
  created_by uuid not null references auth.users(id),
  created_at timestamptz not null default now(),
  unique (card_id, invoice_month)
);

create index card_payments_account_idx on public.card_payments (account_id);

alter table public.credit_cards enable row level security;
alter table public.card_charges enable row level security;
alter table public.card_payments enable row level security;

revoke all on public.credit_cards, public.card_charges, public.card_payments from anon, authenticated;
grant select, insert, delete on public.credit_cards to authenticated;
grant select on public.card_charges, public.card_payments to authenticated;

create policy "Allowed users read cards" on public.credit_cards for select to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));
create policy "Allowed users create cards" on public.credit_cards for insert to authenticated
  with check (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));
create policy "Allowed users delete empty cards" on public.credit_cards for delete to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));
create policy "Allowed users read card charges" on public.card_charges for select to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));
create policy "Allowed users read card payments" on public.card_payments for select to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));

create function public.create_card_purchase(
  p_card_id uuid, p_category_id uuid, p_description text,
  p_amount numeric, p_purchased_on date, p_installments integer default 1
) returns uuid
language plpgsql security definer set search_path = ''
as $$
declare
  v_card public.credit_cards%rowtype;
  v_used numeric;
  v_first_invoice date;
  v_group uuid := gen_random_uuid();
  v_cents bigint;
  v_base bigint;
  v_extra bigint;
  v_index integer;
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  if p_description is null or char_length(trim(p_description)) not between 1 and 180 or
     p_amount is null or p_amount <= 0 or p_amount > 9999999999999999 or
     p_amount <> round(p_amount, 2) or p_purchased_on is null or
     p_installments is null or p_installments not between 1 and 36 then
    raise exception 'Confira os dados da compra.';
  end if;
  select * into v_card from public.credit_cards where id = p_card_id for update;
  if not found then raise exception 'Cartao nao encontrado.'; end if;
  if not exists (select 1 from public.categories where id = p_category_id and kind = 'expense') then
    raise exception 'Selecione uma categoria de saida.';
  end if;
  select coalesce((select sum(amount) from public.card_charges where card_id = p_card_id), 0)
       - coalesce((select sum(amount) from public.card_payments where card_id = p_card_id), 0)
    into v_used;
  if v_used + p_amount > v_card.spending_limit then
    raise exception 'A compra ultrapassa o limite disponivel do cartao.';
  end if;
  v_first_invoice := date_trunc('month', p_purchased_on)::date;
  if extract(day from p_purchased_on) > v_card.closing_day then
    v_first_invoice := (v_first_invoice + interval '1 month')::date;
  end if;
  if v_card.due_day <= v_card.closing_day then
    v_first_invoice := (v_first_invoice + interval '1 month')::date;
  end if;
  v_cents := (p_amount * 100)::bigint;
  v_base := v_cents / p_installments;
  v_extra := v_cents % p_installments;
  if v_base = 0 then raise exception 'Cada parcela deve ser maior que zero.'; end if;
  for v_index in 1..p_installments loop
    if exists (
      select 1 from public.card_payments
      where card_id = p_card_id and invoice_month = (v_first_invoice + make_interval(months => v_index - 1))::date
    ) then raise exception 'Uma das faturas desta compra ja foi paga.'; end if;
    insert into public.card_charges
      (card_id, category_id, description, amount, purchased_on, invoice_month,
       purchase_group_id, installment_number, installment_count, created_by)
    values
      (p_card_id, p_category_id, trim(p_description),
       (v_base + case when v_index <= v_extra then 1 else 0 end)::numeric / 100,
       p_purchased_on, (v_first_invoice + make_interval(months => v_index - 1))::date,
       v_group, v_index, p_installments, auth.uid());
  end loop;
  return v_group;
end;
$$;

create function public.pay_card_invoice(p_card_id uuid, p_invoice_month date, p_paid_on date)
returns uuid
language plpgsql security definer set search_path = ''
as $$
declare
  v_card public.credit_cards%rowtype;
  v_total numeric;
  v_payment_id uuid;
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  if p_invoice_month is null or extract(day from p_invoice_month) <> 1 or p_paid_on is null then
    raise exception 'Informe a fatura e a data de pagamento.';
  end if;
  select * into v_card from public.credit_cards where id = p_card_id for update;
  if not found then raise exception 'Cartao nao encontrado.'; end if;
  if exists (select 1 from public.card_payments where card_id = p_card_id and invoice_month = p_invoice_month) then
    raise exception 'Esta fatura ja foi paga.';
  end if;
  select coalesce(sum(amount), 0) into v_total from public.card_charges
    where card_id = p_card_id and invoice_month = p_invoice_month;
  if v_total <= 0 then raise exception 'Esta fatura nao possui compras.'; end if;
  insert into public.card_payments (card_id, invoice_month, account_id, amount, paid_on, created_by)
  values (p_card_id, p_invoice_month, v_card.payment_account_id, v_total, p_paid_on, auth.uid())
  returning id into v_payment_id;
  return v_payment_id;
end;
$$;

create function public.delete_card_purchase(p_purchase_group_id uuid)
returns void
language plpgsql security definer set search_path = ''
as $$
declare v_card_id uuid;
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  select card_id into v_card_id from public.card_charges where purchase_group_id = p_purchase_group_id limit 1;
  if v_card_id is null then raise exception 'Compra nao encontrada.'; end if;
  perform 1 from public.credit_cards where id = v_card_id for update;
  if exists (
    select 1 from public.card_charges c join public.card_payments p
      on p.card_id = c.card_id and p.invoice_month = c.invoice_month
    where c.purchase_group_id = p_purchase_group_id
  ) then raise exception 'Desfaca o pagamento da fatura antes de excluir a compra.'; end if;
  delete from public.card_charges where purchase_group_id = p_purchase_group_id;
end;
$$;

create function public.undo_card_invoice_payment(p_card_id uuid, p_invoice_month date)
returns void
language plpgsql security definer set search_path = ''
as $$
declare
  v_card public.credit_cards%rowtype;
  v_payment public.card_payments%rowtype;
  v_used numeric;
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  select * into v_card from public.credit_cards where id = p_card_id for update;
  if not found then raise exception 'Cartao nao encontrado.'; end if;
  select * into v_payment from public.card_payments
    where card_id = p_card_id and invoice_month = p_invoice_month;
  if not found then raise exception 'Pagamento nao encontrado.'; end if;
  select coalesce((select sum(amount) from public.card_charges where card_id = p_card_id), 0)
       - coalesce((select sum(amount) from public.card_payments where card_id = p_card_id), 0)
    into v_used;
  if v_used + v_payment.amount > v_card.spending_limit then
    raise exception 'Nao e possivel desfazer: o limite ficaria ultrapassado.';
  end if;
  delete from public.card_payments where id = v_payment.id;
end;
$$;

revoke all on function public.create_card_purchase(uuid, uuid, text, numeric, date, integer) from public;
revoke all on function public.pay_card_invoice(uuid, date, date) from public;
revoke all on function public.delete_card_purchase(uuid) from public;
revoke all on function public.undo_card_invoice_payment(uuid, date) from public;
grant execute on function public.create_card_purchase(uuid, uuid, text, numeric, date, integer) to authenticated;
grant execute on function public.pay_card_invoice(uuid, date, date) to authenticated;
grant execute on function public.delete_card_purchase(uuid) to authenticated;
grant execute on function public.undo_card_invoice_payment(uuid, date) to authenticated;
