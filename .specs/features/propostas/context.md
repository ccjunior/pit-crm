# Contexto — módulo de propostas

Atualizado em 21/11/2026. Esta é uma descoberta em andamento; a especificação ainda não foi aprovada para design ou implementação.

## Direção informada pelo usuário

- Haverá duas entradas para o trabalho operacional: criação manual de demanda, como hoje, e criação de demanda após uma proposta aprovada.
- A proposta pode ser solicitada por cliente atual ou por alguém ainda não cadastrado como cliente (lead/prospect).
- A demanda é criada a partir da aprovação da proposta como rascunho; o Operador valida e completa os dados faltantes antes de confirmar sua entrada na operação.
- Uma proposta aprovada gera uma demanda; a demanda pode conter subitens.
- A entrada de solicitações e o registro de aprovação serão manuais. Não haverá, nesta etapa, leitura automática de e-mails.
- Administrador e Gestor gerenciam propostas; Operador atua nas demandas.
- O usuário pediu uma área para visualizar e acompanhar propostas e considera um quadro Kanban.
- O primeiro recorte registra e acompanha os dados; envio e documento/PDF ficam fora do sistema.

## Recomendações para discutir

- Oferecer Kanban e lista como modos de visualização do mesmo fluxo. O Kanban facilita perceber gargalos; a lista favorece busca e consulta detalhada. Após o usuário dizer “pode seguir” diante da recomendação de cartão por solicitação, esta é a opção usada no rascunho da especificação, sujeita à revisão do próprio documento.
- Registrar cada solicitação/oportunidade comercial mesmo quando a empresa decide não enviar proposta. Caso contrário, solicitações sem orçamento desaparecem do histórico e do acompanhamento do funil.
- Tratar versões da proposta como revisões da mesma negociação, sem duplicar cartões no quadro.
- Na conversão, copiar o escopo da versão aprovada para os subitens da demanda, preservando o vínculo e um retrato dos dados aprovados.
- Manter a demanda em rascunho fora dos indicadores e etapas operacionais até o Operador completar os dados necessários e confirmar.
- Manter contratos fora do escopo inicial do módulo. O vínculo opcional entre contrato e demanda permanece uma decisão separada.

## Pendências de produto

- Nome final do registro comercial e das etapas do quadro.
- Revisão/aprovação interna antes do envio e seus responsáveis.
- Dados mínimos e composição de preço, validade e necessidade de quantidade/unidade nos itens.
- Como registrar evidência de aprovação do cliente e quais dados o Operador deve completar para converter lead/local.
- Campos e estados operacionais dos subitens.
- Se uma solicitação pode conter propostas alternativas ou somente versões sucessivas; o rascunho recomenda revisões sucessivas.
- Como demandas cobertas por contrato serão identificadas quando existir um módulo de contratos.

## Respostas adicionais

- Permissões: Administrador e Gestor gerenciam propostas; Operador trata demandas (resposta de 21/11/2026).
- Documento: no primeiro recorte, o módulo registra e acompanha os dados; envio e PDF/documento ficam fora do sistema (resposta de 21/11/2026).
