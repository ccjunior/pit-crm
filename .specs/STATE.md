# Estado do projeto

Atualizado em 05/11/2026.

## Situação

Documentação de descoberta, escopo, modelo operacional e arquitetura disponível. O usuário aprovou a primeira entrega operacional em 05/11/2026. T01–T17 estão concluídas localmente; T18 é a próxima. Schema, provisionamento idempotente, sessão com antifalsificação, políticas iniciais, interface de entrada, clientes/locais, criação de demanda e gestão de equipe/senha inicial foram verificados. O PostgreSQL 18.4 do Compose está ativo em `127.0.0.1:5433`; o banco `pitcrm_dev` foi inicializado agora e ainda aguarda as migrações da aplicação.

## Decisions

| ID | Decision | Rationale | Status | Date |
| --- | --- | --- | --- | --- |
| AD-001 | [Base técnica aprovada](../docs/decisions/0001-base-tecnica-aprovada.md) | Consolidar a arquitetura preexistente em um registro consultável | active | 2026-11-04 |

A data é a do registro do ADR. Escopo e regras de produto continuam nos documentos 01 e 02; as pendências estão no documento 03.

## Preparação anterior

- Solicitação: criar a estrutura proposta, baixar as cinco skills selecionadas e preparar as três skills do PIT-CRM.
- Fontes: documentação atual e repositório Tech Leads Club / agent-skills, na revisão registrada em [skills.lock.json](../skills.lock.json).
- Entrada para agentes: [AGENTS.md](../AGENTS.md).
- Fluxo de trabalho: [docs/05-TRABALHO-COM-AGENTES.md](../docs/05-TRABALHO-COM-AGENTES.md).
- Catálogo e adaptações: [docs/06-SKILLS-E-ORIGEM.md](../docs/06-SKILLS-E-ORIGEM.md).
- Origem e validação dessa preparação: documento de catálogo e origem.

## Verificação da preparação anterior

As oito skills passaram na validação de formato. Foram conferidos os hashes dos 36 arquivos instalados, a correspondência dos 31 arquivos de origem com a revisão Git, os links locais e a sintaxe dos oito scripts importados. O detalhamento está no documento de catálogo e origem. Os scripts de execução das skills e testes de aplicação não foram executados; a aplicação ainda não existe.

## Trabalho atual

- Solicitação inicial: “vamos”, para especificar a primeira entrega; aprovação posterior: “aprovo”, autorizando o escopo em 05/11/2026.
- Funcionalidade: [primeira-entrega-operacional](features/primeira-entrega-operacional/spec.md).
- Resultado: 26 critérios de aceite, desenho técnico proposto e 23 tarefas de implementação com dependências e verificação planejada.
- Decisões confirmadas D01–D08: [context.md](features/primeira-entrega-operacional/context.md).
- D01: Administrador/Gestor cadastram e editam clientes/locais; Operador apenas consulta esses cadastros.
- D02: cliente, local, descrição e origem obrigatórios na abertura; responsável operacional interno opcional.
- D03: organização e primeiro administrador provisionados de forma controlada; administrador cadastra a equipe.
- Fontes de produto e roadmap atualizados com essas respostas; o restante da Etapa 0 permanece aberto.
- Verificação desta especificação: [verification.md](features/primeira-entrega-operacional/verification.md).
- Conferência documental concluída: 26 critérios rastreados, 23 tarefas, 40 dependências coerentes com o diagrama e 81 links locais válidos em 18 documentos. Os hashes dos 36 arquivos de skills foram preservados.
- Implementação: T01–T17 concluídas localmente em 05/11/2026; T18 é a próxima. D04–D08 registrados em [context.md](features/primeira-entrega-operacional/context.md). PEO-01 verificado por teste e smoke test CLI; persistência, sessão, matriz de papéis, Angular, consulta de responsáveis, clientes/locais, criação de demandas e APIs de equipe/senha verificados; ver [verification.md](features/primeira-entrega-operacional/verification.md). Limites técnicos restantes continuam identificados como proposta.

## Pendências de produto

Permanecem as questões da Etapa 0 em [03-ROADMAP-E-PENDENCIAS.md](../docs/03-ROADMAP-E-PENDENCIAS.md): restante da matriz de permissões (especialmente aprovações e financeiro), perfil Financeiro, categorias de custo, referências específicas dos clientes sem chamado e recorte de importação histórica. As permissões iniciais de clientes/locais e o acesso inicial estão resolvidos em D01–D03.

## Handoff

Execução: primeira entrega aprovada; T01–T17 concluídas localmente; T18 pendente. Repositório inicializado em `ccjunior/pit-crm`; commits iniciais e checkpoint de código T14–T17 estão em `origin/main`. O Compose foi ajustado ao caminho de volume do PostgreSQL 18; serviço ativo e conexão local em 5433 confirmada. O schema da aplicação e o provisionamento do primeiro Administrador ainda não foram executados neste banco recém-inicializado. Não declarar a Etapa 0 inteira concluída.
