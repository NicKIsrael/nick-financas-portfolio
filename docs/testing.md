# Roteiro de testes

## Correcao de aporte em reserva

1. Registre um aporte de R$ 635,03 na reserva Emergencia e anote o saldo disponivel.
2. Aplique a correcao interna do aporte e confirme que a reserva fica zerada sem aumentar o saldo disponivel.
3. Confirme que o aporte saiu do historico da reserva e que nao foi criada despesa ou lancamento no extrato.
4. Confira que saldos de meses anteriores a data do aporte permanecem iguais.

## Versao 1.1.22 - lancamentos historicos

1. Selecione agosto de 2026, abra Novo lancamento e confirme que a data inicial pertence a agosto.
2. Salve cinco despesas no mesmo mes e confirme que todas aparecem na lista, sem filtros ativos.
3. Salve um lancamento com data de outro mes e confirme que o seletor muda para esse mes e exibe o registro.
4. Confirme que a mensagem de sucesso mostra a data gravada.

## Versao 0.1.0-rc.8 - servidor

1. Publique o servidor com `scripts/Publish-SyncServer.ps1`.
2. Instale o servico com `scripts/Install-SyncServer.ps1` em um PowerShell elevado.
3. Acesse `/health` e confirme `status: online` e `database: sqlite`.
4. Tente registrar um dispositivo sem `X-Finance-Api-Key` e confirme a resposta 401.
5. Registre os dispositivos de os dois usuÃ¡rios usando o mesmo `spaceId`.
6. Envie um lote por `/api/sync/push` e confirme a quantidade aceita.
7. Reenvie o mesmo lote e confirme que ele e contado como duplicado, sem nova sequencia.
8. Consulte `/api/sync/pull` pelo outro dispositivo e confira a ordem das alteracoes.
9. Confirme a criacao do banco e do primeiro backup em `C:\ProgramData\Nick FinanÃ§as`.

## Versao 0.1.0-rc.6

1. Abra `Planejamento` e confirme a exibicao dos proximos seis meses.
2. Confira as medias de receita e despesa no resumo superior.
3. Compare o saldo inicial do primeiro mes com o saldo disponivel atual.
4. Cadastre uma conta futura e confirme que ela aparece em `Contas e faturas` no mes correspondente.
5. Defina um aporte mensal em um objetivo e valide sua inclusao em todos os meses futuros.
6. Confira se o saldo final de cada mes se torna o saldo inicial do mes seguinte.
7. Crie um cenario de saldo negativo e valide o destaque vermelho e o alerta de risco.
8. Altere ou exclua um compromisso e confirme que a projecao e recalculada.
9. Alterne entre os temas claro e escuro e valide a legibilidade da tabela e dos alertas.

## Versao 0.1.0-rc.5

1. Abra `Calendario` e confirme a organizacao de segunda-feira a domingo.
2. Confira o destaque do dia atual quando o periodo selecionado for o mes corrente.
3. Selecione dias com receitas, despesas, contas e faturas e valide o detalhamento lateral.
4. Selecione um dia vazio e confira a mensagem correspondente.
5. Clique em `Agendar para este dia` e confirme que o formulario de pendencia abre com a data escolhida.
6. Salve a pendencia e volte ao calendario; confira o novo evento no dia.
7. Troque o periodo no cabecalho e valide a atualizacao de dias e eventos.
8. Alterne entre os temas claro e escuro e confira todos os elementos do calendario.

## Versao 0.1.0-rc.4

1. Abra e feche `Pendencias do mes` e observe a animacao suave nos dois sentidos.
2. Confirme que o cabecalho usa a superficie do tema e nao apresenta nenhuma faixa preta.
3. Em um mes vazio, confira a mensagem `Nenhuma pendencia neste periodo`.
4. Em um mes com contas ou faturas abertas, confirme data, descricao e valor dentro do card.
5. Repita a animacao em limites dos cartoes, uso dos limites e faturas anterior/proxima.
6. Alterne entre os temas claro e escuro e confira fundo, texto, bordas, icones e destaque.
7. Abra configuracoes, role contas e categorias e confirme que os botoes de adicionar nao entram na area de backup.
8. Confirme que o rodape de backup permanece separado e totalmente clicavel.

## Versao 0.1.0-rc.3

