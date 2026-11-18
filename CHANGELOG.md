# Changelog

## 1.1.25 - 2026-10-05

- Uma correcao interna pode retirar um aporte informado por engano sem devolve-lo ao saldo disponivel e sem criar despesa ou lancamento no extrato.
- A correcao respeita a data do aporte e preserva os saldos de meses anteriores.

## 1.1.24 - 2026-10-05

- Pendencias recorrentes podem ser excluidas individualmente sem reaparecer ao atualizar o mes; as outras ocorrencias continuam.
- A exclusao de uma ocorrencia paga retira seu pagamento dos saldos e relatorios.
- A tela de reservas explica que aportes nao sao despesas e independem da ordem de lancamento das receitas.
- Mensagens de exclusao de lancamentos aparecem na propria tela de Lancamentos.
- O intervalo configurado para a sincronizacao deixa de ser limitado a tres segundos.

## 1.1.23 - 2026-10-04

- Cada participante pode configurar valor, dia e conta de recebimento do salario em Contas e cartoes.
- O salario e lancado automaticamente na data configurada, inclusive ao reabrir o aplicativo apos o vencimento.
- A atualizacao preserva o banco de dados local e acrescenta somente os campos necessarios ao cadastro de salario.

## 1.1.22 - 2026-10-04

- Novos lancamentos usam o mes selecionado como data inicial.
- Ao salvar, o aplicativo mostra o mes do lancamento, limpa filtros e confirma a data gravada.

## 1.1.21 - 2026-10-04

- Agosto de 2026 passa a aparecer no seletor de meses.

## 1.1.20 - 2026-10-03

- Compras parceladas em conta mostram a acao Excluir restantes.
- A exclusao remove todas as parcelas pendentes do plano e preserva pagamentos ja realizados.

## 1.1.19 - 2026-10-03

- Erros de exclusao de categorias aparecem no cadastro de categorias, mesmo quando a pagina de Pendencias esta aberta.
- Categorias vinculadas a pendencias mostram a quantidade e o periodo das pendencias que impedem a exclusao.
- Mensagens antigas de erro em Pendencias sao limpas apos uma exclusao bem-sucedida.

## 1.1.18 - 2026-10-03

- O botao Excluir aparece tambem em pendencias pagas.
- Ao excluir uma pendencia paga, o lancamento do pagamento sai dos saldos e relatorios.
- Ocorrencias de recorrencias ativas exigem encerrar a recorrencia antes da exclusao.

## 1.1.17 - 2026-10-03

- Exclusao de pendencias em aberto volta a funcionar apos a confirmacao.
- Botao Excluir fica visivel ao lado de Pagar na lista de pendencias.

## 1.1.16 - 2026-10-01

- Campos de numero de parcelas aceitam ficar vazios durante a digitacao e mostram validacao ao salvar.
- Novos lancamentos parcelados iniciam com duas parcelas validas.

## 1.1.15 - 2026-10-01

- Compras parceladas podem ser registradas nos lancamentos e nas pendencias.
- Compras antigas permitem informar a primeira parcela ainda em aberto, sem recriar as anteriores.
- Pendencias mostram as parcelas restantes e a proxima data de pagamento, inclusive em cartoes.
- O banco local recebe os novos campos de parcelamento sem perder lancamentos existentes.

## 0.1.0-rc.11 - 2026-09-29

- Modal de contas e categorias fica mais compacto e elimina grandes areas vazias.
- Listas vazias exibem estados explicativos com icones no tema azul.
- Formularios passam a usar cartoes agrupados e campos lado a lado.
- Listas preenchidas possuem rolagem propria com altura controlada.
- Backup permanece fixo e acessivel no rodape do modal.

## 0.1.0-rc.10 - 2026-09-29

- Identidade visual passa do verde para uma paleta azul nos temas claro e escuro.
- Fundos, superficies, bordas, navegacao, destaques e estados positivos foram harmonizados em azul.
- Nova logo usa uma moeda dourada ficticia com casa e seta de crescimento em azul.
- Icone da janela e executavel do Windows foram atualizados com a nova identidade.

## 0.1.0-rc.9 - 2026-09-29

