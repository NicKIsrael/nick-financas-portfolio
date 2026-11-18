# Nick FinanÃ§as — web

Aplicação web com painel mensal, contas, categorias, lançamentos de receitas e despesas, transferências, cartões de crédito, pendências, recorrências mensais, reservas, objetivos, relatórios, calendário e planejamento de seis meses. Lançamentos e transferências podem ser editados ou excluídos. Compras no cartão podem ser parceladas, entram na fatura de vencimento e só reduzem o saldo da conta quando a fatura é paga. Pendências e recorrências só reduzem o saldo quando são pagas. Aportes e retiradas de reservas alteram o saldo disponível sem criar despesas.

## Rodar localmente

```powershell
npm install
npm run dev
```

Sem `.env.local`, o ambiente de desenvolvimento apresenta dados fictícios em memória. Mudanças feitas nesse modo são perdidas ao recarregar a página. A compilação de produção sem Supabase configurado mostra apenas uma tela de configuração pendente; nunca publica os dados fictícios.

## Conectar Supabase

1. Criar um projeto Supabase Free.
2. Executar, em ordem, os arquivos `supabase/migrations/202610180001_initial.sql` a `supabase/migrations/202610180008_goals.sql` no SQL Editor.
3. No painel **Connect** ou **Settings > API Keys**, obter a URL e a chave `sb_publishable_...`. Copiar `.env.example` para `.env.local` e preencher as duas variáveis.
4. Em **Authentication**, desabilitar cadastro público. Configurar **Site URL** com a URL de produção da Vercel antes de enviar os convites.
5. Em **Authentication > Users**, convide as pessoas que terão acesso ao sistema e copie os UUIDs criados.
6. Inserir os dois UUIDs na tabela `public.allowed_users` usando o SQL Editor (exemplo no final da migração).
7. Cada pessoa abre o próprio convite e define sua senha pelo site. Validar o acesso de ambas em navegadores separados e negar acesso a uma conta não autorizada.

Não colocar chave `service_role` no cliente ou em variáveis `NEXT_PUBLIC_*`.

## Cópia dos dados da web

No site conectado, **Mais > Baixar cópia dos dados** gera um arquivo JSON com todas as linhas das 12 tabelas financeiras, inclusive pendências removidas e recorrências encerradas. O arquivo fica no dispositivo de quem o baixou e contém informações financeiras e IDs de autoria; guarde-o em local privado. A exportação não inclui senhas, chaves do Supabase nem a lista de usuários autorizados. Alex conferiu o download com dados fictícios. Uma amostra separada, também fictícia, foi restaurada em outro projeto Supabase; contagens e saldos conferiram. O arquivo baixado no dispositivo ainda não foi usado em um teste de restauração.

## Implantação

O projeto Vercel Hobby `nickfinancas` está vinculado a este diretório e publicado em https://nickfinancas.vercel.app. As duas variáveis públicas de `.env.example` estão configuradas em Production. Alex validou o painel, criação, edição, recarga e exclusão de dados fictícios, o fluxo de cartões e faturas e o acesso pelo ícone instalado no iPhone. Bia também confirmou acesso e funcionamento no aparelho dela. Os dois confirmaram que um mesmo registro fictício aparece nos dois celulares após recarregar. A quinta migração foi aplicada e a Agenda publicada. Alex confirmou que o teste de pendências, pagamento e exclusão funcionou. A sexta migração foi aplicada e as recorrências publicadas. Alex confirmou geração mensal, exclusão de uma ocorrência sem recriação, pagamento, saldo, encerramento da regra e limpeza dos dados fictícios. A sétima migração foi aplicada e reservas foram publicadas. Alex validou aporte, retirada, transferência, saldo disponível após recarga e limpeza dos dados fictícios. A oitava migração foi aplicada e os objetivos foram publicados. Alex validou progresso, previsão, edição, proteção da reserva vinculada e limpeza dos dados fictícios.

Alex validou os relatórios em produção com entradas, saídas, resultado, detalhamento por categoria e conta, acesso ao lançamento e recarga; os dados fictícios foram removidos. O calendário reúne lançamentos, vencimentos de pendências e faturas, além dos pagamentos, e permite agendar uma pendência na data selecionada. Alex validou esse fluxo no iPhone e limpou os dados fictícios. O planejamento de seis meses foi publicado e validado com saldo previsto, pendência, aporte planejado e recarga; os dados fictícios foram removidos.
