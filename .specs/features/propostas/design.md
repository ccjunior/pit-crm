# Desenho — solicitações comerciais, propostas e conversão em demanda

## Limites de módulo

### Comercial — fonte de verdade

O módulo Comercial (namespace/feature `Proposals`) é dono de solicitações, prospects registrados dentro da solicitação, etapas do funil, revisões de proposta, itens comerciais e fatos de envio/aprovação. A solicitação representa uma negociação; revisões não geram cartões separados.

### Operação — demandas e rascunhos

O módulo de Demandas é dono da demanda operacional, dos rascunhos incompletos originados por proposta e dos subitens executáveis. Uma demanda confirmada continua obedecendo às invariantes atuais: cliente, local, descrição e origem obrigatórios; estado inicial “Recebida”. Um rascunho não é uma demanda operacional e não aparece em suas contagens/etapas.

### Contrato entre módulos

- Comercial envia ao módulo de Demandas o contrato `ProposalDemandDraftSeed` com organização, solicitação, revisão aprovada, resumo do trabalho, canal de entrada, dados conhecidos de cliente/prospect e itens aprovados.
- O módulo de Demandas cria ou retorna um único rascunho por solicitação comercial. Enquanto não convertido, novas revisões aprovadas atualizam esse rascunho; depois de convertido, não se substitui a demanda.
- Comercial não grava diretamente nas tabelas de Demandas; Demandas não altera revisão ou resultado comercial.
- A aprovação e a criação/atualização do rascunho formam uma única transação de aplicação no monólito PostgreSQL. Se a gravação do rascunho falhar, a aprovação não é confirmada. Chave única por organização/solicitação comercial impede duplicatas em retries.
- Cada módulo mantém os próprios tipos de domínio e mapeamentos EF Core. A migração de schema continua centralizada no `PitCrmDbContext` existente.

## Modelo persistente

### Comercial

- `CommercialRequest`: organização, cliente existente opcional, dados de prospect quando ainda não cliente, canal de entrada, resumo obrigatório do serviço, etapa, versão de concorrência, autor/responsável e datas.
- `ProposalRevision`: organização/solicitação, número sequencial, validade opcional, observações, estado de revisão e fatos de envio e aprovação (data, canal, pessoa que registrou, nome de quem aprovou e observação).
- `ProposalItem`: revisão, posição, descrição e valor monetário informado. O total da revisão é a soma dos itens; não há imposto, desconto, custo ou cálculo de LL neste escopo.
- `CommercialActivity`: histórico de transições e eventos com ator e timestamp.
- Revisões enviadas/aprovadas são imutáveis. Revisões em elaboração podem ser editadas. Uma nova revisão não substitui a anterior.
- Solicitação tem uma negociação e revisões sucessivas. Não há propostas alternativas paralelas na primeira entrega.

### Operação

- `DemandDraft`: organização, solicitação comercial (única), revisão aprovada atual, dados operacionais preliminares, referência de prospect, cliente/local opcionais, versão de concorrência, estado `Pending`/`Converted`, demanda criada e auditoria.
- `DemandDraftItem`: cópia do texto e valor dos itens da revisão aprovada, em ordem estável. A cópia preserva o conteúdo comercial aprovado mesmo que a proposta tenha nova revisão.
- `DemandSubitem`: organização, demanda, posição, descrição e valor aprovado opcional. Itens copiados preservam `SourceProposalItemId` na mesma organização; itens adicionados manualmente não têm essa referência. A demanda pode ter zero ou mais subitens.
- `Demand.SourceProposalRevisionId` é opcional e único por organização; referências externas internas usam ID e não podem cruzar organizações. A atividade inicial da demanda informa que foi gerada de proposta.
- A ligação de um prospect a cliente/local existente é permitida após validação tenant-scoped. A criação/edição de Cliente/Local permanece protegida por `ManageCustomersAndLocations` (D01); se ainda faltar cadastro, a demanda fica em rascunho.

## Estados e transições

### Solicitação comercial

`Received` → `Evaluating` → `Drafting` → `Sent` → `Negotiating` → `Approved`.

Saídas terminais: `NotQuoted` (decisão interna com motivo obrigatório), `Rejected` (cliente recusou), `Expired` e `Cancelled`. Ações de terminal exigem usuário autorizado; resultados externos guardam data/canal. `Expired` pode ser marcada manualmente por Administrador/Gestor; não será criado job de background nesta etapa.

Uma alteração após envio cria a próxima revisão e mantém a anterior imutável. Quando uma nova revisão é aprovada antes da conversão operacional, o sistema atualiza o mesmo `DemandDraft` e seus snapshots, sem criar outro rascunho. Após a demanda ser confirmada, mudanças de escopo/valor seguem o fluxo de aditivo.

### Rascunho de demanda

