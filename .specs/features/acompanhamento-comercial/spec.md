# Acompanhamento comercial — especificação inicial

**Estado:** detalhamento da Fase 2, com decisões pendentes.
**Autorização:** em 22/11/2026, o usuário confirmou concluir a validação da T26 antes de ampliar o módulo comercial por etapas. A confirmação inclui a evolução descrita no [documento 09](../../../docs/09-ESPECIFICACAO-MODULO-COMERCIAL.md), sem decidir seus pontos em aberto.

## Resultado

Permitir à equipe comercial acompanhar responsáveis, prazos, referências, próximas ações, atividades, visitas e motivos de resultado de uma solicitação, com histórico e isolamento por organização. A solicitação continua sendo a unidade do Kanban e da lista; suas revisões e a conversão aprovada continuam seguindo a [T26](../propostas/spec.md).

## Fontes e regras vigentes

- [Fluxo e modelo](../../../docs/02-FLUXO-E-MODELO.md): solicitação pode vir de cliente ou prospect, não cotada exige motivo, revisão aprovada origina rascunho e D01 restringe criação de cliente/local.
- [Roadmap](../../../docs/03-ROADMAP-E-PENDENCIAS.md): perfis financeiros e permissões específicas ainda precisam de definição; parâmetros e dados são da organização.
- [Documento 09](../../../docs/09-ESPECIFICACAO-MODULO-COMERCIAL.md): RF-01–RF-17 e requisitos transversais 9.2–9.7 orientam esta etapa. O exemplo de código legível e os valores citados nesse documento não definem automaticamente formato, prazos ou padrões para todas as empresas.
- [T26](../propostas/spec.md): Administrador/Gestor gerenciam propostas, Operador trata rascunhos e demandas. A mudança de permissões exige especificação e proteção no backend.

## Prioridades e critérios observáveis

Os critérios a seguir descrevem resultados da fase. Decisões identificadas abaixo devem ser resolvidas antes da implementação dependente.

### P1 — identificação e acompanhamento

- **AC-01:** a solicitação recebe identificação legível única dentro da organização, inclusive em criações concorrentes; dados anteriores recebem identificação preservando seus vínculos. O formato, a reinicialização e o tratamento de importações dependem de definição.
- **AC-02:** Administrador/Gestor podem registrar e consultar responsável comercial, prazo solicitado pelo cliente, prazo interno, referência do cliente e local conhecido. A API valida que os vínculos existentes pertencem à organização e, para local de cliente, ao cliente indicado.
- **AC-03:** o prazo do cliente e o prazo interno são apresentados separadamente; o atraso é identificado segundo uma regra de data e fuso definida para a organização.
- **AC-04:** lista e Kanban consultam a mesma coleção e permitem combinar filtros por etapa, responsável, cliente, período, origem e prazo vencido. Paginação e contagem refletem os filtros aplicados.
- **AC-05:** o servidor impede a passagem de Recebida para Em avaliação sem responsável comercial ou atribuição automática configurada, conforme decisão de atribuição e tratamento de solicitações anteriores.
- **AC-06:** registrar a próxima ação e sua data permite localizar pendências do usuário responsável. Lembretes dependem do mecanismo de notificações e de sua política de entrega.

### P1 — histórico e resultados

- **AC-07:** mudanças de etapa registram ator, data, etapa anterior e nova, além do motivo quando exigido. Eventos existentes permanecem consultáveis sem inventar dados de transições antigas.
- **AC-08:** usuário autorizado registra comentários e atividades manuais, com autor, tipo e data. Menções apontam apenas para usuários da organização; a entrega da notificação depende de AC-06.
- **AC-09:** motivos de não cotação, recusa, expiração e cancelamento são selecionáveis por organização, com observação livre. Regras de obrigatoriedade, gestão da lista e tratamento de registros antigos precisam ser definidas.
- **AC-10:** entradas de histórico e auditoria são inseridas sem permitir edição ou exclusão pela aplicação. A proposta de mecanismo transversal depende de desenho que estabeleça retenção e acesso.

### P2 — visitas e anexos

- **AC-11:** usuário autorizado agenda visita técnica pré-orçamento com data, responsável e local e registra o resultado. Visita comercial permanece distinta da execução da demanda.
- **AC-12:** visita apresenta os estados Agendada, Realizada, Cancelada e Remarcada, com histórico e regras de transição definidas. Sua obrigatoriedade por tipo de serviço permanece pendente.
- **AC-13:** anexos e evidências associados a solicitações e visitas usam armazenamento e acesso por organização, com limites e varredura definidos antes do uso. Baixar anexo de outra organização deve falhar.
- **AC-14:** eventual expiração automática respeita validade da revisão e regra específica para negociação ativa, registra ator de sistema e evita repetição. A definição da regra e a infraestrutura de processamento são pré-requisitos.

### Transversal

- **AC-15:** todas as consultas, escritas, IDs e vínculos são autorizados e isolados no backend; testes incluem organização estrangeira, sessão ausente e perfis sem acesso.
- **AC-16:** alterações concorrentes não sobrescrevem silenciosamente dados mais recentes; erros oferecem atualização explícita ao usuário.
- **AC-17:** a migração preserva solicitações, revisões, rascunhos e histórico anteriores. Build, testes, inspeção autenticada e mobile são registrados com evidência por critério.

## Decisões pendentes

1. Formato e ciclo da numeração da solicitação, inclusive dados legados e importação.
2. Atribuição manual ou automática, usuários elegíveis, ausência/desativação do responsável e impacto em solicitações existentes.
3. **Confirmada para AC-T03:** prazos civis em `America/Sao_Paulo`, vencidos a partir do dia seguinte; próxima ação em UTC, aviso in-app único ao responsável elegível. Entrega real e scheduler dependem de AC-T07/08.
4. Origem configurável por organização, valores iniciais e conversão das origens existentes; referências múltiplas e tipadas ou referência única na solicitação.
5. Obrigatoriedade e lista de motivos por resultado, inclusive expiração automática, desativação de motivos e registros históricos sem motivo estruturado.
6. Quem pode registrar atividades, mencionar usuários e agendar ou concluir visita; formato e transições de remarcação.
7. Local de prospect ainda sem cadastro e relação com local cadastrado após associação ao cliente.
8. **Regra de produto confirmada:** validade suspensa durante `Negotiating`; ao sair, reavaliar somente para destino não terminal e preservar resultado terminal. Implementação automática continua pendente do ator System e da persistência/auditoria atômica da AC-T04 e do scheduler AC-T07/08.
9. Mecanismo transversal de auditoria, retenção e visibilidade; política de armazenamento, tipos, limites, varredura e autorização dos anexos.
10. Matriz de permissões granulares e configuração por organização, com plano de migração dos perfis atuais.

## Limites e dependências

A implementação da Fase 2 sucede a validação da T26. Cálculo de custos, BDI, alçada e aprovação interna pertencem à Fase 3; o conector de e-mail e IA pertence à Fase 4; indicadores consolidados pertencem à Fase 5. Anexos, notificações e auditoria têm uso transversal e exigem desenho antes das tarefas que os utilizam. Segurança de acesso por organização deve ser demonstrada por testes negativos e não se limita à apresentação.

## Fontes de verificação

Relacionar cada critério executado a testes de domínio e API com PostgreSQL, testes de interface, migração em banco local autorizado e inspeção autenticada desktop/mobile. Registrar resultados e limites em `verification.md` quando houver execução. Não declarar o módulo validado por build isolado.
