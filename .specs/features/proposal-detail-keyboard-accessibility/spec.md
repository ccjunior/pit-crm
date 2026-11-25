# Acessibilidade por teclado — detalhe de Solicitações

**Status:** implementada e verificada localmente; publicação e PR pendentes.
**Fonte/autorização:** card Trello “Acessibilidade por teclado — detalhe de Solicitações (Lista/Kanban)”, escopo confirmado pelo Product Manager e Tech Lead; implementação autorizada por Atlas em 24/11/2026.

## Resultado

Permitir abrir e usar o detalhe de uma solicitação comercial por teclado nas visualizações Lista e Kanban, sem alterar APIs, payloads, regras, permissões ou persistência.

## Critérios de aceite

- **A11Y-01:** Na Lista, Enter e Espaço abrem o detalhe da linha focada; no Kanban, o cartão abre por Enter e Espaço.
- **A11Y-02:** O painel é anunciado como diálogo modal, com nome acessível derivado do título, e recebe foco inicial.
- **A11Y-03:** Tab e Shift+Tab mantêm o foco no diálogo aberto.
- **A11Y-04:** Escape, Fechar e clique no backdrop fecham o detalhe e restauram o foco ao invocador; se ele saiu do DOM, o foco vai a “Nova solicitação”.
- **A11Y-05:** No editor aberto a partir do detalhe, Escape fecha somente o editor e devolve o foco a “Editar solicitação”; o detalhe continua aberto.
- **A11Y-06:** Ações existentes permanecem disponíveis; testes de componente usam API simulada e a verificação manual de teclado no navegador fica registrada.

## Limites

Não mudar API, contratos, regras comerciais, permissões, persistência, backend ou infraestrutura. A AC-T02 segue independente e não deve ser interrompida.
