# Arquitetura

## Direcao adotada

O aplicativo e local-first. Cada instalacao possui um banco SQLite e uma fila persistente de alteracoes. O computador principal executara uma API ASP.NET Core nativa do Windows e um SQLite central, acessiveis apenas por uma rede privada autorizada.

```text
Desktop Alex ---- SQLite ----\
                                API ASP.NET Core ---- SQLite central
Desktop noiva ------ SQLite ----/
```

Quando o servidor estiver indisponivel, os aplicativos continuam operando. A sincronizacao bidirecional ocorre quando cliente e servidor voltarem a ficar acessiveis.

Cada gravacao local e o respectivo item da fila offline pertencem a mesma transacao SQLite. O cliente envia lotes idempotentes e depois aplica todas as sequencias globais do servidor, inclusive as proprias confirmacoes. Isso faz os dispositivos convergirem para a mesma ordem final sem criar ciclos de reenvio.

## Camadas

- `Domain`: entidades, valores e regras sem dependencia de interface ou banco.
- `Application`: casos de uso, autorizacao e contratos de persistencia/sincronizacao.
- `Infrastructure`: SQLite, clientes HTTP, criptografia e implementacoes dos contratos.
- `Desktop`: views Avalonia e view models.
- `SyncApi`: autenticacao, sincronizacao e banco central SQLite executados como servico do Windows.

## Preparacao para sincronizacao

Registros sincronizaveis usarao UUID, versao, datas de criacao e alteracao, exclusao logica, autor e dispositivo de origem. O banco central nunca sera exposto diretamente; somente a API podera acessa-lo.

## Seguranca

- Trafego restrito ao Tailscale e protegido por criptografia de transporte.
- Senhas armazenadas somente como hash forte.
- Tokens de acesso curtos e revogaveis.
- Segredos fora do codigo-fonte.
- Backups automaticos e testes periodicos de restauracao.