1. Na visao geral, confira que `Pendencias do mes` e `Limites dos cartoes` ocupam toda a largura do card.
2. Passe o mouse sobre os cabecalhos e valide o destaque usando a paleta do tema.
3. Abra e feche ambos e confira espacamento, seta e area clicavel.
4. Em `Contas e cartoes`, repita o teste em `Uso e limite dos cartoes`.
5. Confira tambem `Fatura anterior` e `Proxima fatura`.
6. Alterne entre tema escuro e claro e confirme que nenhum recolhivel apresenta fundo preto fixo.
7. Redimensione a janela ate a largura minima e confira que os cabecalhos continuam preenchendo o card.

## Versao 0.1.0-rc.2

1. Confirme o nome `Nick FinanÃ§as` e a nova logo na janela, barra lateral e executavel.
2. Na visao geral, abra e feche os cards de pendencias e limites dos cartoes.
3. Em `Contas e cartoes`, abra e feche o card de uso dos limites.
4. Abra configuracoes e confira que o card de backup nao sobrepoe contas, categorias ou botoes.
5. Confira o saldo disponivel nas telas de pendencias e reservas.
6. Cadastre uma pendencia com vencimento em ate sete dias e valide o alerta de vencimento proximo.
7. Crie um objetivo deixando o aporte mensal e o prazo vazios; confirme que nao aparece erro tecnico.
8. Edite o objetivo, escolha uma data e confira o formato brasileiro.
9. Informe um aporte mensal e confirme que ele aparece no card do objetivo.
10. Abra o seletor de periodo e confirme que o primeiro mes historico e agosto de 2026.

## Versao 0.1.0-rc.1

1. Abra o aplicativo e confira o novo icone do NickFinancas na janela e na barra de tarefas.
2. Valide que os dados cadastrados em versoes anteriores continuam disponiveis.
3. Abra `Membros`, tente remover um participante e confirme que a operacao exige confirmacao.
4. Cancele a confirmacao e verifique que o participante permanece cadastrado.
5. Repita, confirme a remocao e confira a mensagem de sucesso.
6. Percorra visao geral, lancamentos, pendencias, contas, reservas, objetivos e relatorios.
7. Crie um backup manual e exporte o extrato mensal antes de iniciar o uso diario do release candidate.

## Versao 0.1.0-alpha.34

1. Abra `Lancamentos` e selecione um mes com movimentacoes.
2. Clique em `Exportar CSV` e confira a mensagem com quantidade e caminho do arquivo.
3. Abra o CSV no Excel e valide as colunas de data, tipo, descricao, categoria, contas, cartao, valor, autor e dispositivo.
4. Confirme que acentos, ponto e virgula e aspas existentes nas descricoes permanecem corretos.
5. Verifique que os valores usam virgula decimal e aparecem em colunas separadas.
6. Troque o periodo, exporte novamente e confirme que somente os lancamentos do mes selecionado foram incluidos.
7. Exporte um mes vazio e confirme a criacao do arquivo apenas com o cabecalho.

## Versao 0.1.0-alpha.33

1. Registre despesas em categorias, contas, cartoes e autores diferentes no mesmo mes.
2. Abra `Relatorios` e use a lupa de uma categoria; confirme que o extrato mostra apenas seus lancamentos.
3. Repita o detalhamento em `Despesas por conta` e confira a conta selecionada no filtro.
4. Abra uma linha de `Compras por cartao` e confirme que compras de outros cartoes nao aparecem.
5. Abra uma linha de `Atividade por autor` e confirme que somente lancamentos daquele participante aparecem.
6. Confira a faixa de contexto acima do extrato indicando o detalhamento ativo.
7. Acesse `Lancamentos` diretamente pelo menu e confirme que o contexto e os filtros do relatorio sao limpos.

## Versao 0.1.0-alpha.32

1. Entre com um perfil administrador e abra `Membros`.
2. Promova um participante com papel `Membro` para administrador.
3. Selecione esse participante como perfil ativo e confirme que os controles administrativos ficam disponiveis.
4. Volte ao administrador principal e rebaixe o participante para membro.
5. Confirme que o perfil rebaixado perde imediatamente o acesso aos controles administrativos.
6. Confirme que o administrador principal nao exibe opcao para alterar o papel ou remover o cadastro.
7. Feche e reabra o aplicativo e confira que o papel escolhido foi mantido.

## Versao 0.1.0-alpha.31