- Bancos novos passam a iniciar sem contas, categorias, saldos ou lancamentos de demonstracao.
- Dados financeiros locais e historico central de sincronizacao foram reinicializados.
- Configuracao privada, tema, Tailscale e identidade tecnica do espaco foram preservados.
- Backups anteriores foram retirados do aplicativo e mantidos em uma pasta de recuperacao.

## 0.1.0-rc.8 - 2026-09-29

- Servidor central migra de PostgreSQL em Docker para SQLite nativo do Windows.
- API passa a funcionar como servico leve com inicializacao automatica e recuperacao apos falhas.
- SQLite central usa WAL, espera de concorrencia, transacoes e serializacao das escritas.
- Backups consistentes sao criados na inicializacao e diariamente, com retencao das 30 copias mais recentes.
- Scripts publicam, instalam, validam e removem o servico preservando os dados por padrao.
- Protocolo de sincronizacao e compatibilidade dos clientes foram mantidos.
- Toda alteracao local entra em uma fila offline no mesmo commit do dado financeiro.
- Sincronizador envia lotes idempotentes, recebe alteracoes incrementais e retoma da ultima sequencia confirmada.
- Dados existentes entram automaticamente na primeira sincronizacao, sem exigir recadastro.
- Alteracoes remotas sao aplicadas sem voltar para a fila, evitando ciclos de reenvio.
- Ordem global do servidor garante convergencia quando dois dispositivos alteram o mesmo registro.
- Servidor passa a distribuir manifestos e pacotes autenticados de atualizacao.
- Aplicativo baixa novas versoes em segundo plano, valida SHA-256 e atualiza ao fechar.
- Atualizador mantem a versao anterior e restaura os arquivos em caso de falha.
- API e atualizacoes passam a ser acessiveis exclusivamente pelo Tailscale pessoal, sem Funnel.

## 0.1.0-rc.7 - 2026-09-29

- Novo servidor ASP.NET Core dedicado a sincronizacao entre dispositivos.
- PostgreSQL central armazena alteracoes em ordem global e mantem historico incremental.
- Registro de dispositivos por espaco financeiro.
- Envio idempotente em lotes de ate 500 alteracoes, sem duplicacao apos reconexoes.
- Leitura incremental paginada ignora as proprias alteracoes do dispositivo solicitante.
- Rotas protegidas por chave compartilhada com comparacao resistente a ataques de temporizacao.
- Endpoint de saude valida simultaneamente a API e a conexao com o banco.
- Docker Compose mantem o PostgreSQL isolado e publica somente a API.
- Guia de implantacao privada pela rede Tailscale.

## 0.1.0-rc.6 - 2026-09-28

- Nova secao `Planejamento` projeta os proximos seis meses.
- Receita e despesa previstas usam a media dos meses que possuem movimentacao.
- Contas a pagar, faturas em aberto e aportes mensais dos objetivos entram separadamente no calculo.
- Cada mes informa saldo inicial, entradas, gastos medios, compromissos, aportes e saldo final.
- Meses com saldo final negativo recebem alerta visual e orientacao para revisao.
- Resumo superior informa as medias consideradas, saldo ao fim do periodo e nivel de risco.
- Projecoes sao recalculadas automaticamente sempre que os dados financeiros mudam.

## 0.1.0-rc.5 - 2026-09-28

- Nova secao `Calendario` organiza o mes selecionado em semanas de segunda a domingo.
- Dia atual recebe destaque visual e dias com atividade informam a quantidade de eventos.
- Calendario consolida receitas, despesas, transferencias, pagamentos, contas e faturas.
- Selecionar um dia abre o detalhamento com descricao, tipo e valor de cada evento.
- Dias sem atividade exibem estado vazio explicativo.
- Botao `Agendar conta` abre o formulario de pendencias com a data selecionada preenchida.
- Calendario acompanha automaticamente qualquer alteracao no periodo ou nos dados financeiros.

## 0.1.0-rc.4 - 2026-09-28

