# Tarefas — detalhe de Solicitações acessível por teclado

- [x] **A11Y-T01 — Isolar a implementação.** Criar branch e worktree próprios a partir de `origin/main`; conferir a base e preservar a AC-T02. Evidência: branch `feat/proposal-detail-keyboard-a11y`, baseada em `da13f18`.
- [x] **A11Y-T02 — Habilitar ativação e diálogo acessível.** Suportar Enter/Espaço na Lista e cartão nativo no Kanban; identificar e nomear o diálogo, posicionar foco inicial, prender Tab/Shift+Tab, fechar por Escape/Fechar/backdrop e restaurar foco com fallback.
- [x] **A11Y-T03 — Preservar modal editor filho.** Confirmar Escape fecha apenas o editor e devolve foco ao controle Editar.
- [x] **A11Y-T04 — Verificar e registrar evidências.** Adicionar testes de componente com HTTP simulado, executar testes/build relevantes e verificar teclado manualmente no navegador. Resultado e limites: [verification.md](verification.md).
- [x] **A11Y-T05 — Entregar para revisão.** Diff revisado; commit `4246247` criado e branch publicada. PR [#2](https://github.com/ccjunior/pit-crm/pull/2) aberta para `main`, aguardando revisão do Tech Lead. Sem merge ou deploy.
