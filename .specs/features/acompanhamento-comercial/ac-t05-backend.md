# AC-T05 — contrato e implementação backend

Estado: implementação em PR draft; não aceita até validação PostgreSQL descartável e integração da interface.

## API

- `GET /api/proposals/{id}/timeline?page=1&pageSize=20`: paginação estável por sequência crescente. Retorna `items`, `page`, `pageSize` e `totalCount`. Cada item contém `id`, `sequence`, `type`, `occurredAtUtc`, `occurredAtKnown`, `actorName`, `fromStatus`, `toStatus`, `reason`, `activityType`, `text` e `mentionedUserIds`.
- `POST /api/proposals/{id}/status`: exige `ManageProposals`, CSRF e `Idempotency-Key` UUIDv7. O payload usa `version`, `status` e `reasonId`; motivo livre é rejeitado. Resultado `NotQuoted` requer motivo ativo da mesma organização e do mesmo resultado. Responde com o snapshot da proposta produzido pelo comando; replay devolve esse mesmo snapshot. Versão desatualizada ou conflito real retorna 409 sem persistir a transição.
- `POST /api/proposals/{id}/activities` e `/comments`: exigem `ManageProposals`, CSRF e UUIDv7. O corpo tem `type`, `text` e `mentionedUserIds`; comentário não aceita `type`. Menções exigem a policy `MentionProposalUsers` e usuários ativos da organização.
- `GET /api/proposals/reasons?resultStatus=...`: lista motivos ativos da organização. `POST /api/proposals/reasons` e `POST /api/proposals/reasons/{reasonId}/deactivate` são restritos a Admin e mantêm trilha de auditoria; não há exclusão física.
- `GET /api/proposals/{id}/audit?page=1&pageSize=20`: Admin/Gestor e grant `ViewProposalAudit` vinculado ao usuário e à organização autenticados.
- `GET /api/proposals/{id}/timeline/archive?page=1&pageSize=20`: Admin/Gestor e grant `RestoreProposalTimeline` vinculado ao usuário e à organização autenticados; registra a leitura antes de devolver dados do arquivo.
- `GET /api/demands/{id}/proposal-timeline?page=1&pageSize=20`: policy `OperateDemands`; escopo deriva da organização autenticada e de vínculo válido demanda→revisão→proposta. A projeção allowlist inclui apenas tipo, data conhecida/UTC e status anterior/novo; não expõe comentário, atividade, motivo, menções nem ator.

Todos os `page` são positivos; `pageSize` aceita 1–100 e offsets que excedem `Int32` são rejeitados. Leituras e vínculos sempre filtram pela organização autenticada.

## Eventos, transações e idempotência

Os eventos operacionais são `StageChanged`, `ActivityRecorded` e `CommentAdded`; os dados pré-existentes migram como `LegacyActivity`, preservando IDs de origem e marcando a data como conhecida. O actor é `Human` com usuário ou `System` com código tipado. A aplicação grava evento, sequência, auditoria, alteração de estado e chave de idempotência na mesma transação. Escritas concorrentes por proposta serializam pelo lock da linha e continuam protegidas por `Version`.

`Idempotency-Key` deve ser UUIDv7, não pode exceder 5 minutos no futuro e expira no instante `timestamp + 30 dias`. A chave é única por organização. Mesmo alvo/operação e fingerprint devolvem o resultado gravado; alvo ou payload diferente retorna 409. O worker remove registros vencidos; uma chave vencida continua inválida mesmo após a remoção.

## Migração e retenção

A migration copia `CommercialActivities` para `ProposalTimelineEvents`, preserva ID/data/ator, ordena sequência por `OccurredAtUtc, Id`, registra linhas com vínculo inválido em quarentena sem as expor por API e grava contagens de origem/migradas/quarentena por organização. Ela aborta a transação se as contagens por organização ou globais não reconciliarem. O histórico passa a ser lido da tabela nova; a cópia compatível em `CommercialActivities` é removida atomicamente quando a timeline entra em arquivo. Propostas em estado terminal recebem ciclo com data de encerramento administrativa igual ao corte UTC da migration e `ClosedAtKnown=false`.

O worker arquiva timeline após 24 meses civis no fuso `America/Sao_Paulo`; confirma que a geração selecionada ainda é a mais recente e que a proposta continua terminal; descarta arquivo 12 meses após arquivamento efetivo; reabertura elegível restaura IDs e sequência. Encerramentos terminais sucessivos restauram a geração anterior antes de iniciar outra. Cada evento de auditoria perde detalhes e identificadores no próprio corte individual de 12 meses civis, com lock e revalidação, em lotes de até 500 candidatos. Agregados por categoria, com limiar mínimo de 10 solicitações distintas e supressão complementar, continuam sendo critério de aceite obrigatório e não estão atendidos nesta PR. A implementação será proposta em PR associada separada. A arquitetura em revisão usa token HMAC domain-separated por organização/mês/categoria/solicitação, contribuição idempotente, janela selada uma única vez após cutoff e reconciliação, e remoção dos tokens no selo. Carlos aprovou reter esse token pseudônimo até o selo. Chegadas após o selo são colocadas em quarentena, sem republicação; janelas incompletas/falhas não publicam. O acesso ao auxiliar é restrito à rotina e não há endpoint para expor linhas redigidas; `GetAuditAsync` exclui eventos com `RedactedAtUtc`. A publicação e o tratamento de completude/chegadas tardias ainda aguardam revisão técnica final; AC-T05 permanece aberta até o agregado ser entregue. Nenhum scheduler de expiração de proposta ou entrega de notificação está incluído.

Auditoria e leitura do arquivo exigem grants explícitos por usuário/organização além do papel Admin/Gestor; a autorização nega acesso por padrão. A tabela não é populada automaticamente e esta fatia não fornece um fluxo para conceder/revogar grants. A concessão inicial e sua UI/API precisam de decisão/fluxo aprovado antes de habilitar esses endpoints.

## Verificação pendente

Build e testes unitários locais não exercitam SQL PostgreSQL, locks, migration, reconciliação nem políticas de autorização em HTTP. Antes do aceite, executar migration e testes de integração apenas em schema descartável aprovado, incluindo race/idempotência, isolamento entre organizações, acesso do Operador, archive/restore/discard e reconciliação de legado. A concessão inicial e a gestão de grants explícitos ainda não têm fluxo aprovado; os endpoints de auditoria/arquivo permanecem negados até haver grants. Agregação segura por contagem distinta também fica pendente de desenho. A interface ainda precisa consumir DTOs, lista de motivos e projeção do Operador.
