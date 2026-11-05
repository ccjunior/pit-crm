# Primeira entrega operacional — verificação

**Objeto:** especificação, desenho, plano de tarefas e implementação da primeira entrega operacional.
**Estado:** T01–T13 verificadas em 05/11/2026. Formulário/consulta de demandas, equipe e critérios restantes seguem pendentes.

## Escopo da conferência

- Fidelidade às respostas D01–D03 e às fontes de produto/arquitetura.
- Rastreabilidade dos critérios PEO-01 a PEO-26 nas tarefas propostas.
- Coerência entre dependências declaradas e o diagrama de execução.
- Links locais dos documentos e ausência de uma aprovação integral inventada.
- Separação entre decisões confirmadas, novos detalhamentos propostos e pendências futuras.
- Estrutura executável dos runtimes, teste mínimo da API, tela de inicialização, ferramenta EF e configuração local do PostgreSQL.

## Evidências

Conferência executada em 04/11/2026 com Python, lendo os documentos locais, comparando os IDs e as dependências e calculando os hashes das skills. O script foi executado pela entrada padrão, sem adicionar um script à aplicação.

| Verificação | Resultado observado |
| --- | --- |
| IDs únicos e cobertura | 26 critérios PEO-01 a PEO-26; todos aparecem nas tarefas e na matriz de rastreabilidade |
| Identificação das tarefas | 23 tarefas T01 a T23, sem IDs duplicados |
| Dependências e diagrama | 40 arestas; correspondência exata com as dependências da tabela; nenhuma dependência aponta para tarefa posterior |
| Matriz de rastreabilidade | Os conjuntos de tarefas por critério correspondem aos critérios declarados em cada tarefa |
| Respostas e autorização | D01–D03 presentes com as respostas do usuário; nenhum campo de aprovação integral foi inserido nos documentos da funcionalidade |
| Referências locais | 81 links locais conferidos em 18 documentos do projeto, incluindo os cinco desta funcionalidade |
| Integridade das skills | Os hashes dos 36 arquivos continuam correspondendo a `skills.lock.json` |

A revisão de conteúdo também confirmou a distinção entre responsável operacional interno e equipe/prestador de execução, a permanência das pendências financeiras e a identificação dos novos detalhes como proposta.

## Evidências de T01

| Verificação | Comando / procedimento | Resultado |
| --- | --- | --- |
| Build e integração da API .NET | `dotnet test backend/PitCrm.sln --configuration Debug` | Aprovado; cinco projetos compilados e 1 teste HTTP aprovado |
| Testes do Angular | `npm test -- --watch=false` | Aprovado; 1 arquivo e 2 testes aprovados |
| Build Angular | `npm run build` | Aprovado; bundle de produção criado em `frontend/dist/pitcrm-web` |
| API em execução | `dotnet run --project backend/src/PitCrm.Api`; `Invoke-RestMethod http://localhost:5006/api/health` | Aprovado; HTTP 200 e `{"status":"ok"}` |
| Angular e proxy | `npm start -- --host 127.0.0.1`; requisição para `http://127.0.0.1:4200` e `/api/health` | Aprovado; página HTTP 200, título PIT-CRM e proxy retornou `ok` |
| PostgreSQL local | PostgreSQL 18.4 user-space; `psql -h 127.0.0.1 -p 5432 -U pitcrm -d pitcrm_dev` | Aprovado; conexão e consulta de versão/database/usuário responderam |
| Compose | `docker compose config` | Aprovado; serviço PostgreSQL 18.4 e porta local 5433 configurados. Container não iniciado porque o daemon Docker não estava ativo |
| Ferramenta de migração | `dotnet tool restore`; `dotnet ef --version` | Aprovado; EF CLI 10.0.14 restaurada e executada |

O diretório não possui repositório Git. Não foram criados commits, push ou publicação. O banco local de verificação está em `.local/postgres-data`, ignorado pelo Git; o Compose mantém uma alternativa com volume nomeado.

