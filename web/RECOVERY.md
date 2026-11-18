# Cópia e restauração dos dados da web

## Criar uma cópia

No site, abra **Mais > Baixar cópia dos dados**. O navegador baixa `nickfinancas-AAAA-MM-DD.json` com as 12 tabelas financeiras. Guarde o arquivo em um local privado. Ele contém valores, descrições e identificadores dos usuários que criaram registros. Ele não contém senhas ou chaves do Supabase.

A exportação lê todas as páginas das tabelas, incluindo pendências ocultas e recorrências encerradas. Faça uma nova cópia depois de mudanças importantes e confira que o arquivo foi salvo no dispositivo.

## Preparar uma restauração de teste

1. Crie um projeto Supabase **separado e vazio**. Nunca execute a restauração de teste sobre o projeto em uso.
2. No diretório `web`, execute `node scripts/prepare-schema.mjs supabase/restore-schema.local.sql`. O arquivo reúne as migrações em ordem. Execute esse SQL no SQL Editor do projeto de teste.
3. Crie um usuário em **Authentication > Users** e autorize seu UUID na tabela `public.allowed_users` como descrito no README. Os usuários não fazem parte do arquivo financeiro exportado.
4. Para o primeiro teste, execute `node scripts/create-restore-fixture.mjs supabase/restore-sample.local.json` e `node scripts/prepare-restore.mjs supabase/restore-sample.local.json supabase/restore-data.local.sql`. Essa amostra tem dados fictícios em todas as tabelas. Para restaurar uma cópia própria depois, substitua o primeiro caminho pelo JSON baixado do site.
5. Revise os dois arquivos gerados antes de executar. Execute `restore-data.local.sql` no SQL Editor do projeto de teste. O SQL só aceita tabelas financeiras vazias, mantém IDs e datas e roda em uma transação. Quando o usuário original existir no projeto de destino, preserva a autoria. Caso contrário, atribui a autoria ao primeiro usuário autorizado no novo projeto.
6. Execute `supabase/verify-restore.sql` no projeto de teste. Na amostra, devem aparecer 1 conta, 2 categorias, 2 lançamentos e 1 registro em cada uma das outras tabelas. O saldo bancário calculado é R$ 90 e o disponível, depois da reserva de R$ 20, é R$ 70. Em 18/10/2026, Alex confirmou todas essas contagens e saldos em um projeto Supabase separado. O teste usou a amostra gerada pelo script, não o arquivo exportado no dispositivo.

Os dois arquivos `.local.sql` são locais e ignorados pelo Git. O arquivo JSON deve ser mantido fora do repositório.
