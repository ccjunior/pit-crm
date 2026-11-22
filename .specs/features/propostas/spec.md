# Módulo de solicitações comerciais e propostas

**Status:** Especificação para revisão do usuário — não aprovada para design ou implementação.
**Autorização:** em 21/11/2026, o usuário autorizou seguir com a construção do módulo. As regras explicitamente fornecidas estão separadas das recomendações do agente abaixo.

## Problema

O PIT Obras registra demandas operacionais manualmente, mas ainda não acompanha solicitações de orçamento, a decisão de preparar uma proposta, suas versões e o resultado comercial. Quando uma proposta é aprovada, o trabalho deve entrar na operação sem perder o vínculo com o escopo e as condições que o cliente aceitou.

## Objetivos

- Acompanhar solicitações comerciais de clientes existentes e de prospects, mesmo quando a empresa decide não enviar proposta.
- Registrar propostas e revisões em um fluxo visual Kanban e em uma lista pesquisável.
- Ao registrar a aprovação do cliente, criar automaticamente um rascunho de demanda, que o Operador completa e confirma antes de entrar no fluxo operacional.
- Preservar o caminho atual de criação manual de demandas para clientes existentes.

## Direções confirmadas pelo usuário

- Existem dois caminhos para iniciar trabalho: criar uma demanda manualmente ou converter uma proposta aprovada.
- A solicitação e a aprovação serão registradas manualmente; leitura automatizada de e-mail está fora do escopo desta etapa.
- Uma proposta aprovada gera uma demanda; a demanda pode ter subitens.
- A aprovação cria um rascunho; o Operador confere/completa dados faltantes e confirma a demanda para a operação.
- Administrador e Gestor gerenciam propostas; Operador trata demandas.
- O módulo terá visualização de propostas e fluxo comercial. Kanban é desejado como possibilidade.
- No primeiro recorte, o módulo registra e acompanha os dados; envio ao cliente e documento/PDF ocorrem fora do sistema.

## Recomendação funcional

O cartão representa uma **solicitação/oportunidade comercial**, e não uma revisão específica. As revisões ficam no detalhe do cartão, para a negociação permanecer num único lugar e solicitações sem proposta continuarem visíveis no funil. A mesma coleção deve ser exibida em Kanban e lista.

### Fluxo recomendado

1. Registrar solicitação de cliente ou prospect, com canal de entrada e resumo do serviço.
2. Avaliar se a empresa enviará orçamento. Se decidir não cotar, encerrar com motivo obrigatório.
3. Se cotar, elaborar uma proposta estruturada com itens e revisões sucessivas; registrar manualmente quando foi enviada ao cliente.
4. Registrar aprovação ou resultado negativo. Para aprovação, registrar quem confirmou, quando, por qual canal e uma observação/evidência textual.
5. Na aprovação, criar um único rascunho de demanda, vinculado à solicitação e à versão aprovada. Repetições da ação não podem criar duplicatas.
6. O Operador confere ou completa cliente, local e dados operacionais obrigatórios. Se a solicitação veio de um prospect, seleciona cliente existente correspondente ou cadastra o novo cliente e local.
7. Ao confirmar, criar/ativar a demanda operacional como “Recebida” e copiar os itens do escopo aprovado como subitens, preservando o retrato aprovado e a origem.

### Etapas sugeridas para o Kanban

`Solicitação recebida` → `Em avaliação` → `Em elaboração` → `Enviada` → `Em negociação` → `Aprovada`.

Resultados terminais: `Não cotada`, `Recusada`, `Expirada` e `Cancelada`. “Não cotada” é uma decisão interna; “Recusada” é uma resposta do cliente. A aprovação exige registro manual do usuário autorizado. Revisões criadas após o envio retornam a negociação à etapa de elaboração/envio, mantendo as versões anteriores.

## Requisitos de aceite propostos

### P1 — Solicitação e avaliação comercial

1. **PROP-01:** WHEN Administrador ou Gestor registra solicitação THEN o sistema SHALL permitir associá-la a um cliente existente ou registrar dados de um prospect, junto com canal de entrada e resumo do serviço.
2. **PROP-02:** WHEN o responsável decide não preparar proposta THEN o sistema SHALL permitir encerrar a solicitação como “Não cotada” e exigir o motivo.
3. **PROP-03:** WHEN um usuário abre o módulo THEN o sistema SHALL oferecer Kanban e lista sobre os mesmos registros, com pesquisa e filtros por etapa, cliente/prospect e responsável.
4. **PROP-04:** WHEN uma solicitação muda de etapa THEN o sistema SHALL validar a transição permitida e registrar usuário, etapa anterior, nova etapa e data.

### P1 — Proposta e revisões

5. **PROP-05:** WHEN Administrador ou Gestor prepara uma proposta THEN o sistema SHALL associá-la a uma solicitação e registrar um ou mais itens com descrição e valor.
6. **PROP-06:** WHEN uma proposta enviada precisa de alteração THEN o sistema SHALL criar uma nova revisão numerada, preservar a revisão anterior e registrar qual revisão foi enviada/aprovada.
7. **PROP-07:** WHEN uma proposta é registrada como enviada THEN o sistema SHALL guardar data e canal de envio, sem enviar e-mail ou gerar documento.
8. **PROP-08:** WHEN o cliente aprova uma proposta THEN o sistema SHALL registrar quem informou, a data da aprovação, o canal de retorno e uma observação textual opcional.

### P1 — Conversão para demanda