## Evidências de T02

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Modelo de persistência | `dotnet test PitCrm.sln --configuration Debug` | 2 testes aprovados; o modelo exige vínculo da conta com organização e restringe exclusão da organização referenciada |
| Criação da migração | `dotnet ef migrations add OrganizationIdentityFoundation --project src/PitCrm.Infrastructure --startup-project src/PitCrm.Api --output-dir Persistence/Migrations` com `PITCRM_CONNECTION_STRING` apontando para `pitcrm_test` | Aprovado; arquivos de migração e snapshot gerados |
| Aplicação de migração | `dotnet ef database update --project src/PitCrm.Infrastructure --startup-project src/PitCrm.Api` na mesma connection string | Aprovado; `Organizations`, tabelas Identity e `__EFMigrationsHistory` foram criadas |
| Vínculo relacional | Consulta ao catálogo do PostgreSQL e tentativa de inserir usuário com `OrganizationId` inexistente | Aprovado; coluna NOT NULL, FK `RESTRICT` e inserção inválida rejeitada |

T02 verifica apenas a integridade estrutural da organização e da conta. Provisionamento, autenticação, permissões e isolamento de consultas continuam pendentes nas tarefas seguintes.

## Evidências de T03

- `dotnet run --project src/PitCrm.Api -- provision` lê nome, ID estável, e-mail e senha do Administrador de variáveis de ambiente; senha não fica em arquivos versionados ou logs.
- Teste de integração em schema PostgreSQL isolado criou organização, conta e papel; repetição preservou os três registros, hash/credencial original e nome da organização. O schema foi removido ao final.
- Smoke test da CLI mostrou `Provisioning completed` na primeira execução e `Organization was already provisioned` na repetição, com mesmo ID; não houve credenciais na saída.
- `dotnet test backend/PitCrm.sln --configuration Debug` com `PITCRM_TEST_CONNECTION`: 3 aprovados após T03.

## Evidências de T04

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Login e mensagem genérica | Teste HTTP com e-mail/senha inválidos e válidos no schema PostgreSQL temporário | Inválidos retornam 401 e a mensagem genérica; válido emite cookie de sessão HttpOnly e SameSite=Lax |
| Sessão atual | Login seguido por `GET /api/auth/me` | 200 com usuário, organização e papéis; sem sessão, retorna 401 |
| CSRF e logout | Token por `GET /api/auth/csrf`; tentativa de logout sem token e depois com `X-CSRF-TOKEN` | Sem token, 400; com token, 204; sessão subsequente retorna 401 |
| Revalidação de identidade | Desativação de conta e depois organização após emitir cookie | A requisição seguinte retorna 401; login em organização inativa também retorna 401 genérico |
| Resposta de API | Callback de redirecionamento do cookie | Login resulta em 401 e acesso negado em 403; não há redirect para página HTML |
| Suíte backend | `dotnet test backend/PitCrm.sln --configuration Debug` com `PITCRM_TEST_CONNECTION` | 4 aprovados, 0 falhas; migrações aplicadas em schema isolado e schema removido |

Detalhes concretos de T04: cinco tentativas incorretas causam bloqueio de cinco minutos pelo Identity; cookie não persistente e SecurePolicy Always fora de Development. A migração `AddOrganizationActiveState` adiciona `Organizations.IsActive` NOT NULL com default `true`.

## Evidências de T05

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Matriz dos três perfis | Usuários reais Identity com papéis Administrador, Gestor e Operador; autorização avaliada para cada política | Leitura de clientes/locais, operação de demandas e troca da própria senha para os três; escrita de cliente/local para Administrador/Gestor; gestão de equipe somente Administrador |
| Contexto de organização | Criação de principal com a fábrica Identity do projeto e leitura via `ICurrentOrganizationContext` | ID da organização e usuário vêm dos claims assinados da sessão |
| Revalidação de papel | Troca do papel persistido durante uma sessão ativa e chamada HTTP de `/api/auth/me` | Cookie antigo recusado com 401; nova autenticação recebe os papéis atuais |
| Suíte backend | `dotnet test backend/PitCrm.sln --configuration Debug` com `PITCRM_TEST_CONNECTION` | 4 aprovados, 0 falhas |

As políticas estão disponíveis para os endpoints das próximas tarefas. Os testes HTTP de recusa por perfil em operações concretas serão registrados com os endpoints de clientes, locais e equipe (T07/T09/T15); ainda não há esses recursos na API.

