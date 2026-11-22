# Módulo de solicitações comerciais e propostas — tarefas

**Status:** Implementação local concluída; fechamento integrado pendente das inspeções manuais adiadas.
**Autorização:** usuário aprovou a especificação em 21/11/2026 e confirmou D01 para criação de Cliente/Local.

| ID | Resultado | Dependências | Área | Critérios | Verificação |
| --- | --- | --- | --- | --- | --- |
| T26.1 | Entidades, tabelas, migração e invariantes de Solicitação Comercial, revisões e itens | Spec aprovada | Backend/Comercial | PROP-01–PROP-08, PROP-16–PROP-17 | Implementada; migrações aplicadas em `pitcrm_dev`; testes PostgreSQL passaram como parte da suíte backend 8/8 |
| T26.2 | Pipeline API com transições, busca, histórico, envio e aprovação manual | T26.1 | Backend/Comercial | PROP-01–PROP-08, PROP-16–PROP-17 | Implementada; cenários HTTP, concorrência, permissões e isolamento incluídos na suíte backend 8/8 |
| T26.3 | Rascunho de demanda, conversão idempotente e subitens em demanda | T26.1–T26.2 | Backend/Operação | PROP-09–PROP-18 | Implementada; conversão, reaprovação, subitens, vínculos e isolamento cobertos pela suíte backend 8/8 |
| T26.4 | Tela Kanban/lista, criação/edição/detalhe e histórico de solicitações/propostas | T26.2 | Frontend/Comercial | PROP-01–PROP-08, PROP-16, padrões globais de listagem/cadastro/edição | Implementada; testes Angular 34/34 e build de produção passaram (bundle inicial 416,31 kB); Admin/Gestor inspecionados; inspeção touch foi adiada a pedido do usuário |
| T26.5 | Fila de rascunhos, vínculo permitido de Cliente/Local, confirmação e subitens manuais | T26.3–T26.4 | Frontend/Operação | PROP-09–PROP-18 | Implementada; fluxo e permissões cobertos por testes HTTP e testes Angular; confirmação visual como Operador foi adiada a pedido do usuário (fila sem rascunho pendente na sessão) |
| T26.6 | Verificação integrada e atualização dos documentos de produto/estado | T26.1–T26.5 | Backend/Frontend/Docs | PROP-01–PROP-18 | Suítes completas passaram (backend 8/8; Angular 34/34), build .NET passou durante `dotnet test`, build Angular passou e migração local confirmada. Inspeção touch e confirmação visual de rascunho como Operador permanecem para o fechamento integrado do módulo Comercial, conforme orientação do usuário em 22/11. |

## Handoff

Em 22/11/2026, a integração PostgreSQL passou a exercitar a conversão, a aprovação repetida e simultânea, nova revisão após aprovação, isolamento e acesso por perfil nos cenários descritos em [verification.md](verification.md). O usuário autorizou adiar a inspeção touch e a confirmação visual do rascunho como Operador para o fechamento integrado do Comercial; isso não bloqueia a etapa seguinte. A inspeção autenticada Admin/Gestor e a suíte Angular 34/34 foram registradas.

O design mantém Comercial como dono da solicitação/proposta e Demandas como dono de rascunhos, demandas e subitens. D01 é preservada: Operador não cria/edita Cliente/Local. Em 21/11/2026, o usuário autorizou aplicar as migrações pendentes no banco local `pitcrm_dev`; T25 e T26 foram aplicadas e as tabelas principais foram verificadas. Nenhum outro banco foi alterado. O contrato idempotente de aprovação está definido em `design.md`.
