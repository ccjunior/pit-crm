# Acompanhamento comercial — tarefas

**Estado:** AC-T01 concluída com decisões registradas; AC-T02 concluída conforme validação integrada reportada por Carlos. A T26 foi validada localmente nos níveis registrados, com duas inspeções manuais adiadas pelo usuário para o fechamento integrado do Comercial.

| ID | Resultado | Dependências | Área | Critérios | Verificação |
| --- | --- | --- | --- | --- | --- |
| AC-T01 | Detalhar contratos, campos e decisões de identificação, vínculo e atribuição; desenhar migração dos dados existentes | T26 validada; decisões 1, 2, 4 e 7 | Produto e backend | AC-01, AC-02, AC-05, AC-15–AC-17 | **Concluída:** decisões confirmadas em `design.md`; contrato e desenho da migração definidos. |
| AC-T02 | Identificação legível, responsável, local e referências em API e interface | AC-T01 | Backend e frontend | AC-01, AC-02, AC-05, AC-15–AC-17 | **Concluída.** Validação integrada em PostgreSQL descartável reportada por Carlos no [commit `5280438`](https://github.com/ccjunior/pit-crm/commit/5280438c6f168edfdd263ca93b9ca3702f1e325e). A [PR #3](https://github.com/ccjunior/pit-crm/pull/3) foi incorporada à `main`. Local continua no rascunho de demanda conforme decisão confirmada. |
| AC-T03 | Prazos, próxima ação e filtros consistentes entre lista e Kanban | AC-T01; decisões 3 e 8 | Backend e frontend | AC-03, AC-04, AC-06, AC-14–AC-17 | Testes com datas de borda, filtros combinados, paginação e organizações; inspeção mobile |
| AC-T04 | Desenhar histórico transversal, motivos e permissões; definir tratamento de eventos existentes | T26 validada; decisões 5, 9 e 10 | Produto e arquitetura | AC-07–AC-10, AC-15–AC-17 | Revisão do modelo, migração e matriz de acesso |
| AC-T05 | Histórico de transições, atividades e motivos com acesso controlado | AC-T04; decisão 6 | Backend e frontend | AC-07–AC-10, AC-15–AC-17 | Testes de transição, auditoria, registros antigos, isolamento e inspeção autenticada |
| AC-T06 | Visitas pré-orçamento e resultados, com transições e responsáveis definidos | AC-T01, AC-T04; decisões 6 e 7 | Backend e frontend | AC-11, AC-12, AC-15–AC-17 | Testes de estados, perfis e vínculos; inspeção autenticada |
| AC-T07 | Desenhar armazenamento, varredura, política de acesso e notificações transversais | Decisões 3 e 9 | Arquitetura e infraestrutura | AC-06, AC-13, AC-15 | Revisão dos contratos, limites e falhas, sem alterar ambiente externo |
| AC-T08 | Anexos e lembretes conforme desenho aprovado | AC-T07 | Backend e frontend | AC-06, AC-13, AC-15–AC-17 | Testes de limites, acesso cruzado, falhas e interface |
| AC-T09 | Verificação integrada e atualização da documentação | AC-T02–AC-T08 | Integração | AC-01–AC-17 | Suítes relevantes, builds, migração local autorizada, inspeção autenticada e registro de evidências |

As tarefas são sequenciáveis conforme dependências e decisões; a numeração não é autorização para antecipar etapas ou presumir respostas de produto.