## Evidências de T07

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Criação e autorização | HTTP com token CSRF por Administrador, Gestor e Operador | Administrador/Gestor criam (201); Operador recebe 403; sem sessão recebe 401 |
| Validação e CSRF | Requisição sem token e payload com nome/documento/instruções inválidos | Sem token, 400; payload inválido retorna erros dos campos; criação válida permite documento/instruções ausentes |
| Busca e paginação | `GET /api/customers?q=DOC-123&page=1&pageSize=1` e consulta paginada | Resultado e total calculados na organização; filtro busca nome/documento; caractere `%` literal; limites inválidos recebem 400 |
| Detalhe e isolamento | Acesso pelo ID criado em A, usando sessão de B | A recebe 200; B recebe 404 e lista vazia/total zero; `organizationId` adulterado no body não move o registro |
| Persistência | Migração `AddCustomers` em schema temporário e `pitcrm_dev` | Tabela, chaves e índices criados; schema de teste descartado |
| Suíte backend | `dotnet test backend/PitCrm.sln --configuration Debug` com `PITCRM_TEST_CONNECTION` | 6 aprovados, 0 falhas |

## Evidências de T08

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Alteração e identidade | Administrador altera cliente via HTTP | 200; mantém ID e data de criação e passa a buscar pelo novo nome |
| Autorização e isolamento | Operador altera cliente; conta de outra organização usa o ID | 403 para Operador; 404 entre organizações |
| Suíte backend | `dotnet test backend/PitCrm.sln --configuration Debug` com `PITCRM_TEST_CONNECTION` | 6 aprovados, 0 falhas |

## Evidências de T09

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Campos e criação | HTTP cria local sem endereço e depois com nome/endereço | Sem endereço: 400 e erro do campo; cadastro válido: 201; instruções ausentes aceitas |
| Edição e listagem | Gestor altera endereço/instruções; API lista pelo cliente | 200; mantém ID/vínculo e lista somente locais do cliente |
| Autorização e vínculos | Operador tenta criar; ID consultado sob outro cliente ou organização | 403 para escrita do Operador; 404 para cliente/local fora do escopo |
| Persistência | Migração `AddCustomerLocations` em schema temporário e `pitcrm_dev` | Tabela, índices, chaves e FK composta criados; schema temporário removido |
| Suíte backend | `dotnet test backend/PitCrm.sln --configuration Debug` com `PITCRM_TEST_CONNECTION` | 6 aprovados, 0 falhas |

## Evidências de T06

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Sessão frontend | Testes do `AuthService` com `HttpTestingController` | Busca CSRF, envia `X-CSRF-TOKEN`, carrega `/api/auth/me`, inclui cookies e limpa a sessão expirada |
| Login inválido | Navegador local, preenchimento de e-mail inexistente e senha de teste | API respondeu pela mensagem genérica; formulário mostrou erro sem indicar se o e-mail existe |
| Navegação protegida | Acesso direto a `/demandas` sem cookie | Guard consultou `/api/auth/me` e redirecionou para `/entrar` |
| Teclado | Navegação sequencial com Tab na tela de login | Ordem: marca, e-mail, senha e Entrar; foco visível e validação acessível nos campos |
| Layout | Viewports 1440×900 e 375×812 | Desktop em duas áreas e celular em uma coluna; sem rolagem horizontal; controles cabem no viewport |
| Build e testes Angular | `npm test -- --watch=false`; `npm run build` | 2 arquivos/5 testes aprovados; build de produção aprovado |

Não foi feita autenticação visual com conta provisionada no banco de desenvolvimento; o fluxo de login válido e retorno de sessão foi coberto pela integração HTTP backend (T04) e pelo teste do serviço Angular. A estrutura autenticada é exercitada via serviço e guard; logout HTTP foi verificado no backend e o estado do serviço no teste Angular.

## Evidências de T12

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Sessão necessária e três papéis | HTTP sem sessão e depois login como Administrador, Gestor e Operador | Sem sessão, 401; os três perfis autenticados recebem 200 |
| Limite organizacional e conta ativa | Dados temporários em duas organizações; uma conta inativa na organização autenticada | Retornados somente os usuários ativos da organização da sessão; inativo e outra organização excluídos |
| Minimização de dados | Inspeção do JSON | Cada item contém apenas `id` e `displayName`; e-mail e papel não aparecem |
| Suíte backend | `dotnet test backend/PitCrm.sln --configuration Debug` com `PITCRM_TEST_CONNECTION` | 5 aprovados, 0 falhas; schema temporário removido |