- Componente `Expander` padrao foi removido integralmente dos cards do aplicativo.
- Novo recolhivel proprio usa somente cores e recursos dos temas claro e escuro.
- Abertura e fechamento animam altura, opacidade, espacamento e deslocamento em ate 280 ms.
- Setas, icones, descricoes e estados expandido/recolhido foram redesenhados.
- Pendencias abertas exibem contas a pagar e faturas de cartao em uma lista unica.
- Periodos sem pendencias agora mostram um estado vazio explicativo, em vez de uma area em branco.
- Conteudo de contas e categorias ganhou rolagem independente do rodape de backup.
- Rodape de backup permanece fixo e nao e mais sobreposto pelos formularios.
- Todos os recolhiveis de limites e faturas usam o mesmo componente visual.

## 0.1.0-rc.3 - 2026-09-28

- Todos os componentes recolhiveis recebem um estilo proprio alinhado aos temas claro e escuro.
- Cabecalhos recolhiveis passam a ocupar toda a largura disponivel dos cards.
- Fundo preto e caixas internas estreitas do estilo padrao do Avalonia foram removidos.
- Estados normal, destaque ao passar o mouse e expandido usam as cores do tema do aplicativo.
- Espacamento, altura minima, bordas e area clicavel foram uniformizados.
- Novo estilo cobre pendencias, limites na visao geral, limites em contas e faturas anterior e seguinte.

## 0.1.0-rc.2 - 2026-09-28

- Aplicativo passa a se chamar `Nick FinanÃ§as` na janela, barra lateral, metadados e executavel.
- Nova identidade visual usa monograma N e K com referencia a lar e estabilidade financeira.
- Limites dos cartoes ficam recolhiveis na visao geral e em contas e cartoes.
- Pendencias do mes ganham resumo recolhivel com detalhes na visao geral.
- Tela de pendencias e tela de reservas passam a exibir o saldo disponivel.
- Pendencias com vencimento nos proximos sete dias recebem alerta dedicado.
- Campo vazio de aporte planejado deixa de gerar erro tecnico e passa a valer zero.
- Campo de prazo dos objetivos usa data em portugues e tipo compativel com o Avalonia.
- Cartoes de objetivos exibem o aporte mensal desejado.
- Seletor de periodo passa a comecar em setembro de 2026 e mantem doze meses futuros.
- Modal de configuracoes e controles de backup recebem espacamento e hierarquia visual revisados.
- Indicacao ficticia de sincronizacao foi substituida por estado honesto dos dados locais.

## 0.1.0-rc.1 - 2026-09-28

- Primeiro pacote candidato a entrega do MVP local.
- Aplicativo recebe icone proprio do NickFinancas na janela e no executavel do Windows.
- Remocao de participantes passa a exigir confirmacao explicita.
- Confirmacao informa que o historico financeiro sera preservado e que o acesso sera revogado.
- Validacao final mantem compatibilidade com bancos locais existentes e inicializacao de bases novas.
- Suite automatizada consolidada com 22 testes aprovados.

## 0.1.0-alpha.34 - 2026-09-28

- Extrato mensal pode ser exportado em CSV diretamente pela tela de lancamentos.
- Arquivo inclui data, tipo, descricao, categoria, origem, destino, cartao, valor, autor e dispositivo.
- Exportacao usa UTF-8, separador compativel com Excel e valores no formato brasileiro.
- Arquivos sao salvos na pasta local `Exports` sem alterar o banco financeiro.
- Interface informa quantidade de registros e caminho completo do arquivo gerado.
- Teste automatizado cobre geracao, conteudo e escape de caracteres especiais no CSV.

## 0.1.0-alpha.33 - 2026-09-28

- Relatorios por categoria, conta, cartao e autor passam a abrir os lancamentos correspondentes.
- Detalhamento aplica automaticamente os filtros corretos no extrato do periodo.
- Compras de cartao e autoria recebem filtros dedicados, sem misturar resultados de outros grupos.
- Extrato identifica visualmente quando foi aberto a partir de um detalhamento do relatorio.
- Acesso comum a tela de lancamentos limpa o contexto anterior e restaura os filtros gerais.

## 0.1.0-alpha.32 - 2026-09-28

- Administradores podem promover membros e rebaixar administradores diretamente na lista de participantes.
- Alteracoes de papel sao persistidas no banco local e refletidas imediatamente no perfil ativo.
- Administrador principal fica protegido contra rebaixamento e remocao.
- Controles de papel permanecem indisponiveis para perfis sem acesso administrativo.
- Testes automatizados cobrem promocao, rebaixamento e protecao do administrador principal.

