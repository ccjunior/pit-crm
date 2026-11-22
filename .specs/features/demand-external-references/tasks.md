# Referências externas em demandas — tarefas

**Status:** Concluída localmente e verificada
**Autorização:** próximo trabalho autorizado em 20/11/2026; direção generalista para empresas de construção e reformas.
**Build backend:** `dotnet build backend/PitCrm.sln --configuration Debug`
**Testes backend:** `dotnet test backend/PitCrm.sln --configuration Debug`
**Build frontend:** `npm run build` (em `frontend`)
**Testes frontend:** `npm test -- --watch=false` (em `frontend`)

| ID | Entrega | Dependências | Critérios | Verificação |
| --- | --- | --- | --- | --- |
| T25.1 | Entidade, persistência e migração de legado para referências múltiplas | T20, T22 | REF-02, REF-03, REF-04, REF-07 | Concluída; teste PostgreSQL em schema temporário e build backend aprovados |
| T25.2 | Contratos de API para criar, editar, detalhar e pesquisar referências | T25.1 | REF-01, REF-02, REF-03, REF-04, REF-05, REF-08 | Concluída; suíte backend completa 8/8 aprovada |
| T25.3 | Formulário repetível e detalhe de referências externas | T25.2 | REF-01, REF-02, REF-06, REF-08 | Concluída; suíte Angular 30/30 aprovada e build concluído |
| T25.4 | Consolidação documental e verificação integrada | T25.1–T25.3 | REF-01–REF-08 | Concluída localmente com suítes automatizadas; evidências e limites em [verification.md](verification.md) |

## Handoff

**Concluídas:** T25.1–T25.4.
**Dados:** a migração foi exercitada em schema temporário. Em 21/11/2026, após autorização do usuário para aplicar migrações pendentes no banco local, foi aplicada ao `pitcrm_dev` junto à migração T26. Nenhum banco compartilhado ou publicado foi alterado.