1. Selecione um perfil com papel `Membro` na barra lateral.
2. Confirme que configuracoes e gerenciamento de cartoes ficam desabilitados.
3. Abra `Membros` e confirme que cadastro e remocao de participantes estao desabilitados.
4. Confirme que o membro ainda consegue consultar telas e registrar lancamentos.
5. Selecione novamente um perfil `Administrador`.
6. Confirme que os controles administrativos voltam a ficar disponiveis.
7. Abra configuracoes e valide o acesso aos backups, contas e categorias.

## Versao 0.1.0-alpha.30

1. Cadastre pelo menos dois participantes na secao `Membros`.
2. Escolha um deles no seletor `Perfil ativo` da barra lateral.
3. Registre uma receita ou despesa e confira o nome do autor no extrato.
4. Abra os detalhes do lancamento e confira tambem o dispositivo de origem.
5. Troque o perfil, registre outro lancamento e confirme a nova autoria.
6. Pague uma pendencia ou fatura e confirme que o perfil ativo foi registrado.
7. Feche e reabra o aplicativo e confira que os lancamentos mantiveram autor e dispositivo.

## Versao 0.1.0-alpha.29

1. Abra o aplicativo e as configuracoes e confirme a existencia do backup automatico do dia.
2. Feche e reabra no mesmo dia e confirme que uma copia diaria duplicada nao foi criada.
3. Clique em `Restaurar ultimo` e confirme que o aplicativo exibe um modal antes da operacao.
4. Cancele e verifique que nenhum dado foi alterado.
5. Repita, confirme a restauracao e confira a mensagem de sucesso.
6. Confirme que as configuracoes exibem a quantidade de backups no formato `n/10 copias`.

## Versao 0.1.0-alpha.28

1. Crie duas reservas e registre um aporte na primeira.
2. Em `Transferir entre reservas`, escolha origem, destino e valor.
3. Confirme que o valor diminui na origem e aumenta no destino.
4. Confira que o total reservado e o saldo disponivel nao mudam.
5. Confirme que nenhuma receita ou despesa foi criada.
6. Tente transferir mais que o saldo da origem e confira a validacao.
7. Tente escolher a mesma reserva como origem e destino e confirme o bloqueio.

## Versao 0.1.0-alpha.27

1. Registre receitas e despesas em meses, contas e cartoes diferentes.
2. Abra a visao geral e confirme que o grafico dos ultimos seis meses usa os valores cadastrados.
3. Abra `Relatorios` e confira as despesas agrupadas por conta.
4. Confira as compras agrupadas por cartao e a quantidade de compras.
5. Confira a atividade agrupada pelo autor dos lancamentos.
6. Valide o resumo de reservas e a quantidade de objetivos.
7. Troque o periodo e confirme que todas as consolidacoes sao atualizadas.

## Versao 0.1.0-alpha.26

1. Crie uma reserva sem movimentacoes, edite seu nome e confirme a alteracao.
2. Crie um objetivo vinculado a essa reserva.
3. Edite nome, valor alvo, prazo e aporte mensal do objetivo.
4. Tente excluir a reserva vinculada e confirme a protecao.
5. Exclua o objetivo, confirmando no modal, e verifique que a reserva permanece.
6. Exclua a reserva vazia e confirme sua remocao.
7. Em outra reserva, registre um aporte e confirme que ela nao pode ser excluida.

## Versao 0.1.0-alpha.25

1. Abra `Reservas` e registre um aporte e uma retirada.
2. Confira no historico o nome da reserva, conta, tipo, data e valor de cada operacao.
3. Confirme que as movimentacoes mais recentes aparecem primeiro.
4. Crie um objetivo vinculado a uma reserva, deixando o aporte mensal planejado em zero.
5. Registre aportes na reserva em meses diferentes.
6. Confirme que a previsao do objetivo passa a indicar o uso da media real dos aportes.
7. Crie outro objetivo com aporte planejado e confirme que a previsao identifica essa fonte.

## Versao 0.1.0-alpha.24

1. Abra o aplicativo pela primeira vez e confirme que o tema escuro e aplicado por padrao.
2. Navegue por todas as secoes e abra os formularios de lancamento, pendencia, cartao e configuracoes.
3. Confira contraste e legibilidade de textos, campos, bordas, paineis, graficos e mensagens.
4. Clique em `Usar tema claro` e confirme a troca imediata sem reiniciar o aplicativo.
5. Feche e reabra o aplicativo e confirme que o tema escolhido foi mantido.
6. Retorne ao tema escuro e confirme novamente a persistencia.