## 0.1.0-alpha.31 - 2026-09-28

- Papeis de administrador e membro passam a controlar a interface administrativa.
- Apenas administradores podem abrir cadastros de contas, categorias e cartoes.
- Cadastro e remocao de participantes ficam restritos a administradores.
- Criacao e restauracao de backups exigem perfil administrativo.
- Membros continuam podendo consultar dados e registrar movimentacoes financeiras.
- Barra lateral informa claramente o nivel de acesso do perfil ativo.

## 0.1.0-alpha.30 - 2026-09-28

- Membros cadastrados podem ser escolhidos como perfil ativo na barra lateral.
- Novos lancamentos registram o membro atualmente selecionado como autor.
- Pagamentos de pendencias e faturas tambem respeitam o perfil ativo.
- Lancamentos passam a registrar o nome do dispositivo de origem.
- Extrato detalhado exibe autor e dispositivo usados na operacao.
- Registros antigos recebem identificacao neutra de dispositivo durante a migracao.
- Testes automatizados confirmam persistencia da autoria e origem.

## 0.1.0-alpha.29 - 2026-09-28

- Aplicativo cria automaticamente um backup por dia ao iniciar.
- Backups locais passam a manter automaticamente as 10 copias mais recentes.
- Configuracoes exibem a quantidade de copias disponiveis e o limite de retencao.
- Restauracao exige confirmacao explicita em um modal dedicado.
- Estado atual continua sendo salvo antes de qualquer restauracao.
- Testes automatizados cobrem backup diario e politica de retencao.

## 0.1.0-alpha.28 - 2026-09-28

- Reservas podem transferir valores diretamente entre si.
- Transferencia exige reservas de origem e destino diferentes.
- Saldo insuficiente na origem impede toda a operacao.
- Operacao atomica evita movimentacoes parciais em caso de erro.
- Total reservado e saldo disponivel permanecem inalterados durante a transferencia.
- Transferencias nao geram receitas ou despesas.
- Testes automatizados cobrem saldos, neutralidade financeira e protecao contra saldo insuficiente.

## 0.1.0-alpha.27 - 2026-09-28

- Grafico demonstrativo da visao geral e substituido pelo historico real dos ultimos seis meses.
- Relatorios passam a consolidar despesas por conta.
- Compras no credito sao agrupadas por cartao no periodo selecionado.
- Atividade financeira e consolidada pelo autor de cada lancamento.
- Resumo de reservas e quantidade de objetivos sao adicionados ao relatorio.
- Todos os novos paineis acompanham o periodo selecionado e os temas claro ou escuro.

## 0.1.0-alpha.26 - 2026-09-28

- Reservas cadastradas podem ter o nome editado.
- Objetivos podem ser editados: nome, valor alvo, prazo, aporte planejado e reserva vinculada.
- Exclusoes de reservas e objetivos usam confirmacao visual.
- Reservas com movimentacoes ficam protegidas contra exclusao.
- Reservas vinculadas a objetivos tambem nao podem ser apagadas.
- Excluir um objetivo preserva integralmente a reserva e seu historico.
- Testes automatizados cobrem edicao, exclusao e protecoes de integridade.

## 0.1.0-alpha.25 - 2026-09-28

- Reservas passam a exibir historico completo de aportes e retiradas.
- Cada movimentacao identifica reserva, conta relacionada, tipo, data e valor.
- Historico e carregado em ordem da movimentacao mais recente.
- Objetivos sem aporte planejado passam a usar a media real dos aportes mensais na previsao.
- Previsao informa se o calculo usa o valor planejado ou a media real observada.
- Testes automatizados cobrem historico e media mensal de aportes.

## 0.1.0-alpha.24 - 2026-09-28

- Tema escuro completo passa a ser o visual padrao do aplicativo.
- Paleta escura cobre fundo, paineis, textos, bordas, campos, seletores e graficos.
- Contraste de valores positivos, negativos e elementos de destaque foi ajustado para telas escuras.
- Botao no cabecalho permite alternar imediatamente entre tema escuro e claro.
- Preferencia visual e salva localmente e restaurada ao abrir novamente o aplicativo.
- Tema claro original continua disponivel para quem preferir.

