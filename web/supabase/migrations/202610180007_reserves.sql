-- Reservas e movimentos. Execute depois de 202610180006_recurring_payables.sql.
-- Reservar separa dinheiro do saldo disponivel; nao altera o saldo bancario nem cria despesa.

create table public.reserves (
  id uuid primary key default gen_random_uuid(),
  name text not null unique check (char_length(trim(name)) between 1 and 120),
  created_by uuid not null references auth.users(id),
  created_at timestamptz not null default now()
);

create table public.reserve_movements (
  id uuid primary key default gen_random_uuid(),
  reserve_id uuid not null references public.reserves(id) on delete restrict,
  account_id uuid not null references public.accounts(id) on delete restrict,
  amount numeric(18,2) not null check (amount > 0),
  kind text not null check (kind in ('deposit', 'withdrawal')),
  occurred_on date not null,
  transfer_group_id uuid,
  created_by uuid not null references auth.users(id),
  created_at timestamptz not null default now(),
  unique (transfer_group_id, reserve_id)
);

create index reserve_movements_reserve_idx on public.reserve_movements (reserve_id, occurred_on desc);
create index reserve_movements_account_idx on public.reserve_movements (account_id);

alter table public.reserves enable row level security;
alter table public.reserve_movements enable row level security;
revoke all on public.reserves, public.reserve_movements from anon, authenticated;
grant select on public.reserves, public.reserve_movements to authenticated;

create policy "Allowed users read reserves" on public.reserves for select to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));
create policy "Allowed users read reserve movements" on public.reserve_movements for select to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));

create function public.create_reserve(p_name text)
returns uuid
language plpgsql security definer set search_path = ''
as $$
declare v_id uuid;
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  if p_name is null or char_length(trim(p_name)) not between 1 and 120 then
    raise exception 'Informe o nome da reserva.';
  end if;
  insert into public.reserves (name, created_by) values (trim(p_name), auth.uid()) returning id into v_id;
  return v_id;
end;
$$;

create function public.rename_reserve(p_reserve_id uuid, p_name text)
returns void
language plpgsql security definer set search_path = ''
as $$
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  if p_name is null or char_length(trim(p_name)) not between 1 and 120 then
    raise exception 'Informe o nome da reserva.';
  end if;
  update public.reserves set name = trim(p_name) where id = p_reserve_id;
  if not found then raise exception 'Reserva nao encontrada.'; end if;
end;
$$;

create function public.delete_empty_reserve(p_reserve_id uuid)
returns void
language plpgsql security definer set search_path = ''
as $$
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  perform 1 from public.reserves where id = p_reserve_id for update;
  if not found then raise exception 'Reserva nao encontrada.'; end if;
  if exists (select 1 from public.reserve_movements where reserve_id = p_reserve_id) then
    raise exception 'Exclua os movimentos antes de remover a reserva.';
  end if;
  delete from public.reserves where id = p_reserve_id;
end;
$$;

create function public.move_reserve(
  p_reserve_id uuid, p_account_id uuid, p_amount numeric, p_kind text, p_occurred_on date
) returns uuid
language plpgsql security definer set search_path = ''
as $$
declare v_balance numeric(18,2); v_id uuid;
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  if p_amount is null or p_amount <= 0 or p_amount > 9999999999999999 or
     p_amount <> round(p_amount, 2) or p_kind not in ('deposit', 'withdrawal') or
     p_occurred_on is null then
    raise exception 'Confira os dados do movimento.';
  end if;
  perform 1 from public.reserves where id = p_reserve_id for update;
  if not found or not exists (select 1 from public.accounts where id = p_account_id) then
    raise exception 'Reserva ou conta nao encontrada.';
  end if;
  select coalesce(sum(case when kind = 'deposit' then amount else -amount end), 0)
    into v_balance from public.reserve_movements where reserve_id = p_reserve_id;
  if p_kind = 'withdrawal' and p_amount > v_balance then
    raise exception 'A retirada nao pode ser maior que o saldo da reserva.';
  end if;
  insert into public.reserve_movements
    (reserve_id, account_id, amount, kind, occurred_on, created_by)
  values (p_reserve_id, p_account_id, p_amount, p_kind, p_occurred_on, auth.uid())
  returning id into v_id;
  return v_id;
end;
$$;

