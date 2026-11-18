-- Permitir aos dois usuarios editar os lancamentos compartilhados.
-- A autoria original (created_by) nao pode ser alterada pelo cliente.

grant update (account_id, category_id, destination_account_id, kind, description, amount, occurred_on)
  on public.transactions to authenticated;

create policy "Allowed users edit transactions"
  on public.transactions for update to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())))
  with check (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));