## 0.1.0-alpha.23 - 2026-09-28

- Categorias podem receber uma categoria principal opcional e se tornar subcategorias.
- Apenas categorias do mesmo tipo podem ser vinculadas entre si.
- Hierarquia aparece nos formularios, filtros, ultimos lancamentos e relatorios.
- Categorias principais com subcategorias ficam protegidas contra exclusao.
- Banco local existente evolui automaticamente sem alterar categorias cadastradas.
- Testes automatizados cobrem hierarquia, validacao de tipo e exclusao protegida.

## 0.1.0-alpha.22 - 2026-09-28

- Compras no cartao podem ser divididas em ate 60 parcelas.
- Parcelas sao distribuidas automaticamente entre as faturas dos meses seguintes.
- Cada lancamento identifica sua posicao, como `1/3`, `2/3` e `3/3`.
- Divisao monetaria preserva o valor total, incluindo diferencas de centavos na ultima parcela.
- Despesas mensais e faturas passam a reconhecer cada parcela no periodo correspondente.
- Testes automatizados cobrem distribuicao entre faturas e arredondamento exato.

## 0.1.0-alpha.21 - 2026-09-28

- Configuracoes recebem criacao de backups locais do banco financeiro.
- Data, horario e tamanho do backup mais recente ficam visiveis no aplicativo.
- Restauracao do backup mais recente valida o arquivo antes de substituir os dados ativos.
- Uma copia de seguranca do estado atual e criada automaticamente antes de cada restauracao.
- Backups sao mantidos em pasta propria ao lado do banco local.
- Testes automatizados cobrem criacao, listagem e restauracao integral dos dados.

## 0.1.0-alpha.20 - 2026-09-28

- Secao de membros substitui o ultimo placeholder do menu principal.
- Participantes podem ser adicionados por nome, e-mail e papel no espaco compartilhado.
- Papeis de membro e administrador passam a ser armazenados no banco local.
- E-mails duplicados sao impedidos e normalizados para letras minusculas.
- Administrador principal e criado automaticamente e protegido contra remocao.
- Participantes adicionais podem ser removidos do espaco.
- Testes automatizados cobrem persistencia, remocao e protecao do administrador principal.

## 0.1.0-alpha.19 - 2026-09-27

- Secao de relatorios passa a usar os lancamentos reais do banco local.
- Comparativo visual exibe receitas e despesas dos ultimos seis meses.
- Resultado liquido do periodo selecionado e calculado automaticamente.
- Despesas sao consolidadas por categoria com valor e participacao percentual.
- Atalho do relatorio abre os lancamentos do periodo ja filtrados por despesas.
- Grafico demonstrativo da visao geral deixa de ser a unica referencia historica disponivel.

## 0.1.0-alpha.18 - 2026-09-27

- Secao de objetivos passa a permitir cadastro com valor alvo, prazo e aporte mensal planejado.
- Objetivos podem ser vinculados a reservas e usam o saldo reservado como progresso real.
- Progresso percentual, valor acumulado, prazo e previsao em meses sao calculados automaticamente.
- Visao geral exibe o proximo objetivo com dados reais.
- Banco local existente evolui automaticamente para armazenar objetivos.
- Testes automatizados confirmam persistencia e integracao entre objetivos e reservas.

## 0.1.0-alpha.17 - 2026-09-27

- Secao de reservas substitui o placeholder e passa a permitir cadastro persistente.
- Aportes e retiradas podem ser registrados escolhendo a reserva e a conta relacionada.
- Total reservado e exibido na visao geral e na pagina de reservas.
- Dinheiro reservado deixa de compor o saldo disponivel sem ser contabilizado como despesa.
- Retiradas acima do saldo da reserva sao impedidas.
- Banco local existente evolui automaticamente sem apagar os dados cadastrados.
- Testes automatizados cobrem aportes, retiradas e integracao com o saldo disponivel.

## 0.1.0-alpha.16 - 2026-09-23

- Paineis recolhiveis de fatura anterior e proxima passam a ocupar toda a largura do card.
- Conteudo das faturas recebe animacao suave de deslocamento e opacidade ao ser aberto.