## Evidências de T10 e T11

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Busca e estados de clientes | Teste Angular com API simulada; carregamento, vazio e cadastro | Busca paginada com `pageSize=20`; estado vazio identificado; cadastro envia CSRF e atualiza a lista |
| Formulário e autorização visual | Testes com sessão Gestor e Operador; Operador seleciona cliente e consulta locais | Gestor recebe ações de gravação; botões de cadastro/edição não aparecem para Operador |
| Locais por cliente | Testes carregam cliente sem local, criam local pelo endpoint aninhado e listam múltiplos locais | Formulário requer nome/endereço; chamada usa ID do cliente selecionado; vazio e lista com locais verificados |
| Build/testes frontend | `npm test -- --watch=false`; `npm run build` | 3 arquivos de teste / 9 testes aprovados; build de produção aprovado sem avisos. Limite de aviso de estilos por componente passou de 4 para 6 kB; limite de erro continua 8 kB |

## Evidências de T13

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Criação e opcionais | Operador cria demanda via HTTP sem responsável, ticket ou OC; prioridade “média” e origem em minúsculas | 201; ID próprio, status “Recebida”, prioridade “Média”, origem “WhatsApp”; opções ausentes nulas |
| Validação e vínculo | Campos inválidos, local pertencente a outro cliente e responsável de outra organização | 400 com erro do campo; sem gravação dos vínculos rejeitados |
| Transação e atividade | Teste confirma atividade na criação; store recebe autor inválido para a atividade | Falha de FK sem persistência parcial da demanda |
| Persistência | Migração `AddDemandCreation` em schema de teste e `pitcrm_dev` | Tabelas, índices e FKs compostas criados; schema temporário removido |
| Suíte backend | `dotnet test backend/PitCrm.sln --configuration Debug` com `PITCRM_TEST_CONNECTION` | 6 aprovados, 0 falhas |

## Verificação de comportamento planejada

| Cenário | Critérios principais | Estado atual |
| --- | --- | --- |
| Preparar organização e primeiro Administrador; repetir provisionamento | PEO-01 | Backend verificado por teste e smoke test; demonstração integrada ainda em T23 |
| Entrar, consultar sessão e sair | PEO-02, PEO-03, PEO-04, PEO-25 | Backend T04 e integração Angular T06 verificados; interface validada em navegador |
| Trocar credencial inicial quando exigida | PEO-06 | Não executado; tarefas T15–T16 e T19 |
| Administrador cadastra equipe; Gestor/Operador recebem recusa | PEO-05, PEO-07 | Política avaliada em T05; endpoint HTTP e último Administrador pendentes em T15–T17 |
| Administrador/Gestor mantêm clientes e locais; Operador consulta | PEO-08, PEO-09, PEO-10, PEO-11 | APIs e telas T07–T11 verificadas; Gestor grava e Operador consulta |
| Criar demanda sem chamado e sem responsável | PEO-12, PEO-13, PEO-14, PEO-15 | API T13 verificada; formulário ainda pendente em T14 |
| Consultar responsáveis elegíveis; rejeitar referência incompatível na demanda | PEO-16, PEO-21 | T12 e vínculos de T13 verificados por HTTP; edição/concorrência pendentes em T20 |
| Buscar, filtrar e abrir detalhe após nova sessão | PEO-17, PEO-18, PEO-21 | Não executado; tarefas T18 e T21 |
| Editar demanda, registrar atividade e detectar versão antiga | PEO-19, PEO-20, PEO-22 | Não executado; tarefas T20 e T22 |
| Consultar ou gravar dados entre organizações A e B | PEO-21, PEO-25 | Não executado; tarefas T02–T23 conforme matriz de rastreabilidade |
| Simular falha transacional, resposta perdida e estados de interface | PEO-22, PEO-23, PEO-24 | Não executado; tarefas T13–T14, T20 e T22 |
| Percorrer as telas com teclado nas duas larguras propostas | PEO-26 | Não executado; tarefas T06, T10–T11, T14, T19 e T21–T23 |

## Limites

Perfil local da skill `tlc-spec-driven`. Os validadores originais dependentes do esquema upstream, de Git, commits e provas de feature não foram executados; seu gate completo não está aprovado. T01–T13 cobrem estrutura, autenticação, autorização inicial, interface de entrada, APIs/telas de clientes e locais, consulta de responsáveis e criação transacional de demanda. Equipe, formulário/consulta de demandas e critérios restantes seguem pendentes. A tela de login foi inspecionada no navegador; clientes/locais foram conferidos por testes de componentes e build, sem autenticação visual em navegador.

Revisão documental e implementação T01–T13 realizadas pelo mesmo agente, sem revisor independente ou delegação. O escopo da primeira entrega foi aprovado pelo usuário em 05/11/2026; os limites técnicos que continuam marcados como proposta não foram convertidos em regras comerciais.
