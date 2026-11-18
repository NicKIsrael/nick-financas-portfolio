# Plano de implantacao web — Nick FinanÃ§as

Atualizado em 18/10/2026.

## Objetivo e decisoes

- Substituir o uso cotidiano do aplicativo desktop por uma aplicacao web confortavel no iPhone e no computador.
- Dois usuarios, cada um com seu proprio login, compartilham 100% dos dados financeiros.
- Existe apenas um conjunto de contas, cartoes, categorias, lancamentos, reservas e objetivos. Nao havera seletor ou cadastro de espacos nesta fase.
- Iniciar com banco novo. Nao utilizar dados do aplicativo desktop nem a planilha Excel no desenvolvimento ou nos testes.
- Usar apenas dados ficticios nos ambientes de desenvolvimento e teste. Antes do uso real, apagar esses dados e cadastrar as informacoes iniciais diretamente no site.
- Meta de custo recorrente: R$ 0. Um dominio proprio e opcional.
- Uso online na primeira entrega. Instalacao na tela inicial do iPhone entra na primeira versao; operacao offline fica para uma etapa posterior.

## Arquitetura proposta

- **Interface:** Next.js com TypeScript, layout responsivo e recursos de web app instalavel.
- **Hospedagem:** um projeto em uma conta pessoal Vercel Hobby, para uso nao comercial.
- **Dados e login:** um projeto em uma conta Supabase Free (Postgres e Auth). Os dois logins de os dois usuÃ¡rios sao usuarios do site, nao contas administrativas da Vercel ou do Supabase.
- **Acesso:** apenas dois usuarios previamente autorizados; cadastro publico desativado.
- **Seguranca:** permissoes no banco por usuario autenticado, verificacao de entrada em todas as operacoes, segredos somente no servidor e HTTPS da hospedagem.
- **Regras financeiras:** funcoes transacionais no banco ou rotas de servidor quando uma operacao alterar varios registros, como transferencias, faturas e reservas. Valores monetarios em `numeric`, nunca em ponto flutuante.
- **Codigo:** nova aplicacao web em `web/`, mantendo o aplicativo desktop como referencia funcional durante a construcao.

## Sequencia de implantacao

### Estado em 18/10/2026

- Aplicacao Next.js criada em `web/`, com layout responsivo e identidade visual do financeiro.
- Painel mensal, contas, categorias, receitas, despesas e transferencias entre contas funcionam em demonstracao local com dados ficticios descartaveis.
- Esquema SQL inicial e politicas de acesso para dois usuarios autorizados preparados em `web/supabase/migrations/`.
- Compilacao de producao, analise estatica e teste visual no navegador concluidos.
- Projeto Supabase Free criado, migracoes iniciais aplicadas e acesso limitado aos dois e-mails convidados. Cadastro publico desativado.
- Projeto Vercel `nickfinancas` publicado em https://nickfinancas.vercel.app com as variaveis publicas do Supabase; leitura anonima da tabela de contas foi negada na verificacao da API.
- Alex concluiu o convite e confirmou acesso ao painel apos recarregar. Bia tambem entrou e confirmou que o site funciona no aparelho dela.
- Alex validou criacao, calculo, persistencia apos recarga e exclusao de conta, categoria e lancamento com dados ficticios; todos os registros de teste foram removidos.
- Edicao de lancamentos e transferencias implementada e publicada; a terceira migracao SQL foi aplicada. Alex alterou uma despesa ficticia de R$ 10 para R$ 15, confirmou o saldo de R$ 85 apos recarregar e removeu os dados de teste.
- Alex confirmou que o painel, os menus e o botao de novo lancamento funcionam pelo icone instalado no iPhone.
- Cartoes, parcelas e faturas foram implementados. A quarta migracao SQL foi aplicada e a tela publicada. Alex validou compra parcelada, faturas de meses diferentes, pagamento, saldo apos recarga e limpeza de todos os dados ficticios.
- Alex confirmou que os campos no iPhone deixaram de ampliar a tela automaticamente.
- Pendencias e parcelamentos publicados apos aplicar a quinta migracao SQL. Alex confirmou o teste de vencimentos, pagamento e limpeza em producao; a suspeita de falha na lixeira de contas e categorias foi descartada apos nova verificacao.
- Recorrencias mensais implementadas com geracao ao abrir o mes, exclusao de uma ocorrencia sem recriacao e encerramento da regra sem apagar pagamentos feitos. Sexta migracao aplicada e versao publicada. Alex confirmou o teste completo em producao e removeu os dados ficticios.
- Reservas com aportes, retiradas, transferencias e historico publicadas apos aplicar a setima migracao. Alex confirmou os saldos apos recarga e removeu os dados ficticios do teste.
- Objetivos com metas, vinculo opcional a reservas, progresso e previsao de conclusao publicados apos aplicar a oitava migracao. Alex validou o fluxo em producao e removeu os dados ficticios.
- Relatorios dos ultimos seis meses, categorias, contas e faturas pagas com detalhamento dos registros publicados e validados em producao com dados ficticios.
- os dois usuÃ¡rios confirmaram que o mesmo registro ficticio apareceu nos dois celulares apos recarregar.

### 1. Fundacao local

