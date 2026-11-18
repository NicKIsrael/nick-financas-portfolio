-- Financeiro compartilhado: exatamente os usuarios cadastrados em allowed_users podem usar os dados.
-- Execute no SQL Editor do projeto Supabase. Depois, crie os dois usuarios em Auth e inclua seus UUIDs em allowed_users.

create table public.allowed_users (
  user_id uuid primary key references auth.users(id) on delete cascade,
  display_name text not null check (char_length(trim(display_name)) between 1 and 120),
  created_at timestamptz not null default now()
);

create table public.accounts (
  id uuid primary key default gen_random_uuid(),
  name text not null check (char_length(trim(name)) between 1 and 120),
  type text not null check (type in ('checking', 'savings', 'cash', 'investment')),
  initial_balance numeric(18,2) not null default 0,
  created_at timestamptz not null default now()
);

create table public.categories (
  id uuid primary key default gen_random_uuid(),
  name text not null check (char_length(trim(name)) between 1 and 80),
  kind text not null check (kind in ('income', 'expense')),
  created_at timestamptz not null default now(),
  unique (id, kind),
  unique (name, kind)
);

create table public.transactions (
  id uuid primary key default gen_random_uuid(),
  account_id uuid not null references public.accounts(id) on delete restrict,
  category_id uuid,
  destination_account_id uuid references public.accounts(id) on delete restrict,
  kind text not null check (kind in ('income', 'expense', 'transfer')),
  description text not null check (char_length(trim(description)) between 1 and 180),
  amount numeric(18,2) not null check (amount > 0),
  occurred_on date not null,
  created_by uuid not null default auth.uid() references auth.users(id),
  created_at timestamptz not null default now(),
  foreign key (category_id, kind) references public.categories(id, kind) on delete restrict,
  check (
    (kind = 'transfer' and category_id is null and destination_account_id is not null and destination_account_id <> account_id)
    or (kind in ('income', 'expense') and category_id is not null and destination_account_id is null)
  )
);

create index transactions_occurred_on_idx on public.transactions (occurred_on desc);
create index transactions_account_id_idx on public.transactions (account_id);
create index transactions_category_id_idx on public.transactions (category_id);
create index transactions_destination_account_id_idx on public.transactions (destination_account_id);

alter table public.allowed_users enable row level security;
alter table public.accounts enable row level security;
alter table public.categories enable row level security;
alter table public.transactions enable row level security;

revoke all on public.allowed_users, public.accounts, public.categories, public.transactions from anon, authenticated;
grant select on public.allowed_users to authenticated;
grant select, insert on public.accounts, public.categories to authenticated;
grant select, insert, delete on public.transactions to authenticated;

create policy "Users see their own access record"
  on public.allowed_users for select to authenticated
  using (user_id = (select auth.uid()));

create policy "Allowed users read accounts"
  on public.accounts for select to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));
create policy "Allowed users create accounts"
  on public.accounts for insert to authenticated
  with check (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));

create policy "Allowed users read categories"
  on public.categories for select to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));
create policy "Allowed users create categories"
  on public.categories for insert to authenticated
  with check (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));

create policy "Allowed users read transactions"
  on public.transactions for select to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));
create policy "Allowed users create their transactions"
  on public.transactions for insert to authenticated
  with check (
    created_by = (select auth.uid()) and
    exists (select 1 from public.allowed_users where user_id = (select auth.uid()))
  );
create policy "Allowed users delete transactions"
  on public.transactions for delete to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));

-- Exemplo para executar manualmente apos criar os usuarios no painel Auth:
-- insert into public.allowed_users (user_id, display_name) values
--   ('UUID_DO_USUARIO_A', 'Alex'), ('UUID_DO_USUARIO_B', 'Bia');
