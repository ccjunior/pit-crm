# Módulo de solicitações comerciais e propostas — tarefas

**Status:** Especificação aprovada; implementação em andamento.
**Autorização:** usuário aprovou a especificação em 21/11/2026 e confirmou D01 para criação de Cliente/Local.

| ID | Resultado | Dependências | Área | Critérios | Verificação |
| --- | --- | --- | --- | --- | --- |
| T26.1 | Entidades, tabelas, migração e invariantes de Solicitação Comercial, revisões e itens | Spec aprovada | Backend/Comercial | PROP-01–PROP-08, PROP-16–PROP-17 | **Em andamento:** migrações T25/T26 aplicadas em `pitcrm_dev`; histórico e tabelas comerciais confirmados; testes de fluxo e invariantes pendentes |
| T26.2 | Pipeline API com transições, busca, histórico, envio e aprovação manual | T26.1 | Backend/Comercial | PROP-01–PROP-08, PROP-16–PROP-17 | **Em andamento:** endpoints e build .NET concluído; testes HTTP pendentes |
| T26.3 | Rascunho de demanda, conversão idempotente e subitens em demanda | T26.1–T26.2 | Backend/Operação | PROP-09–PROP-18 | **Em andamento:** fluxo implementado e compilando; verificação PostgreSQL/concorrência pendente |
| T26.4 | Tela Kanban/lista, criação/edição/detalhe e histórico de solicitações/propostas | T26.2 | Frontend/Comercial | PROP-01–PROP-08, PROP-16, padrões globais de listagem/cadastro/edição | **Em andamento:** grade de formulário corrigida para duas/uma coluna conforme viewport; tipografia de rótulo/controle separada; painel, botões, filtros e paginação alinhados; build Angular passou; inspeção visual autenticada pendente |
| T26.5 | Fila de rascunhos, vínculo permitido de Cliente/Local, confirmação e subitens manuais | T26.3–T26.4 | Frontend/Operação | PROP-09–PROP-18 | **Em andamento:** fila e formulário criados; validação de interação pendente |
| T26.6 | Verificação integrada e atualização dos documentos de produto/estado | T26.1–T26.5 | Backend/Frontend/Docs | PROP-01–PROP-18 | Suítes completas, builds, diff check e verificação local autorizada |

## Handoff

Em 22/11/2026, a integração PostgreSQL passou a exercitar a conversão, a aprovação repetida e simultânea, o isolamento e o acesso por perfil nos cenários descritos em [verification.md](verification.md). A validação autenticada no navegador e os critérios restantes seguem pendentes.

O design mantém Comercial como dono da solicitação/proposta e Demandas como dono de rascunhos, demandas e subitens. D01 é preservada: Operador não cria/edita Cliente/Local. Em 21/11/2026, o usuário autorizou aplicar as migrações pendentes no banco local `pitcrm_dev`; T25 e T26 foram aplicadas e as tabelas principais foram verificadas. Nenhum outro banco foi alterado. O contrato idempotente de aprovação está definido em `design.md`.
