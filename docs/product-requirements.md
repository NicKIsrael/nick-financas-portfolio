# Requisitos do produto

## Objetivo

Oferecer controle financeiro pessoal e do casal em um aplicativo desktop que funciona offline e sincroniza entre dispositivos quando o computador-servidor estiver disponivel.

## Usuarios e espacos

- Cada pessoa possui login proprio.
- Cada usuario possui um espaco financeiro individual.
- O casal possui um espaco financeiro conjunto.
- Um usuario acessa somente os espacos dos quais e membro.
- Cada alteracao registra seu autor e dispositivo de origem.

## Funcionalidades do MVP

- Contas financeiras: conta corrente, carteira, poupanca e investimento.
- Cartoes de credito e acompanhamento de faturas.
- Receitas, despesas e transferencias.
- Categorias e subcategorias.
- Parcelamentos e recorrencias.
- Visao mensal com saldo, receitas, despesas e comparacao historica.
- Reservas com saldo, aportes e retiradas.
- Objetivos com valor alvo, progresso, valor restante e previsao de conclusao.
- Relatorios por periodo e categoria.
- Backup e restauracao.
- Multiplos usuarios e espacos individuais ou conjuntos.

## Reservas e objetivos

- Uma reserva representa dinheiro efetivamente separado do saldo disponivel.
- Uma reserva pode existir sem objetivo associado.
- Um objetivo pode usar uma reserva como fonte de progresso.
- A previsao de conclusao usa a media real dos aportes mensais e, quando informado, o aporte planejado.

## Organizacao funcional das secoes

### Visao geral

- Painel resumido do espaco financeiro e do periodo selecionados.
- Exibe saldo disponivel, receitas, despesas, total em reservas, comparacao historica, proximo objetivo e ultimos lancamentos.
- Oferece atalhos para novo lancamento e para as respectivas areas detalhadas.

### Lancamentos

- Extrato completo de receitas, despesas e transferencias.
- Cada registro identifica data, descricao, valor, conta, categoria, autor e, quando aplicavel, cartao, fatura, parcela ou recorrencia.
- Permite criar, pesquisar, filtrar, editar e excluir movimentacoes, alem de consultar lancamentos futuros.

### Contas e cartoes

- Centraliza as fontes e os meios usados para movimentar dinheiro.
- Contas possuem nome, tipo, saldo inicial, saldo calculado e historico.
- Cartoes possuem limite, fechamento, vencimento, conta de pagamento, faturas e compras parceladas.

### Reservas

- Representam dinheiro efetivamente separado e indisponivel para gastos comuns, sem ser tratado como despesa.
- Permitem aportes, retiradas, transferencias, consulta de historico e vinculo opcional com objetivos.

### Objetivos

- Representam planos financeiros com valor alvo, progresso, valor restante, prazo, aporte planejado e previsao de conclusao.
- Podem usar uma reserva como fonte do progresso, mas continuam conceitualmente separados dela.

### Relatorios

- Consolidam e comparam receitas, despesas, categorias, contas, cartoes, membros, reservas e objetivos por periodo.
- Os totais e graficos devem permitir consultar os lancamentos que formam cada resultado.

### Membros

- Gerencia participantes de espacos compartilhados e a autoria das alteracoes.
- Os papeis previstos sao administrador e membro.
- Espacos individuais nao aceitam outros membros.

## Relacao entre as secoes

1. Contas e cartoes definem onde e como o dinheiro e movimentado.
2. Lancamentos registram as movimentacoes financeiras.
3. Visao geral e relatorios consolidam os lancamentos.
4. Reservas separam parte do dinheiro do saldo disponivel.
5. Objetivos acompanham a finalidade e o progresso do dinheiro acumulado.
6. Membros controlam o acesso e identificam a autoria em espacos compartilhados.

## Fora do primeiro MVP

- Aplicativo para iPhone.
- Importacao bancaria automatica.
- Publicacao em loja de aplicativos.
- Hospedagem em nuvem.
