# Levantamento do processo operacional

Atualizado em 13/10/2026 com as respostas enviadas por Ari. O objetivo é documentar o fluxo real antes de definir as telas e regras do PIT-CRM.

## Perfil da operação

- Os serviços mais frequentes são de civil e ar-condicionado.
- Há clientes com contratos fixos para manutenções preventivas e corretivas.
- Também há atendimentos corretivos pontuais, chamados de spots.

## Entrada e identificação das demandas

- As demandas chegam principalmente por WhatsApp; também chegam por ligação e, menos frequentemente, por e-mail.
- Cada novo acionamento normalmente representa uma nova demanda.
- As demandas geralmente vêm vinculadas a um número de chamado/ticket, mas isso depende do cliente.
- Um cliente pode abrir vários chamados em locais diferentes. Em geral, há uma demanda por local; raramente podem ocorrer duas demandas no mesmo local e horário.
- Modelagem recomendada: cada demanda deve receber um identificador próprio no PIT-CRM. O número de chamado/ticket deve ser uma referência externa pesquisável, associada ao cliente e ao local; não pode ser tratado como obrigatório nem como identificador global único sem validar exceções por cliente.

## Fluxo informado

1. Receber a demanda, geralmente pelo WhatsApp, ou por ligação/e-mail.
2. Realizar a visita técnica inicial, quando aplicável; ela é o primeiro atendimento.
3. Preparar e enviar o orçamento.
4. Após a autorização do orçamento, programar e executar o serviço.
5. Acompanhar a execução e reunir evidências (fotos, vídeos ou relatório técnico).
6. Obter autorização do cliente para emitir a nota fiscal.
7. Emitir e enviar a nota fiscal.
8. Acompanhar o recebimento, geralmente previsto para cerca de 30 dias após a emissão, conforme o cliente.

## Status e responsabilidades

- **Autorizado:** orçamento enviado e aprovado pelo cliente.
- **Programado:** no sistema, separar a visita técnica inicial da execução agendada após autorização do orçamento.
- **Executado:** serviço concluído e pronto para faturar/emitir nota fiscal.
- **Pausado:** serviço adiado pelo cliente ou pela empresa, inclusive por falta de mão de obra ou material.
- **Retificar:** orçamento enviado/autorizado precisa ser alterado.
- Toda a equipe de operação pode criar e alterar demandas. O responsável à frente acompanha o andamento. Podem ser criadas permissões específicas para os demais perfis.
- Perfis iniciais propostos: **Administrador** (acesso completo e gestão de usuários/configurações), **Gestor** (operação, indicadores e dados financeiros conforme permissão) e **Operador** (cria/altera demandas, agenda e registra evidências). As permissões podem ser ajustadas por perfil/usuário.
- **Financeiro** é um perfil opcional para custos, cálculo LL, notas e recebimentos se houver essa divisão. **Prestador** poderá, futuramente, acessar apenas demandas atribuídas para atualizar execução e enviar evidências. Acesso de **cliente** fica fora do MVP até confirmação de necessidade.
- **Cancelado:** cliente cancela a solicitação ou autorização. **Retrabalho:** nova atuação em garantia, vinculada à demanda original.

### Distinção a preservar

O processo contém dois momentos diferentes de autorização: aprovação do orçamento antes da execução e autorização do cliente para emitir a nota fiscal após apresentar as evidências. Devem ser registrados como marcos distintos.

## Programação e execução

- A data é combinada conforme disponibilidade dos técnicos e do cliente; o andamento é acompanhado até a atividade poder seguir para faturamento.
- Geralmente acionam técnicos que já trabalham com a empresa. Quando não há disponibilidade ou profissional adequado, buscam indicações e anúncios do Google.
- As fotos são as evidências mais importantes, especialmente antes, durante e depois do serviço; também podem ser enviados vídeos e relatórios técnicos.

## Aditivos e adiantamentos

- **Aditivo** significa acrescentar uma linha ao serviço/orçamento ou, em alguns casos, ajustar a linha existente.
- **ADD** significa **adiantamento**, tanto de recebimentos quanto de pagamentos a prestadores.
- Essas expressões representam coisas diferentes e precisam ser tratadas separadamente no sistema.

## Faturamento e recebimento

- Uma demanda entra em **Faturar** quando está concluída/executada e pronta para emissão da nota fiscal.
- Normalmente os clientes não liberam adiantamento; o pagamento final costuma ocorrer cerca de 30 dias após a emissão da nota. Há casos isolados de adiantamento para iniciar a atividade.
- É necessário registrar número da nota, data de emissão, envio ao cliente, previsão de recebimento e data do pagamento efetivo.
- Ari acompanha as cobranças atrasadas e hoje usa a cor vermelha na data de recebimento como alerta.
- A aba de faturamento é a visão mais consultada; também acompanha demandas em andamento para comparar o avanço operacional com o faturamento.
- É importante manter o histórico/relatório do que já foi faturado até uma data de referência.

## Custos e compras

- Cada demanda tem uma equipe ou prestador responsável. O valor contratado geralmente é fechado como pacote, já incluindo deslocamento, tempo e materiais ou recursos necessários.
- Compras de materiais são registradas como despesas do chamado específico. Luciana geralmente realiza essas compras.
- O cálculo financeiro é feito em planilha separada. O custo estimado é digitado manualmente. A empresa considera fixos os 28% de imposto/administração. “LL” significa Lucro Líquido e a meta mínima é 20%. Pela fórmula da planilha, o percentual é calculado sobre o valor aprovado.
- O custo estimado aparece como uma soma de valores dentro de uma fórmula, sem composição nomeada por item. A planilha não contém cliente, chamado ou demanda, nem referências a outro arquivo.
- Ari aprovou trazer o cálculo LL para o PIT-CRM. Os parâmetros de 28% e meta mínima de 20% são da empresa piloto; manter configuráveis por organização.

## Prioridades para o sistema

- O cadastro de novas demandas é importante desde a primeira etapa.
- A maior oportunidade de economizar tempo ou reduzir erros parece estar da conclusão do serviço em diante: autorização para faturar, emissão da nota e acompanhamento do recebimento.
- As colunas atuais são consideradas importantes pelo responsável; qualquer simplificação deve ser validada com ele.
- A primeira versão deve priorizar o acompanhamento operacional e o fluxo de faturamento/recebimento, preservando o histórico.

## Pontos ainda a esclarecer

- Validar com Ari a matriz de ações de cada perfil (consulta, criação, alteração, aprovação e dados financeiros) e se haverá um perfil Financeiro separado. Os perfis-base já definidos são Administrador, Gestor e Operador.
- Quais categorias de custo devem compor o custo estimado manual no PIT-CRM; a integração do cálculo já foi aprovada.
- Se há clientes sem número de chamado e quais outros identificadores/referências usam nesses casos.