## 0.1.0-alpha.15 - 2026-09-23

- Marcador do controle de limite corrigido para aparecer como um circulo completo.
- Faturas anterior e proxima transformadas em paineis recolhiveis, fechados por padrao.
- Cartoes cadastrados agora podem ser editados: nome, conta, limite total, fechamento e vencimento.
- Campos monetarios deixam de exibir botoes de incremento e decremento.
- Formatacao monetaria com separadores de milhar e duas casas decimais padronizada durante a entrada.
- Edicao do cartao protege o limite contra valores inferiores ao total ja utilizado.

## 0.1.0-alpha.14 - 2026-09-23

- Campo numerico e botao de atualizacao do limite substituidos por controle deslizante com marcador circular.
- Valores utilizado, disponivel, percentual e limite escolhido mudam em tempo real durante o arraste.
- Novo limite de uso e salvo automaticamente ao terminar o ajuste.
- Limite total do banco e limite de uso escolhido passam a ser armazenados separadamente.
- Cartoes existentes recebem automaticamente o limite atual como teto inicial, sem perda de dados.

## 0.1.0-alpha.13 - 2026-09-23

- Compras no cartao de credito deixam de reduzir o saldo disponivel antes do pagamento da fatura.
- Pagamento da fatura passa a ser o unico momento em que o saldo da conta vinculada e reduzido.
- Visao geral passa a exibir os cartoes, o limite utilizado, o valor disponivel e uma barra de progresso.
- Pagina `Contas e cartoes` recebe o mesmo acompanhamento consolidado de limites.
- Limite de cada cartao pode ser ajustado diretamente nas duas telas e e persistido no banco local.
- Testes automatizados cobrem compra, pagamento de fatura e alteracao de limite.

## 0.1.0-alpha.12 - 2026-09-23

- Campos monetarios substituidos por entradas numericas com duas casas decimais e separadores automaticos.
- Digitacao de `100` passa a representar diretamente `R$ 100,00`.
- Limite de seguranca de R$ 1 bilhao aplicado a lancamentos, pendencias, cartoes e saldos iniciais.
- Validacao de valores aplicada tanto na interface quanto na camada de persistencia.
- Banco local de teste confirmado no estado inicial, sem os lancamentos de valores absurdos.
- Copia de seguranca criada antes da verificacao dos dados locais.

## 0.1.0-alpha.11 - 2026-09-23

- Saldo disponivel passa a descontar faturas ainda nao pagas sem duplicar o desconto no pagamento.
- Testes explicitos garantem `R$ 100 - R$ 100 = R$ 0` e `-R$ 100 + R$ 100 = R$ 0`.
- Botao de pagamento das pendencias redimensionado para nao sobrepor o valor.
- Faturas pendentes integradas a secao `Pendencias` e ao total previsto do mes.
- Gerenciamento de pagamentos recorrentes com opcao para encerrar a recorrencia.
- Encerramento remove ocorrencias futuras nao pagas e preserva pagamentos anteriores.
- Faturas anterior, atual e seguinte passam a ter paineis visualmente separados e identificados.
- Contas e categorias usadas por pendencias passam a ser protegidas contra exclusao.

## 0.1.0-alpha.10 - 2026-09-22

- Faturas do periodo selecionado e do mes seguinte exibidas em conjunto.
- Lancamentos no cartao informam explicitamente a fatura de destino.
- Seletor de periodos passa a oferecer meses anteriores e futuros dinamicamente.
- Nova secao `Pendencias` para contas a pagar, vencidas e pagas.
- Cadastro de pendencias avulsas com valor, vencimento, conta e categoria.
- Cadastro de contas recorrentes mensais, com geracao automatica das proximas ocorrencias.
- Confirmacao de pagamento cria a despesa e reduz o saldo somente no momento do pagamento.
- Resumo de pendencias e valor previsto adicionado a visao geral.
- Banco local evolui automaticamente para suportar agenda e recorrencias.

## 0.1.0-alpha.9 - 2026-09-22