1. Criar a aplicacao web, estrutura de rotas, tema e componentes responsivos inspirados na referencia visual aprovada.
2. Definir o modelo de dados novo, com um unico conjunto financeiro e autoria em cada alteracao.
3. Criar migracoes SQL versionadas, dados ficticios descartaveis de desenvolvimento e testes das regras financeiras essenciais.
4. Preparar configuracao local sem credenciais no repositorio.

**Pronto quando:** o projeto inicia localmente, a navegacao funciona no celular e o esquema pode ser recriado a partir das migracoes.

### 2. Acesso seguro

1. Configurar os dois logins e o convite da segunda pessoa.
2. Impedir cadastro publico e acesso anonimo aos dados.
3. Proteger todas as tabelas expostas com Row Level Security e privilegios minimos.
4. Testar leitura e escrita como cada usuario e negar acesso sem sessao.

**Pronto quando:** os dois entram separadamente e veem os mesmos dados; visitantes nao conseguem consultar nem alterar informacoes financeiras.

### 3. Nucleo utilizavel

1. Cadastrar contas e saldos iniciais, categorias e cartoes.
2. Criar, editar, excluir, pesquisar e filtrar receitas, despesas e transferencias.
3. Montar visao geral mensal e extrato com saldos calculados.
4. Priorizar formularios e navegacao para iPhone, inclusive teclado numerico, datas e confirmacoes claras.

**Pronto quando:** o casal consegue registrar a rotina financeira pelo celular e conferir o mesmo resultado no computador.

### 4. Paridade funcional

1. Pendencias, parcelamentos, recorrencias, faturas e pagamento de cartao.
2. Reservas, aportes, retiradas e objetivos.
3. Calendario, planejamento e relatorios com acesso aos lancamentos que compoem os totais.
4. Importacao de CSV bancario apenas se continuar util no fluxo de voces.

**Pronto quando:** os fluxos usados no aplicativo ou na planilha foram reproduzidos e conferidos com cenarios de teste representativos.

### 5. Publicacao e adocao

1. Criar ou conectar as contas Vercel e Supabase, aplicar migracoes e configurar variaveis de ambiente.
2. Publicar em subdominio gratuito da Vercel e validar login, permissoes e operacoes financeiras em producao.
3. Testar nos dois iPhones e computadores; instalar o site pela tela inicial do Safari.
4. Definir como exportar periodicamente os dados novos e testar uma restauracao em ambiente separado.
5. Acompanhar cotas gratuitas e erros nas primeiras semanas.

**Pronto quando:** os dois conseguem usar o site diariamente, os valores conferem e existe um caminho testado para recuperar os dados criados na web.

## Andamento em producao

- Contas, categorias, lancamentos, transferencias, cartoes, pendencias, recorrencias, reservas, objetivos e relatorios foram publicados e validados com dados ficticios por Alex. Os dados de teste foram removidos.
- O calendario foi publicado e validado no iPhone, inclusive abertura de pendencia na data escolhida; os dados ficticios foram removidos.
- O planejamento foi publicado e validado com saldo previsto, pendencia e aporte planejado. Os dados ficticios foram removidos.
- A exportacao JSON das 12 tabelas financeiras foi publicada. Alex baixou o arquivo, conferiu contas, categorias e lancamentos ficticios e limpou os dados. No segundo projeto Supabase Free (`nickfinancas-restore-test`), Alex aplicou o esquema, autorizou um usuario, restaurou uma amostra ficticia com registros nas 12 tabelas e confirmou as contagens, saldo bancario R$ 90, reservado R$ 20 e disponivel R$ 70. O teste nao usou o arquivo baixado no dispositivo.
- Bia confirmou que o site funciona no aparelho dela. Os dois confirmaram que um mesmo registro ficticio aparece nos dois celulares apos recarregar.

## Responsabilidades e pontos de contato

**Codex:** implementar a aplicacao, banco, migracoes, testes, configuracao, documentacao e preparacao da publicacao; explicar e executar as etapas tecnicas possiveis neste ambiente.

**Alex:** criar ou liberar acesso a uma conta Vercel e uma conta Supabase quando isso exigir identidade, login ou verificacao pessoal; informar os dois e-mails de acesso ao site; testar a experiencia real no iPhone junto com Bia; cadastrar os dados reais diretamente no site quando decidirem iniciar o uso. Qualquer eventual plano pago ou dominio proprio sera decidido antes da contratacao.

## Custo e limites

- Vercel Hobby e Supabase Free permitem comecar sem mensalidade, sujeitos as cotas e regras vigentes na data da publicacao.
- O Supabase Free pode pausar projetos pouco usados e nao fornece backup do banco para download pelo painel. Por isso, a exportacao periodica dos dados criados no site faz parte da implantacao, mesmo sem migracao historica.
- Um dominio proprio, recursos pagos ou funcionamento offline completo nao sao requisitos para colocar a primeira versao em uso.

## Referencias oficiais consultadas

- Vercel Hobby: https://vercel.com/docs/plans/hobby
- Supabase Free: https://supabase.com/docs/guides/platform/billing-on-supabase
- Supabase, seguranca e RLS: https://supabase.com/docs/guides/database/postgres/row-level-security
- Supabase, checklist de producao: https://supabase.com/docs/guides/deployment/going-into-prod
- Apple, site como app no iPhone: https://support.apple.com/guide/iphone/open-as-web-app-iphea86e5236/27/ios/27
