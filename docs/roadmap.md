# Roadmap apos a revisao do PDF

## Entregue no RC.2

- Cards recolhiveis de pendencias e limites.
- Revisao visual do backup.
- Saldo visivel em pendencias e reservas.
- Correcao dos campos de objetivo e datas em portugues.
- Aporte mensal visivel no card dos objetivos.
- Periodos a partir de setembro de 2026.
- Alertas para contas com vencimento em ate sete dias.
- Nova marca e novo nome do aplicativo.

## Entregue no RC.6

- Planejamento de receitas, despesas, reservas e saldo para meses futuros.

## Entregue no RC.5

- Calendario mensal com vencimentos, pagamentos e lancamentos por dia.
- Agendamento de contas diretamente pelo calendario.

## Etapa de sincronizacao

- API ASP.NET Core nativa no computador-servidor. (fundacao entregue no RC.8)
- SQLite central acessado exclusivamente pela API e protegido por backups. (entregue no RC.8)
- Sincronizacao automatica entre os computadores de os dois usuÃ¡rios.
- Fila offline, atualizacao em tempo real quando ambos estiverem conectados e resolucao de conflitos.

O RC.2 continua local-first: toda alteracao aparece imediatamente neste computador. A atualizacao entre computadores sera ativada somente quando a API e o servidor Tailscale estiverem implantados e validados.
