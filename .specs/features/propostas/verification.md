# Verificação local do fluxo comercial (T26)

O ajuste responsivo de Propostas em 22/11/2026 reorganizou filtros, formulário e itens da revisão em telas estreitas, com quebra dos controles de ação e paginação. O build Angular passou com o aviso existente de 520,37 kB no bundle inicial. A conferência visual autenticada em viewport mobile permanece pendente.

Registro de 22/11/2026. A API foi exercitada com sessões reais de teste e com PostgreSQL local em schemas temporários.

| Cobertura | Evidência |
| --- | --- |
| Solicitação de cliente, avaliação, revisão, envio e aprovação (PROP-01, PROP-04–PROP-09) | Cenário HTTP em `CustomersApiTests.VerifyCommercialConversionAsync`. A primeira tentativa encontrou HTTP 409 ao criar a primeira revisão. A persistência agora registra a revisão como nova entidade. |
| Aprovação repetida e simultânea (PROP-10) | As duas chamadas e o reenvio retornam o mesmo ID de rascunho. Em conflito de gravação, a API relê o resultado da aprovação e reutiliza o rascunho da mesma revisão. |
| Confirmação e cópia de descrição, valor e origem (PROP-11–PROP-14) | O Operador vincula um local do cliente existente, confirma o rascunho e consulta o subitem pelo endpoint de demanda. A cobertura do cenário de prospect permanece pendente. |
| Perfis e organizações (PROP-16–PROP-17) | Gestor cria e aprova. Operador recebe 403 ao criar proposta e consegue confirmar a demanda. Outra organização não pode reutilizar o cliente, consultar ou aprovar a proposta, nem consultar o rascunho. A cobertura representa as operações exercitadas. |
| Regressão Angular | `npm test -- --watch=false` aprovou 30 testes após alinhar os exemplos de demanda ao campo `subitems`. O build de produção passou com 520,37 kB no bundle inicial (orçamento de 500 kB). |

Ainda faltam a inspeção autenticada do Kanban e da fila de rascunhos no navegador, cenários de nova revisão após aprovação, prospect, desfechos comerciais e cobertura completa dos filtros e das permissões. O gate original da skill não foi executado. T26 segue em validação.