create function public.transfer_reserve(
  p_source_id uuid, p_destination_id uuid, p_account_id uuid,
  p_amount numeric, p_occurred_on date
) returns uuid
language plpgsql security definer set search_path = ''
as $$
declare v_balance numeric(18,2); v_count integer; v_group uuid := gen_random_uuid();
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  if p_source_id is null or p_destination_id is null or p_source_id = p_destination_id or
     p_amount is null or p_amount <= 0 or p_amount > 9999999999999999 or
     p_amount <> round(p_amount, 2) or p_occurred_on is null then
    raise exception 'Confira os dados da transferencia.';
  end if;
  perform id from public.reserves where id in (p_source_id, p_destination_id)
    order by id for update;
  get diagnostics v_count = row_count;
  if v_count <> 2 or not exists (select 1 from public.accounts where id = p_account_id) then
    raise exception 'Reserva ou conta nao encontrada.';
  end if;
  select coalesce(sum(case when kind = 'deposit' then amount else -amount end), 0)
    into v_balance from public.reserve_movements where reserve_id = p_source_id;
  if p_amount > v_balance then
    raise exception 'A transferencia nao pode ser maior que o saldo da reserva de origem.';
  end if;
  insert into public.reserve_movements
    (reserve_id, account_id, amount, kind, occurred_on, transfer_group_id, created_by)
  values
    (p_source_id, p_account_id, p_amount, 'withdrawal', p_occurred_on, v_group, auth.uid()),
    (p_destination_id, p_account_id, p_amount, 'deposit', p_occurred_on, v_group, auth.uid());
  return v_group;
end;
$$;

create function public.delete_reserve_movement(p_movement_id uuid)
returns void
language plpgsql security definer set search_path = ''
as $$
declare
  v_movement public.reserve_movements%rowtype;
  v_balance numeric(18,2);
  v_reserve_ids uuid[];
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  select * into v_movement from public.reserve_movements where id = p_movement_id;
  if not found then raise exception 'Movimento nao encontrado.'; end if;
  if v_movement.transfer_group_id is null then
    v_reserve_ids := array[v_movement.reserve_id];
  else
    select array_agg(distinct reserve_id) into v_reserve_ids
      from public.reserve_movements where transfer_group_id = v_movement.transfer_group_id;
  end if;
  perform id from public.reserves where id = any(v_reserve_ids) order by id for update;
  select * into v_movement from public.reserve_movements where id = p_movement_id for update;
  if not found then raise exception 'Movimento nao encontrado.'; end if;
  if v_movement.transfer_group_id is not null then
    select coalesce(sum(case when kind = 'deposit' then amount else -amount end), 0)
      into v_balance from public.reserve_movements
      where reserve_id = (select reserve_id from public.reserve_movements
        where transfer_group_id = v_movement.transfer_group_id and kind = 'deposit');
    if v_balance < v_movement.amount then
      raise exception 'O valor transferido ja foi usado na reserva de destino.';
    end if;
    delete from public.reserve_movements where transfer_group_id = v_movement.transfer_group_id;
  else
    if v_movement.kind = 'deposit' then
      select coalesce(sum(case when kind = 'deposit' then amount else -amount end), 0)
        into v_balance from public.reserve_movements where reserve_id = v_movement.reserve_id;
      if v_balance < v_movement.amount then
        raise exception 'Este aporte ja foi usado por uma retirada ou transferencia.';
      end if;
    end if;
    delete from public.reserve_movements where id = p_movement_id;
  end if;
end;
$$;

revoke all on function public.create_reserve(text) from public;
revoke all on function public.rename_reserve(uuid, text) from public;
revoke all on function public.delete_empty_reserve(uuid) from public;
revoke all on function public.move_reserve(uuid, uuid, numeric, text, date) from public;
revoke all on function public.transfer_reserve(uuid, uuid, uuid, numeric, date) from public;
revoke all on function public.delete_reserve_movement(uuid) from public;
grant execute on function public.create_reserve(text) to authenticated;
grant execute on function public.rename_reserve(uuid, text) to authenticated;
grant execute on function public.delete_empty_reserve(uuid) to authenticated;
grant execute on function public.move_reserve(uuid, uuid, numeric, text, date) to authenticated;
grant execute on function public.transfer_reserve(uuid, uuid, uuid, numeric, date) to authenticated;
grant execute on function public.delete_reserve_movement(uuid) to authenticated;
