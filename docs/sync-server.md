# Servidor de sincronizacao

O servidor recebe alteracoes dos dispositivos, grava um historico ordenado em SQLite e entrega somente as alteracoes ainda nao lidas pelo outro computador. Ele roda diretamente como servico do Windows, sem Docker, WSL ou PostgreSQL.

## Persistencia

- Banco padrao: `C:\ProgramData\Nick FinanÃ§as\Data\sync.db`.
- Backups: `C:\ProgramData\Nick FinanÃ§as\Backups`.
- Um backup e criado na inicializacao e a cada 24 horas.
- Sao preservadas as 30 copias mais recentes.
- WAL, `busy_timeout` e uma fila de escrita protegem a concorrencia.
- Os clientes nunca abrem o arquivo SQLite central diretamente; todo acesso passa pela API.

## Publicacao e instalacao

```powershell
.\scripts\Publish-SyncServer.ps1
```

Depois de reiniciar o Windows, abra o PowerShell como Administrador e execute:

```powershell
.\scripts\Install-SyncServer.ps1 -PublishPath .\artifacts\sync-server-win-x64
```

O instalador gera uma chave segura, restringe as permissoes dos dados, instala o servico com inicio automatico e valida `http://127.0.0.1:5210/health`. Por seguranca, a API escuta somente no computador local por padrao. A exposicao por uma rede pessoal sera configurada separadamente, sem interferir no Tailscale corporativo.

## Protocolo

- `GET /health`: verifica a API e a integridade rapida do SQLite.
- `POST /api/devices/register`: registra ou atualiza um dispositivo.
- `POST /api/sync/push`: recebe ate 500 alteracoes idempotentes por lote.
- `GET /api/sync/pull`: entrega alteracoes incrementais posteriores a ultima sequencia.

Cada alteracao possui um `changeId` unico. Reenviar o mesmo lote depois de uma queda de conexao nao duplica os dados. Todas as rotas de sincronizacao exigem o cabecalho `X-Finance-Api-Key`.

## Implantacao atual

- Servico `NickFinancasSync` instalado em 29/09/2026.
- Inicio automatico retardado e tres tentativas de recuperacao apos falha.
- API local validada em `http://127.0.0.1:5210`.
- Banco e backups protegidos por permissoes de administrador e `LocalSystem`.
- Consumo observado apos a inicializacao: aproximadamente 64 MB de RAM.
- Registro autenticado, reinicio do servico e verificacao de integridade validados.
- Notebook de Alex configurado como primeiro cliente e sincronizacao inicial concluida sem pendencias.
- Tailscale pessoal ativo no notebook e conta corporativa mantida desconectada.
- Endereco dos clientes: `http://seu-computador.seu-tailnet.ts.net:8080`.
- A rota e exclusiva do tailnet pessoal; nao usa Funnel e nao fica acessivel pela internet.
- O HTTP trafega dentro do tunel WireGuard criptografado do Tailscale. HTTPS adicional foi removido por incompatibilidade do Schannel/.NET neste Windows.

## Desinstalacao

`Uninstall-SyncServer.ps1` preserva o banco e os backups. O parametro `-RemoveData` apaga esses dados e deve ser usado somente quando houver uma copia de seguranca confirmada.

## Atualizacoes automaticas

O aplicativo consulta o servidor autenticado na inicializacao. Quando existe uma versao superior, baixa o ZIP em segundo plano, valida o SHA-256 e aplica a troca ao fechar. O atualizador preserva uma copia da versao anterior e a restaura se a substituicao falhar.

Para publicar uma versao:

```powershell
.\scripts\Publish-DesktopUpdate.ps1 -Version 1.0.1 -ReleaseNotes 'Descricao da versao'
.\scripts\Deploy-DesktopUpdate.ps1
```

O segundo comando deve ser executado como administrador. Apenas o servidor precisa receber o pacote; os demais computadores atualizam sem instalacao manual.
