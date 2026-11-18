# Nick Finanças

Aplicação de finanças pessoais e compartilhadas, criada para organizar o dia a dia financeiro em uma experiência que funciona bem no computador e no celular. O projeto reúne uma versão web instalável (PWA) e um aplicativo desktop local-first.

> Projeto pessoal desenvolvido em colaboração com IA. Nicollas definiu prioridades e regras do produto, tomou decisões de uso e validou os fluxos no navegador e em iPhones. O Codex apoiou a implementação, a modelagem técnica, a documentação e a preparação da publicação.

> Este repositório é uma **cópia de portfólio** do projeto em uso. O código é o mesmo; os nomes reais dos usuários foram trocados por nomes fictícios (Alex e Bia), e o histórico de commits e os endereços da rede privada ficaram de fora.

**Site:** [nickfinancas.vercel.app](https://nickfinancas.vercel.app) · **Aplicação web:** [`web/`](web) · **Tecnologias:** Next.js, React, TypeScript, Supabase, PostgreSQL, .NET, Avalonia, SQLite

## Demonstração

A versão web está publicada em [nickfinancas.vercel.app](https://nickfinancas.vercel.app) e exige login autorizado, pois é usada em um ambiente pessoal. A demonstração local usa dados fictícios.

## O que o sistema faz

- Consolida receitas, despesas, transferências e saldos por mês.
- Organiza contas, categorias, cartões de crédito, compras parceladas e faturas.
- Acompanha pagamentos pendentes e recorrências mensais.
- Gerencia reservas, aportes, retiradas e objetivos financeiros.
- Oferece calendário de movimentações, planejamento dos próximos seis meses e relatórios por período, conta e categoria.
- Permite pesquisar, editar e excluir movimentações e consultar os registros que formam os totais.
- Compartilha os dados entre usuários autorizados na versão web, com autenticação e políticas de acesso no banco.
- Exporta uma cópia JSON dos dados financeiros para facilitar uma recuperação controlada.
- Inclui uma aplicação desktop local-first em Avalonia/.NET, com SQLite local e uma API de sincronização opcional.

## Regras financeiras em destaque

- Compras no cartão aparecem nas faturas; o saldo da conta muda quando a fatura é paga.
- Pendências e recorrências afetam o saldo quando são pagas.
- Aportes e retiradas movimentam o dinheiro reservado sem registrar uma despesa fictícia.
- Transferências movimentam valores entre contas sem contar como receita ou despesa.

## Demonstração e dados

Em desenvolvimento, sem configuração do Supabase, a aplicação web usa registros de exemplo em memória. Eles são fictícios e descartados ao recarregar a página. A versão de produção não publica esses dados de demonstração. O acesso ao serviço implantado é restrito aos usuários autorizados do projeto.

## Executar a aplicação web

Requer Node.js 24 ou compatível com a versão do Next.js definida no projeto.

```powershell
cd web
npm install
npm run dev
```

Abra `http://localhost:3000`. Para usar um projeto Supabase próprio, copie `web/.env.example` para `web/.env.local`, configure a URL e a chave pública do projeto e aplique as migrações de `web/supabase/migrations/` em ordem. O guia detalhado está em [`web/README.md`](web/README.md).

Nunca adicione arquivos `.env.local`, chaves administrativas, backups, exportações financeiras ou dados reais ao repositório.

## Tecnologias

**Web:** Next.js 16, React 19, TypeScript, Supabase Auth e PostgreSQL, SQL com Row Level Security, PWA.

**Desktop e sincronização:** .NET 10, Avalonia 12, CommunityToolkit.Mvvm, SQLite e ASP.NET Core.

## Estrutura do repositório

```text
web/                         Aplicação web, componentes, regras e migrações Supabase
src/NickFinancas.Domain/     Entidades e regras de domínio
src/NickFinancas.Application/ Casos de uso e contratos
src/NickFinancas.Infrastructure/ Persistência e integrações
src/NickFinancas.Desktop/    Aplicativo desktop Avalonia
src/NickFinancas.SyncApi/    API de sincronização para a versão desktop
tests/                       Testes automatizados do domínio e da persistência
docs/                        Requisitos, arquitetura e guias operacionais
```

## Documentação

- [Guia da aplicação web](web/README.md)
- [Arquitetura](docs/architecture.md)
- [Requisitos do produto](docs/product-requirements.md)
- [Cópia e restauração dos dados](web/RECOVERY.md)
- [Guia do servidor de sincronização desktop](docs/sync-server.md)

## Privacidade

O site publicado é uma aplicação de uso pessoal e exige autenticação. Não existe uma demonstração pública com dados do ambiente real. A exportação financeira deve ficar em armazenamento privado. Para um novo ambiente, siga o guia de recuperação e use um projeto Supabase de teste separado.

## Licença

Este repositório não declara uma licença de código aberto. Consulte o autor antes de reutilizar o código.