9. **PROP-09:** WHEN uma proposta é marcada como aprovada THEN o sistema SHALL criar automaticamente um único rascunho de demanda ligado à solicitação e à revisão aprovada.
10. **PROP-10:** IF a confirmação de aprovação for repetida ou reenviada THEN o sistema SHALL retornar o mesmo rascunho e não criar outra demanda.
11. **PROP-11:** WHILE a demanda estiver em rascunho THEN o sistema SHALL mantê-la fora das listas, contagens e etapas operacionais até que o Operador a confirme.
12. **PROP-12:** WHEN o Operador confirma o rascunho THEN o sistema SHALL exigir cliente, local, descrição e origem, criar ou vincular os registros faltantes de cliente/local após verificação e colocar a demanda como “Recebida”.
13. **PROP-13:** WHEN o rascunho é convertido THEN o sistema SHALL copiar os itens da revisão aprovada para subitens da demanda e preservar a descrição/valores aprovados e sua rastreabilidade.
14. **PROP-14:** IF uma proposta já aprovada sofre mudança comercial THEN o sistema SHALL exigir nova revisão e nova aprovação antes de alterar a origem aprovada da demanda.

### P1 — Acesso e isolamento

15. **PROP-15:** O sistema SHALL permitir que Administrador e Gestor criem e gerenciem solicitações comerciais, propostas, revisões e resultados; Operador SHALL poder consultar/completar rascunhos de demanda originados de proposta e gerenciar demandas conforme as permissões existentes.
16. **PROP-16:** O sistema SHALL aplicar isolamento por organização em todas as operações de consulta e escrita de solicitações, propostas, revisões e conversões em rascunho de demanda.

## Regras e limites

- A solicitação/oportunidade pode existir sem proposta, para registrar decisão de não cotar.
- Uma solicitação contém uma negociação com revisões sucessivas; alternativas comerciais paralelas não estão incluídas no primeiro recorte.
- Uma revisão aprovada gera uma demanda; itens viram subitens operacionais. Um subitem não tem cliente, local, autorização comercial ou ciclo de demanda próprios.
- Demanda criada manualmente segue o fluxo existente e pode receber subitens.
- O caminho atual da demanda continua disponível; proposta aprovada é uma origem adicional, não substitui a demanda manual.
- Contratos não são condição obrigatória nem fazem parte da gestão deste módulo. A associação opcional contrato-demanda permanece para decisão/entrega própria.
- E-mail, envio automático, portal de cliente, assinatura eletrônica e geração de PDF/documento ficam fora do primeiro recorte.
- O módulo armazena propostas estruturadas; quem envia ao cliente faz isso fora do PIT Obras e registra data/canal.
- Valores são informados por item; desconto, impostos automáticos, cálculo de custo/LL e condições de pagamento configuráveis ficam fora desta proposta inicial até validação.

## Suposições de produto para revisão

| Suposição/recomendação | Padrão sugerido | Motivo | Confirmada? |
| --- | --- | --- | --- |
| Cartão do quadro | Uma solicitação/oportunidade; revisões ficam dentro do cartão | Inclui solicitações não cotadas e evita duplicar negociações no Kanban | Padrão recomendado após “pode seguir”; validar na aprovação da especificação |
| Estados | Etapas e resultados listados acima; aprovação registrada manualmente | Torna avaliável o processo sem integração externa | Não |
| Proposta paralela | Uma negociação e revisões sequenciais por solicitação | Histórico simples, alinhado ao fluxo já discutido | Não |
| Prospect aprovado | No rascunho, operador vincula cliente existente ou cadastra cliente/local antes de confirmar | Evita criar cliente duplicado e preserva os campos obrigatórios da demanda operacional | Não |
| Item comercial | Descrição e valor por item; quantidade/unidade podem ser adicionadas se necessárias | Atende proposta por pacote e itens discriminados sem impor um catálogo de custos | Não |
| Evidência de aprovação | Pessoa, data, canal e observação; sem anexos no primeiro recorte | Registra a confirmação manual sem depender de integração ou armazenamento documental | Não |
| Revisão interna | Não criar uma etapa de aprovação interna separada inicialmente | Não foi solicitado um segundo aprovador e os papéis autorizados já são Administrador/Gestor | Não |

## Fora de escopo

| Item | Motivo |
| --- | --- |
| Leitura automática de e-mails e captura de anexos | Explicitamente excluída pelo usuário nesta etapa. |
| Envio automático de proposta, portal externo ou assinatura | Envio/aceite serão registrados manualmente fora do sistema. |
| Documento PDF ou modelo comercial | Usuário escolheu registrar/acompanhamento sem gerar documento no primeiro recorte. |
| Gestão de contratos | Vínculo contratual pode ser especificado separadamente. |
| Orçamento de custo, LL, impostos e comissões | Regras financeiras configuráveis permanecem pendentes no roadmap. |

## Questões para aprovação desta especificação

1. Aprovar o cartão por solicitação/oportunidade com revisões no detalhe?
2. Aprovar as etapas e resultados terminais propostos?
3. Aprovar as regras de proposta, registro manual de aprovação e conversão descritas acima?
4. Confirmar se os valores dos itens bastam, ou se quantidade e unidade são necessários desde o primeiro corte?

## Fontes

- [Contexto e respostas do usuário](context.md).
- [Fluxo e modelo atual](../../../docs/02-FLUXO-E-MODELO.md), que deverá ser atualizado quando este módulo for aprovado.
- [Direção e escopo](../../../docs/01-DIRECAO-E-ESCOPO.md), onde o funil comercial amplo consta como fase posterior.
- [Primeira entrega operacional](../primeira-entrega-operacional/context.md), decisões D02 e D07: requisitos e estado inicial da demanda operacional atual.
