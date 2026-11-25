# Verificação — detalhe de Solicitações acessível por teclado

## Resultado por critério

| Critério | Resultado | Evidência |
|---|---|---|
| A11Y-01 — Enter/Espaço na Lista; ativação nativa no Kanban | Verificado | `proposals-page.spec.ts`; navegador local: Enter e Espaço abriram linhas/cartões. |
| A11Y-02 — diálogo nomeado e foco inicial | Verificado | Teste de componente; navegador confirmou `role=dialog`, `aria-modal=true`, `aria-labelledby=proposal-detail-title` e foco no título. |
| A11Y-03 — Tab/Shift+Tab contidos | Verificado | Teste de componente e navegador: Shift+Tab do início e Tab do fim permaneceram dentro do detalhe. |
| A11Y-04 — Escape, Fechar, backdrop e retorno de foco/fallback | Verificado | Teste de componente; navegador confirmou Escape, botão Fechar por Enter e clique no backdrop, com retorno à linha/cartão. Teste também cobre fallback “Nova solicitação” quando o invocador sai do DOM. |
| A11Y-05 — Escape do editor filho | Verificado | Teste de componente e navegador: editor fechou, detalhe permaneceu aberto e foco voltou a “Editar solicitação”. |
| A11Y-06 — ações e API simulada | Verificado | Testes usam `HttpTestingController`; ações atuais do detalhe foram preservadas. |

## Comandos e execução

- Ambiente: `source /workspace/.tools/pitcrm-env.sh`; Node `v24.15.0`, npm `11.12.1`, Angular CLI local `22.2.6`.
- `npm ci --no-audit --fund=false` — passou; instalação local para esta worktree.
- `npm test -- --watch=false --include=src/app/proposals/proposals-page.spec.ts` — 6/6 testes passaram antes do último ajuste de asserção do editor.
- `npm test -- --watch=false` — passou: 10 arquivos, 38 testes.
- `npm run build` — passou: bundle inicial 416,33 kB; sem avisos.
- `git diff --check` — passou.

## Verificação manual no navegador

Executada em 24/11/2026 no Chromium embutido, rota `/propostas`, viewport 1265×700, com servidor API temporário que forneceu sessão Administrador e respostas sintéticas em memória. Não iniciou nem consultou API real, banco, Docker ou AC-T02. Foram exercitados: abrir Lista com Enter e Espaço; abrir Kanban com Enter e Espaço; foco inicial no título; ciclo Tab/Shift+Tab; Escape; Fechar por Enter; backdrop; restauração ao invocador; e Escape do editor com o painel de detalhe preservado. O navegador confirmou todos esses resultados e mostrou o indicador de foco da linha quando alcançada por teclado.

## Limites

O cenário manual usa dados simulados e confirma comportamento de interface, não integração autenticada com API real. Sem teste com leitor de tela ou dispositivos móveis nesta entrega. Não foi executado o gate completo dos scripts originais da skill TLC.