## Versao 0.1.0-alpha.23

1. Abra as configuracoes e crie uma categoria de despesa chamada `Transporte`.
2. Crie `Combustivel` escolhendo `Transporte` como categoria principal.
3. Abra um novo lancamento e confirme a opcao `Transporte › Combustivel`.
4. Registre uma despesa nessa subcategoria e confira a hierarquia nos lancamentos e relatorios.
5. Troque o tipo para receita e confirme que categorias principais de despesa nao ficam disponiveis.
6. Tente excluir `Transporte` antes de excluir `Combustivel` e confirme a protecao.
7. Feche e reabra o aplicativo e confirme que a hierarquia foi mantida.

## Versao 0.1.0-alpha.22

1. Abra `Novo lancamento`, escolha `Despesa` e `Cartao de credito`.
2. Informe uma compra de R$ 100,00 dividida em 3 parcelas.
3. Confirme que o mes atual mostra `1/3` com R$ 33,33.
4. Avance um mes e confirme a parcela `2/3` com R$ 33,33.
5. Avance novamente e confirme `3/3` com R$ 33,34.
6. Confira que cada parcela aparece na fatura correspondente.
7. Confirme que a soma das parcelas permanece exatamente R$ 100,00.
8. Tente informar zero ou mais de 60 parcelas e confira a validacao.

## Versao 0.1.0-alpha.21

1. Abra as configuracoes pelo icone de engrenagem e clique em `Criar backup`.
2. Confirme que a data, o horario e o tamanho do backup aparecem no painel.
3. Cadastre uma conta ou reserva apenas para teste depois de criar o backup.
4. Volte às configuracoes e clique em `Restaurar ultimo`.
5. Confirme que os dados retornam ao estado capturado no backup.
6. Confira que o aplicativo informa a criacao de uma copia de seguranca antes da restauracao.
7. Feche e reabra o aplicativo e confirme que o estado restaurado foi mantido.

## Versao 0.1.0-alpha.20

1. Abra `Membros` e confirme que Alex aparece como administrador principal.
2. Cadastre um participante informando nome, e-mail e papel `Membro`.
3. Feche e reabra o aplicativo e confirme que o participante foi mantido.
4. Tente cadastrar novamente o mesmo e-mail e confirme a validacao de duplicidade.
5. Cadastre outro participante como `Administrador` e confira o papel exibido.
6. Remova um participante adicional e confirme a atualizacao imediata da lista.
7. Confirme que o administrador principal nao apresenta opcao de remocao.

## Versao 0.1.0-alpha.19

1. Registre receitas e despesas em categorias diferentes e em mais de um mes.
2. Abra `Relatorios` e confira o comparativo dos ultimos seis meses.
3. Passe o mouse sobre as colunas e confirme os valores de receitas e despesas.
4. Confira se o resultado do periodo corresponde a receitas menos despesas.
5. Confira os valores e percentuais da consolidacao por categoria.
6. Altere o periodo no topo e confirme que o resultado e as categorias acompanham a selecao.
7. Clique em `Ver lancamentos` e confirme que a pagina abre filtrada por despesas.

## Versao 0.1.0-alpha.18

1. Crie uma reserva e registre um aporte nela.
2. Abra `Objetivos` e cadastre um objetivo com valor alvo, prazo, aporte mensal e a reserva criada.
3. Confirme que o progresso usa o saldo atual da reserva vinculada.
4. Confira o percentual e a previsao em meses calculados automaticamente.
5. Volte para `Visao geral` e confirme que o proximo objetivo apresenta os mesmos dados.
6. Registre outro aporte na reserva e confirme a atualizacao do progresso do objetivo.
7. Feche e reabra o aplicativo e confirme que o objetivo foi mantido.

## Versao 0.1.0-alpha.17

1. Abra `Reservas` e crie uma reserva chamada `Emergencia`.
2. Registre um aporte escolhendo uma conta e confirme que o total reservado aumenta.
3. Volte para `Visao geral` e confirme que o saldo disponivel diminuiu pelo mesmo valor, sem alterar as despesas do mes.
4. Feche e reabra o aplicativo e confirme que a reserva e o saldo foram mantidos.
5. Registre uma retirada e confira o aumento correspondente no saldo disponivel.
6. Tente retirar mais que o saldo da reserva e confirme que a operacao e impedida.

## Versao 0.1.0-alpha.16

