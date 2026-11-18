-- Objetivos vinculados opcionalmente a reservas. Execute depois de 202610180007_reserves.sql.

create table public.goals (
  id uuid primary key default gen_random_uuid(),
  name text not null check (char_length(trim(name)) between 1 and 120),
  target_amount numeric(18,2) not null check (target_amount > 0),
  target_on date,
  planned_monthly_contribution numeric(18,2) not null default 0
    check (planned_monthly_contribution >= 0),
  reserve_id uuid references public.reserves(id) on delete restrict,
  is_primary boolean not null default false,
  created_by uuid not null references auth.users(id),
  created_at timestamptz not null default now()
);

create unique index goals_one_primary_idx on public.goals (is_primary) where is_primary;
create index goals_reserve_idx on public.goals (reserve_id);

alter table public.goals enable row level security;
revoke all on public.goals from anon, authenticated;
grant select on public.goals to authenticated;
create policy "Allowed users read goals" on public.goals for select to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));

create function public.save_goal(
  p_goal_id uuid, p_name text, p_target_amount numeric, p_target_on date,
  p_planned_monthly_contribution numeric, p_reserve_id uuid
) returns uuid
language plpgsql security definer set search_path = ''
as $$
declare v_id uuid;
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  if p_name is null or char_length(trim(p_name)) not between 1 and 120 or
     p_target_amount is null or p_target_amount <= 0 or p_target_amount > 9999999999999999 or
     p_target_amount <> round(p_target_amount, 2) or
     p_planned_monthly_contribution is null or p_planned_monthly_contribution < 0 or
     p_planned_monthly_contribution > 9999999999999999 or
     p_planned_monthly_contribution <> round(p_planned_monthly_contribution, 2) then
    raise exception 'Confira os dados do objetivo.';
  end if;
  if p_reserve_id is not null and not exists (select 1 from public.reserves where id = p_reserve_id) then
    raise exception 'Reserva vinculada nao encontrada.';
  end if;
  if p_goal_id is null then
    perform pg_catalog.pg_advisory_xact_lock(2026101808);
    insert into public.goals
      (name, target_amount, target_on, planned_monthly_contribution, reserve_id, is_primary, created_by)
    values (trim(p_name), p_target_amount, p_target_on, p_planned_monthly_contribution,
      p_reserve_id, not exists (select 1 from public.goals), auth.uid())
    returning id into v_id;
  else
    update public.goals set name = trim(p_name), target_amount = p_target_amount,
      target_on = p_target_on, planned_monthly_contribution = p_planned_monthly_contribution,
      reserve_id = p_reserve_id where id = p_goal_id returning id into v_id;
    if not found then raise exception 'Objetivo nao encontrado.'; end if;
  end if;
  return v_id;
end;
$$;

create function public.set_primary_goal(p_goal_id uuid)
returns void
language plpgsql security definer set search_path = ''
as $$
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  perform pg_catalog.pg_advisory_xact_lock(2026101808);
  if not exists (select 1 from public.goals where id = p_goal_id) then
    raise exception 'Objetivo nao encontrado.';
  end if;
  update public.goals set is_primary = false where is_primary;
  update public.goals set is_primary = true where id = p_goal_id;
end;
$$;

create function public.delete_goal(p_goal_id uuid)
returns void
language plpgsql security definer set search_path = ''
as $$
declare v_was_primary boolean;
begin
  if auth.uid() is null or not exists (select 1 from public.allowed_users where user_id = auth.uid()) then
    raise exception 'Acesso nao autorizado.';
  end if;
  perform pg_catalog.pg_advisory_xact_lock(2026101808);
  delete from public.goals where id = p_goal_id returning is_primary into v_was_primary;
  if not found then raise exception 'Objetivo nao encontrado.'; end if;
  if v_was_primary then
    update public.goals set is_primary = true where id =
      (select id from public.goals order by target_on nulls last, name limit 1);
  end if;
end;
$$;

create or replace function public.delete_empty_reserve(p_reserve_id uuid)
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
  if exists (select 1 from public.goals where reserve_id = p_reserve_id) then
    raise exception 'Desvincule ou exclua o objetivo antes de remover a reserva.';
  end if;
  delete from public.reserves where id = p_reserve_id;
end;
$$;

revoke all on function public.save_goal(uuid, text, numeric, date, numeric, uuid) from public;
revoke all on function public.set_primary_goal(uuid) from public;
revoke all on function public.delete_goal(uuid) from public;
grant execute on function public.save_goal(uuid, text, numeric, date, numeric, uuid) to authenticated;
grant execute on function public.set_primary_goal(uuid) to authenticated;
grant execute on function public.delete_goal(uuid) to authenticated;