- Compras no cartao de credito integradas ao formulario de lancamentos.
- Definicao automatica da fatura conforme a data da compra e o dia de fechamento.
- Resumo de faturas por cartao com total, valor pendente, status e vencimento.
- Calculo automatico de limite utilizado e disponivel.
- Pagamento integral da fatura pela conta vinculada ao cartao.
- Compra reconhecida como despesa e pagamento tratado sem duplicidade nos totais.
- Edicao e exclusao de compras recalculam faturas e limites automaticamente.
- Protecao contra exclusao de cartoes que possuem compras ou pagamentos.
- Banco local atualizado automaticamente sem apagar dados existentes.

## 0.1.0-alpha.8 - 2026-09-22

- Saldo consolidado passa a considerar todo o historico ate o fim do periodo selecionado.
- Calculo individual do saldo de cada conta.
- Lista de saldos adicionada a pagina de contas e cartoes.
- Transferencias entre contas com origem, destino, data, descricao e valor.
- Transferencias nao alteram os totais de receitas e despesas nem criam valor no saldo consolidado.
- Busca, filtro, edicao e exclusao de transferencias integrados a pagina de lancamentos.
- Banco local existente evolui automaticamente sem apagar os dados cadastrados.
- Testes automatizados adicionados para saldos historicos e neutralidade das transferencias.

## 0.1.0-alpha.7 - 2026-09-22

- Navegacao funcional entre todas as secoes do menu lateral.
- Indicacao visual da secao atualmente selecionada.
- Nova pagina de lancamentos com busca e filtros por tipo, conta e categoria.
- Edicao e exclusao logica de receitas e despesas.
- Estado vazio para pesquisas sem resultados.
- Pagina inicial de contas e cartoes com acesso aos cadastros existentes.
- Testes de persistencia ampliados para consulta, edicao e exclusao de lancamentos.

## 0.1.0-alpha.6 - 2026-09-22

- Ligacao direta e confiavel dos botoes de exclusao de contas, categorias e cartoes.
- Estado vazio compacto na tela de cartoes, sem grande area em branco.
- Lista de cartoes passa a ocupar espaco somente quando houver cadastros.
- Configuracao de compilacao estabilizada entre as versoes Debug e Release.

## 0.1.0-alpha.5 - 2026-09-22

- Exclusoes agora atualizam as listas imediatamente e exibem confirmacao de sucesso.
- Primeiro cadastro de cartoes de credito com limite, fechamento e vencimento.
- Vinculo do cartao com a conta usada para pagamento.
- Exclusao de cartoes com confirmacao.
- Protecao contra exclusao de contas vinculadas a cartoes.
- Evolucao automatica do banco local existente sem apagar os dados.

## 0.1.0-alpha.4 - 2026-09-22

- Alinhamento horizontal e vertical padronizado em todos os botoes.
- Alinhamento corrigido nos botoes de cadastro e novo lancamento.
- Seletor de data compacto com formato `dd/MM/aaaa` e calendario suspenso.
- Transicoes suaves de cor, escala e movimento nos estados de mouse e clique.
- Estados visuais proprios para botoes principais, secundarios, icones e exclusao.

## 0.1.0-alpha.3 - 2026-09-22

- Listas de contas e categorias com altura limitada e rolagem independente.
- Formularios mantidos dentro do enquadramento mesmo com muitos cadastros.
- Exclusao de contas e categorias com confirmacao.
- Protecao contra exclusao de cadastros usados em lancamentos.
- Protecao para manter pelo menos uma conta.
- Tipos de conta apresentados em portugues.

## 0.1.0-alpha.2 - 2026-09-22

- Cadastro persistente de contas com tipo e saldo inicial.
- Cadastro persistente de categorias de receita ou despesa.
- Contas e categorias novas disponiveis no formulario de lancamento.
- Selecao mensal conectada aos totais e lancamentos do periodo.
- Testes de persistencia ampliados para contas e categorias.

## 0.1.0-alpha.1 - 2026-09-22

- Estrutura inicial em Avalonia e .NET 10.
- Painel baseado no prototipo visual aprovado.
- Banco SQLite local criado automaticamente.
- Contas e categorias iniciais.
- Cadastro persistente de receitas e despesas.
- Atualizacao dos totais e ultimos lancamentos.
- Testes de espacos financeiros e persistencia SQLite.