1. Abra `Contas e cartoes` e localize `Faturas por periodo`.
2. Confirme que `Fatura anterior` e `Proxima fatura` ocupam toda a largura do painel.
3. Abra e feche os dois paineis e confira a entrada suave do conteudo com deslocamento e opacidade.
4. Confirme que a fatura atual permanece alinhada com os paineis recolhiveis.

## Versao 0.1.0-alpha.15

1. Confira se a bolinha do ajuste de limite aparece inteira e centralizada sobre a barra.
2. Em `Contas e cartoes`, confirme que `Fatura anterior` e `Proxima fatura` iniciam recolhidas e podem ser abertas pelo cabecalho.
3. Confirme que a fatura atual continua visivel por padrao.
4. Abra `Gerenciar cartoes`, clique no lapis e altere nome, conta, limite total, fechamento e vencimento.
5. Salve, feche e reabra o aplicativo e confirme que os dados editados foram mantidos.
6. Confira que os campos de valores nao possuem mais setas laterais.
7. Digite valores com quatro ou mais algarismos e confirme os separadores de milhar e duas casas decimais.

## Versao 0.1.0-alpha.14

1. Feche completamente qualquer versao anterior do Nick Financas.
2. Na `Visao geral`, confirme que o antigo campo numerico com setas e o botao `Atualizar` nao aparecem mais.
3. Arraste a bolinha do cartao e confirme a atualizacao imediata do limite escolhido, valor disponivel e percentual utilizado.
4. Confirme que a bolinha nao passa abaixo do valor ja utilizado nem acima do limite total do banco.
5. Aguarde a mensagem de limite atualizado, feche e reabra o aplicativo e confirme que a posicao foi mantida.
6. Repita o ajuste em `Contas e cartoes` e confirme o mesmo comportamento.

## Versao 0.1.0-alpha.13

1. Feche completamente qualquer versao anterior do Nick Financas.
2. Registre uma compra de R$ 100,00 no cartao e confirme que a despesa aparece no mes, mas o saldo disponivel da conta nao muda.
3. Confira na `Visao geral` a barra do cartao, com R$ 100,00 utilizados e o restante disponivel.
4. Ajuste um limite de R$ 1.000,00 para R$ 500,00 na `Visao geral` e confirme que o disponivel passa a R$ 400,00.
5. Abra `Contas e cartoes`, confira os mesmos valores e ajuste o limite novamente por essa tela.
6. Feche e reabra o aplicativo e confirme que o novo limite foi mantido.
7. Pague a fatura e confirme que somente nesse momento o saldo da conta diminui R$ 100,00 e o limite utilizado volta a zero.

## Versao 0.1.0-alpha.12

1. Feche completamente qualquer versao anterior do Nick Financas.
2. Abra a nova versao e confirme que o saldo voltou ao valor inicial esperado.
3. No novo lancamento, digite `100` no campo de valor e confirme a apresentacao com duas casas decimais.
4. Registre uma despesa de R$ 100,00 em uma conta com R$ 100,00 e confirme saldo R$ 0,00.
5. Exclua a despesa, registre uma receita de R$ 100,00 e confirme que o saldo passa de R$ 100,00 para R$ 200,00.
6. Teste os campos de saldo inicial, limite do cartao e valor da pendencia e confira a mesma formatacao automatica.
7. Tente informar um valor superior a R$ 1 bilhao e confirme que o aplicativo impede o cadastro.
8. Confirme que ponto e virgula nao produzem mais escalas inesperadas nos valores.

## Versao 0.1.0-alpha.11

1. Feche completamente qualquer versao anterior do Nick Financas.
2. Em uma conta com saldo de R$ 100,00, registre uma despesa de R$ 100,00 e confirme saldo R$ 0,00.
3. Em uma conta com saldo de -R$ 100,00, registre uma receita de R$ 100,00 e confirme saldo R$ 0,00.
4. Registre uma compra no cartao e confirme que a divida reduz o saldo disponivel imediatamente.
5. Pague a fatura e confirme que o saldo disponivel nao e reduzido pela segunda vez.
6. Abra `Pendencias` e confira se as faturas em aberto aparecem junto das contas do mes.
7. Confira se o botao `Pagar` nao cobre nem encosta no valor da pendencia.
8. Cadastre uma recorrencia e confira sua aparicao na lista `Pagamentos recorrentes`.
9. Encerre a recorrencia pela lixeira e confirme que ocorrencias futuras nao pagas desaparecem.
10. Confirme que ocorrencias ja pagas continuam no historico.
11. Abra `Contas e cartoes` e confira os paineis separados `Fatura anterior`, `Fatura atual` e `Proxima fatura`.
12. Confirme que compras feitas apos o fechamento aparecem somente na proxima fatura.

