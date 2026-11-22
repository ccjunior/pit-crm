# Verificação local do fluxo comercial (T26)

## Pendências adiadas para o fechamento do módulo Comercial

Conforme orientação do usuário em 22/11/2026, ficam para a validação integrada ao concluir o módulo Comercial completo: (1) confirmar um rascunho pela interface autenticada como Operador, com um rascunho disponível; (2) inspecionar as telas do fluxo em dispositivo touch/emulador móvel. A responsividade já foi inspecionada em viewport estreito via zoom do navegador, e a confirmação por Operador e permissões foram cobertas por testes HTTP. Essas duas conferências manuais não bloqueiam o início das etapas seguintes; devem ser retomadas no fechamento do módulo.

Em 22/11/2026, na sessão autenticada do Operador, o menu comercial não apareceu, a navegação direta a `/propostas` retornou à visão geral e a lista de clientes abriu sem ação de cadastro. A fila de rascunhos abriu vazia. O preenchimento e a confirmação de rascunho pelo Operador foram cobertos pelos testes HTTP, mas não foram repetidos na interface porque não havia rascunho pendente.

Em 22/11/2026, a sessão autenticada de Gestor acessou Propostas e criou o prospect sintético TESTE GESTOR T26. O quadro e o histórico exibiram o registro e o usuário Gestor. O registro permanece no banco local. A confirmação operacional pelo Gestor e a inspeção da interface do Operador permanecem pendentes.

Em 22/11/2026, foi verificada a tela autenticada de Propostas com zoom de 250% em janela de aproximadamente 1050 px, equivalente a cerca de 420 px de largura CSS. Filtros e formulário ficaram em uma coluna, ações permaneceram acessíveis e o quadro apresentou rolagem horizontal própria. Essa verificação não simula toque nem cobre todas as telas. O zoom foi restaurado. A suíte Angular passou 34/34 incluindo guarda de acesso de Administrador, Gestor e Operador; as permissões da API foram verificadas por testes PostgreSQL. A inspeção visual autenticada com Gestor e Operador ainda não foi executada.

Em 22/11/2026, com sessão Administrador no Chrome, foi criado o prospect sintético TESTE LOCAL T26, iniciada avaliação, criada revisão de R$ 125,00, registrado envio e aprovação simulados e confirmado o rascunho com cliente e local de teste. A demanda DEM-0002 apareceu como Recebida e a fila ficou vazia. O registro permanece em `pitcrm_dev`. O cadastro da localidade Angular pt-BR corrigiu a exibição de valores e ações após a revisão. A verificação mobile e os demais perfis no navegador permanecem pendentes.

Em 22/11/2026, a sessão local de Administrador abriu as rotas de Propostas e Rascunhos no Chrome. O quadro apresentou uma solicitação existente e a fila informou ausência de rascunhos pendentes. A navegação foi feita pelo endereço, pois a automação de clique na janela não conseguiu obter a geometria. Fluxo completo interativo e viewport mobile seguem pendentes.

Na retomada, o ajuste da janela permitiu usar cliques: o formulário de nova solicitação abriu e fechou, a alternância para lista apresentou o mesmo registro do quadro e o detalhe exibiu ações e histórico. A verificação não alterou o registro existente. O fluxo de gravação pela interface e o viewport mobile seguem pendentes.

Em 22/11/2026, Clientes, Nova Demanda, Propostas e Rascunhos passaram a carregar por rota sob demanda. O build Angular mais recente passou com bundle inicial de 416,31 kB, abaixo do limite de 500 kB. A navegação autenticada de Clientes e Nova Demanda ainda requer conferência visual.

O ajuste responsivo de Propostas em 22/11/2026 reorganizou filtros, formulário e itens da revisão em telas estreitas, com quebra dos controles de ação e paginação. A conferência em zoom de 250% em tela desktop foi registrada acima; inspeção touch/mobile permanece adiada para o fechamento integrado do Comercial por orientação do usuário.

Registro de 22/11/2026. A API foi exercitada com sessões reais de teste e com PostgreSQL local em schemas temporários.

Cobertura adicional em 22/11/2026: a suíte backend completa passou 8/8 no PostgreSQL local, executada com schemas temporários e removidos pelos testes. O cenário de prospect confirma nova revisão e reaprovação sobre o mesmo rascunho, impede vinculação de cliente pelo Operador, permite vinculação pelo Gestor e confirmação pelo Operador. As tentativas entre organizações cobrem lista, detalhe, edição, mudança de etapa, revisão, envio, aprovação e lista, detalhe, edição e confirmação de rascunho. O Operador recebeu 403 nas consultas e ações comerciais exercitadas. A suíte Angular passou 34/34. O build de produção Angular passou com bundle inicial 416,31 kB (limite 500 kB). Essas verificações não substituem as duas inspeções manuais adiadas.

| Cobertura | Evidência |
| --- | --- |
| Solicitação de cliente, avaliação, revisão, envio e aprovação (PROP-01, PROP-04–PROP-09) | Cenário HTTP em `CustomersApiTests.VerifyCommercialConversionAsync`. A primeira tentativa encontrou HTTP 409 ao criar a primeira revisão. A persistência agora registra a revisão como nova entidade. |
| Aprovação repetida e simultânea (PROP-10) | As duas chamadas e o reenvio retornam o mesmo ID de rascunho. Em conflito de gravação, a API relê o resultado da aprovação e reutiliza o rascunho da mesma revisão. |
| Confirmação e cópia de descrição, valor e origem (PROP-11–PROP-14) | O Operador vincula um local do cliente existente, confirma o rascunho e consulta o subitem pelo endpoint de demanda. A cobertura do cenário de prospect permanece pendente. |
| Perfis e organizações (PROP-16–PROP-17) | Gestor cria e aprova. Operador recebe 403 ao criar proposta e consegue confirmar a demanda. Outra organização não pode reutilizar o cliente, consultar ou aprovar a proposta, nem consultar o rascunho. A cobertura representa as operações exercitadas. |
| Regressão Angular (verificação anterior ao lazy loading das rotas) | `npm test -- --watch=false` aprovou 30 testes após alinhar os exemplos de demanda ao campo `subitems`. O build de produção então ficou em 520,37 kB no bundle inicial; a medição atual após lazy loading está registrada acima. |

Ainda faltam a inspeção autenticada do Kanban e da fila de rascunhos no navegador, cenários de nova revisão após aprovação, prospect, desfechos comerciais e cobertura completa dos filtros e das permissões. O gate original da skill não foi executado. T26 segue em validação.
