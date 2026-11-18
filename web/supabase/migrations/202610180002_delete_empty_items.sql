-- Excluir contas e categorias sem lancamentos vinculados.
-- As chaves estrangeiras impedem a exclusao de itens em uso.

grant delete on public.accounts, public.categories to authenticated;

create policy "Allowed users delete accounts"
  on public.accounts for delete to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));

create policy "Allowed users delete categories"
  on public.categories for delete to authenticated
  using (exists (select 1 from public.allowed_users where user_id = (select auth.uid())));