## Versao 0.1.0-alpha.10

1. Feche completamente qualquer versao anterior do Nick Financas.
2. Confirme que os dados existentes continuam disponiveis apos abrir a nova versao.
3. Abra `Contas e cartoes` e confira duas referencias por cartao: o periodo selecionado e o mes seguinte.
4. Registre uma compra depois do fechamento e confirme que ela aparece na proxima fatura.
5. Na pagina de lancamentos, confira a referencia da fatura exibida abaixo da descricao da compra.
6. Use o seletor superior para navegar por meses anteriores e futuros.
7. Abra `Pendencias` e cadastre uma conta avulsa com descricao, valor, vencimento, conta e categoria.
8. Confirme que a pendencia aparece no mes do vencimento e no resumo da visao geral.
9. Marque a conta como paga e confirme que ela muda para `Paga`, gera uma despesa e reduz o saldo da conta.
10. Cadastre uma conta recorrente mensal, como aluguel ou internet.
11. Avance para o mes seguinte e confirme que uma nova ocorrencia foi gerada automaticamente.
12. Confirme que uma pendencia nao reduz o saldo antes de ser paga e que o pagamento nao e duplicado.
13. Crie uma recorrencia de Academia de R$ 129,90, exclua apenas a ocorrencia do mes e reabra a tela. Ela nao deve reaparecer; a do mes seguinte deve continuar.
14. Pague outra ocorrencia recorrente e depois exclua-a. Confira que o pagamento sai das despesas e do saldo.

## Versao 0.1.0-alpha.9

1. Feche completamente qualquer versao anterior do Nick Financas.
2. Abra a nova versao e confirme que contas, cartoes e lancamentos existentes continuam disponiveis.
3. Cadastre um cartao com limite, fechamento, vencimento e conta de pagamento.
4. Crie uma despesa escolhendo `Cartao de credito` como forma de pagamento.
5. Confirme que a compra aparece nos lancamentos e nas despesas do mes, mas ainda nao reduz o saldo da conta.
6. Abra `Contas e cartoes` e confira total da fatura, valor pendente, vencimento e limite disponivel.
7. Cadastre compras antes e depois do fechamento e confira se elas entram nas faturas correspondentes.
8. Edite o valor e a data de uma compra e confirme o recálculo da fatura e do limite.
9. Pague a fatura e confirme que o saldo da conta vinculada e reduzido.
10. Confirme que o pagamento nao aumenta novamente as despesas do mes.
11. Exclua uma compra e confira a atualizacao da fatura e do limite.
12. Tente excluir um cartao que possua compras e confira a mensagem de protecao.

## Versao 0.1.0-alpha.8

1. Feche completamente qualquer versao anterior do Nick Financas.
2. Abra a nova versao e confirme que os dados cadastrados anteriormente continuam disponiveis.
3. Registre uma receita em julho e confira se ela continua compondo o saldo ao selecionar agosto ou setembro.
4. Abra `Contas e cartoes` e confira o saldo calculado de cada conta.
5. Crie uma transferencia escolhendo contas de origem e destino diferentes.
6. Confirme que o valor diminui na origem e aumenta no destino.
7. Confirme que o saldo consolidado e os totais de receitas e despesas nao mudam por causa da transferencia.
8. Na pagina de lancamentos, filtre por `Transferencias` e pelas contas envolvidas.
9. Edite a transferencia e confira novamente os saldos das duas contas.
10. Exclua a transferencia e confirme que os saldos anteriores sao restaurados.
11. Tente escolher a mesma conta como origem e destino e confira a validacao.

## Versao 0.1.0-alpha.7