- `Pending`: criado a partir da aprovação, fora das consultas operacionais.
- `Converted`: operador confirmou; guarda a demanda resultante para histórico e idempotência.
- Rascunho de prospect sem cliente permanece pendente. Somente Administrador/Gestor pode vincular/criar o cliente e o local quando esses registros faltarem. Com o cliente vinculado, Operador pode selecionar um local existente e completar os demais dados, mas não pode alterar o vínculo do cliente nem cadastrar/editar Cliente/Local.
- A conversão valida campos obrigatórios, organização dos vínculos e versão do rascunho; cria `Demand`, subitens e atividade, e marca o rascunho como convertido na mesma transação.
- Repetir confirmação retorna a demanda já criada. Dois pedidos concorrentes não criam duplicatas.

## Contratos HTTP

### Comercial (Administradores e Gestores)

- `GET /api/proposals`: pesquisa, filtros de etapa, cliente/prospect e período; paginação/ordenação para lista e Kanban.
- `POST /api/proposals`: cria solicitação comercial.
- `GET /api/proposals/{id}`: detalhe, revisões, itens e histórico.
- `PUT /api/proposals/{id}`: atualiza dados editáveis usando versão concorrente.
- `POST /api/proposals/{id}/revisions`: cria próxima revisão com seus itens.
- `POST /api/proposals/{id}/send`: registra envio externo, data/canal e revisão.
- `POST /api/proposals/{id}/approve`: registra aprovação manual e cria/atualiza rascunho; retorno inclui `demandDraftId`.
- `POST /api/proposals/{id}/status`: registra transições e resultados permitidos, incluindo não cotada/recusada/expirada/cancelada com as validações próprias.

Todas as escritas usam antifalsificação e política `ManageProposals`; contexto da organização vem da sessão.

### Rascunhos e subitens de demanda

- `GET /api/demand-drafts`: fila separada da operação, acessível a perfis com `OperateDemands`.
- `GET /api/demand-drafts/{id}`: dados de origem e campos a completar.
- `PUT /api/demand-drafts/{id}`: atualiza campos operacionais e versão; somente Administrador/Gestor podem alterar o vínculo de cliente em rascunho de prospect. Com cliente já vinculado, Operador pode apontar para local existente desse cliente, sem permissão para cadastrar/editar Cliente/Local.
- `POST /api/demand-drafts/{id}/convert`: confirma e cria a demanda/subitens uma única vez.
- `POST /api/demands` e edição recebem coleção opcional de subitens para demandas manuais; GET detalhe devolve a coleção.

Falhas previstas: 404 fora do tenant, 403 sem perfil, 409 por versão/estado terminal, 422 por campo ou transição inválida. DTOs não expõem entidades EF.

## Autorização e isolamento

- Nova política backend `ManageProposals`: Administrador/Gestor.
- `OperateDemands`: Administrador/Gestor/Operador acessam fila e completam rascunho; somente Administrador/Gestor podem cadastrar/alterar Cliente/Local, preservando `ManageCustomersAndLocations`.
- Consulta, escrita, ids, prospects, revisão, drafts e conversão sempre incluem `OrganizationId` derivado da sessão; IDs fornecidos pelo cliente não estabelecem tenant.
- FK composta protege os vínculos de Demand/Subitem e Cliente/Local. Revisão comercial de origem usa chave idempotente com `OrganizationId`.

## Interface Angular

- Rota `/propostas`, visível e protegida para Administrador/Gestor.
- Mesmos registros e filtros em modos Kanban e lista. Cartão por solicitação; revisão atual e etapa visíveis; detalhe contém revisões, itens, ações de envio/resultado e histórico.
- Formulário cria solicitação existente/prospect e revisão com itens de descrição/valor; total soma itens. Valores exibidos em BRL.
- Aprovação pede data, canal e nome do aprovador; o ator vem da sessão. Nenhum e-mail/PDF é gerado.
- Fila de rascunhos em Demandas. Operador completa resumo/origem/prioridade/atribuição, seleciona Cliente/Local existentes e confirma quando obrigatório; Administrador/Gestor podem ir ao cadastro existente criar/vincular Cliente/Local para prospects.
- Detalhe da demanda mostra subitens. Formulário manual permite adicionar/remover subitens opcionais.
- Ações respeitam política também na API. Estados de carregamento, vazio, validação, erro de rede, 403, conflito e salvamento são explícitos.

## Decisões técnicas relevantes

- Novo subdomínio/feature no monólito modular (`Proposals`) em vez de colocar o pipeline comercial em `Demands`; o módulo de Demandas mantém o estado operacional e expõe a conversão por contrato de aplicação.
- Persistência PostgreSQL e transação síncrona com idempotência. Não introduzir fila/outbox para um fluxo de banco único e conversão imediatamente visível.
- Valores persistidos em `numeric(18,2)` e datas em UTC; etapa tem transições explícitas, não é texto livre.
- Revisões e subitens guardam snapshots de descrição e valor; não apontam para valores mutáveis como fonte única do escopo contratado.
- Contrato, PDF, anexos, portal, e-mail, preço com imposto/desconto e cálculo de LL ficam fora desta entrega.
