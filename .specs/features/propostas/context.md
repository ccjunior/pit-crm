# Contexto — módulo de propostas

Atualizado em 21/11/2026. A especificação foi aprovada pelo usuário; implementação em andamento.

## Direção informada pelo usuário

- Haverá duas entradas para o trabalho operacional: criação manual de demanda, como hoje, e criação de demanda após uma proposta aprovada.
- A proposta pode ser solicitada por cliente atual ou por alguém ainda não cadastrado como cliente (lead/prospect).
- A demanda é criada a partir da aprovação da proposta como rascunho; o Operador valida e completa os dados faltantes antes de confirmar sua entrada na operação.
- Uma proposta aprovada gera uma demanda; a demanda pode conter subitens.
- A entrada de solicitações e o registro de aprovação serão manuais. Não haverá, nesta etapa, leitura automática de e-mails.
- Administrador e Gestor gerenciam propostas; Operador atua nas demandas.
- O usuário pediu uma área para visualizar e acompanhar propostas e considera um quadro Kanban.
- O primeiro recorte registra e acompanha os dados; envio e documento/PDF ficam fora do sistema.
- O usuário confirmou que D01 permanece: apenas Administrador/Gestor criam ou editam clientes/locais; Operador completa o rascunho depois que esses registros forem vinculados.

## Diretrizes para o design

- Oferecer Kanban e lista como modos de visualização do mesmo fluxo. O Kanban facilita perceber gargalos; a lista favorece busca e consulta detalhada. O usuário aprovou a recomendação de um cartão por solicitação, com revisões agrupadas no detalhe.
- Registrar cada solicitação/oportunidade comercial mesmo quando a empresa decide não enviar proposta. Caso contrário, solicitações sem orçamento desaparecem do histórico e do acompanhamento do funil.
- Tratar versões da proposta como revisões da mesma negociação, sem duplicar cartões no quadro.
- Na conversão, copiar o escopo da versão aprovada para os subitens da demanda, preservando o vínculo e um retrato dos dados aprovados.
- Manter a demanda em rascunho fora dos indicadores e etapas operacionais até o Operador completar os dados necessários e confirmar.
- Manter contratos fora do escopo inicial do módulo. O vínculo opcional entre contrato e demanda permanece uma decisão separada.

## Pontos futuros fora desta entrega

- Como associar futuramente demandas a contratos; gestão de contratos permanece fora desta entrega.

## Respostas adicionais

- Permissões: Administrador e Gestor gerenciam propostas; Operador trata demandas (resposta de 21/11/2026).
- Documento: no primeiro recorte, o módulo registra e acompanha os dados; envio e PDF/documento ficam fora do sistema (resposta de 21/11/2026).
- Permissões na conversão de prospect: preservar D01; Administrador/Gestor criam/vinculam cliente e local, Operador completa e confirma a demanda (resposta de 21/11/2026).