1. Feche completamente qualquer versao anterior do Nick Financas.
2. Abra cada item do menu lateral e confirme que a pagina e o destaque selecionado mudam corretamente.
3. Abra `Lancamentos` e confirme que a tela nao abre mais o cadastro de cartoes.
4. Cadastre uma receita e uma despesa pelo botao `Novo lancamento`.
5. Confira se os novos itens aparecem na lista e atualizam os totais da visao geral.
6. Pesquise um lancamento pela descricao.
7. Teste os filtros de tipo, conta e categoria, inclusive combinando mais de um filtro.
8. Edite descricao, valor, data, tipo, conta e categoria de um lancamento.
9. Exclua um lancamento e confirme que ele desaparece da lista e deixa de participar dos totais.
10. Feche e abra o aplicativo para confirmar que as alteracoes continuam salvas.
11. Abra `Contas e cartoes` e confirme que os dois atalhos de gerenciamento funcionam.
12. Na visao geral, teste os atalhos de lancamentos, relatorios, reservas e objetivos.

## Versao 0.1.0-alpha.6

1. Feche completamente qualquer versao anterior do Nick Financas.
2. Abra os cadastros e clique na lixeira da categoria de teste.
3. Confirme que a pergunta de exclusao aparece e conclua a operacao.
4. Confirme que a categoria desaparece e que a mensagem de sucesso e exibida.
5. Repita o teste com uma conta sem lancamentos e com um cartao de teste.
6. Abra `Contas e cartoes` sem cartoes cadastrados.
7. Confira o estado compacto `Nenhum cartao cadastrado`, sem a antiga area vazia.
8. Cadastre varios cartoes e confirme que a lista passa a usar rolagem.

## Versao 0.1.0-alpha.5

1. Abra os cadastros pelo botao de configuracoes e exclua uma categoria sem lancamentos.
2. Confirme que ela desaparece imediatamente e que a mensagem de sucesso aparece.
3. No menu lateral, abra `Contas e cartoes`.
4. Cadastre um cartao informando nome, conta de pagamento, limite, fechamento e vencimento.
5. Feche e abra o aplicativo para confirmar que o cartao continua cadastrado.
6. Tente excluir a conta de pagamento vinculada e confira a mensagem de protecao.
7. Exclua o cartao, confirme a remocao imediata e depois exclua a conta de teste.

## Versao 0.1.0-alpha.4

1. Passe o mouse pelos botoes principais, secundarios, menu lateral e icones.
2. Confirme que as mudancas de cor e escala acontecem suavemente.
3. Confira a centralizacao de `Novo lancamento`, `Adicionar conta` e `Adicionar categoria`.
4. Abra `Novo lancamento` e confirme que todos os botoes estao centralizados.
5. Confira se o campo de data permanece dentro da coluna.
6. Digite uma data no formato `dd/MM/aaaa` e selecione outra usando o calendario.
7. Salve o lancamento e confirme a data apresentada na lista.

## Versao 0.1.0-alpha.3

1. Abra os cadastros e confirme que contas e categorias possuem listas rolaveis.
2. Cadastre varios itens e confirme que os formularios continuam dentro da janela.
3. Exclua uma conta sem lancamentos e confirme a remocao.
4. Exclua uma categoria sem lancamentos e confirme a remocao.
5. Tente excluir uma conta ou categoria usada por um lancamento e confira a mensagem de protecao.
6. Confirme que a exclusao sempre apresenta uma pergunta antes de apagar.
7. Confira os tipos de conta em portugues.

## Versao 0.1.0-alpha.2

1. Abra `NickFinancas.exe` e clique no botao de configuracoes.
2. Cadastre uma conta com nome, tipo e saldo inicial.
3. Cadastre uma categoria de despesa e outra de receita.
4. Feche a janela de cadastros e abra `Novo lancamento`.
5. Confirme que a conta e as categorias novas aparecem nas listas.
6. Registre lancamentos em setembro, agosto e julho de 2026.
7. Troque o periodo no topo e confira se os totais e a lista acompanham o mes.
8. Feche e abra o aplicativo para confirmar a persistencia.

## Versao 0.1.0-alpha.1

1. Abra `NickFinancas.exe`.
2. Confirme que a tela inicial segue o prototipo aprovado.
3. Clique em `Novo lancamento`.
4. Cadastre uma despesa com descricao, valor, data, conta e categoria.
5. Confirme que o saldo e as despesas do mes foram atualizados.
6. Feche completamente o aplicativo e abra novamente.
7. Confirme que o lancamento continua na lista e os totais permanecem atualizados.
8. Repita o fluxo escolhendo `Receita` e a categoria `Salario`.

## Dados locais

O banco desta fase fica em `%LOCALAPPDATA%\NickFinancas\nickfinancas.db`.

Esta e uma versao de desenvolvimento. Use apenas dados ficticios durante os testes iniciais.
