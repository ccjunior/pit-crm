# Primeira entrega operacional — verificação

**Objeto:** especificação, desenho, plano de tarefas e implementação da primeira entrega operacional.
**Estado:** T01–T18 verificadas; T19 implementada com testes focados/build aprovados; T21 implementada e verificada por testes focados/build; T24 implementada e compilada. Edição de demandas e demonstração integrada seguem pendentes; inspeção autenticada e conferência de teclado/viewport da T19 não foram executadas.

## Escopo da conferência

- Fidelidade às respostas D01–D03 e às fontes de produto/arquitetura.
- Rastreabilidade dos critérios PEO-01 a PEO-27 nas tarefas propostas.
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

## Evidências de T14

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Seleção e vínculo local | Teste carrega cliente, seleciona, busca locais; mudança limpa local anterior | Requisição de locais usa o cliente ativo; seleção antiga descartada |
| Campos do formulário | Teste envia campos obrigatórios e omite responsável/chamado/OC; prioridade opcional em enum | Body respeita requisitos D02/D06 e campos opcionais |
| CSRF, envio único e erros | Interceptor injeta token; dois submits seguidos; resposta 400 com erro de vínculo | Um POST; erro localizado; valores digitados preservados; sem repetição automática |
| Sucesso | API retorna demanda criada | Interface confirma ID interno e estado Recebida |
| Frontend | `npm test -- --watch=false`; `npm run build` | 4 arquivos/11 testes aprovados; build de produção sem avisos |

### Refinamento visual da abertura de demanda — 17/11/2026

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Validação e progresso | Teste de componente sem interação, após blur e após submit | Erros não aparecem no carregamento; checklist e mensagens refletem os campos preenchidos/tentativa de envio |
| Cliente e local | Busca por nome/documento, seleção por teclado (setas/Enter), dependência do Local e caso de um único local | Combobox pesquisa e seleciona; Local inicia desabilitado e o único resultado é pré-selecionado |
| Ações e conteúdo | Inspeção do formulário e teste de criação | Origem por opções segmentadas; há Cancelar, dica/contador de descrição e Ctrl+Enter |
| Contraste e shell | Cálculo de contraste do placeholder padrão contra branco; inspeção das regras do shell | 4,97:1; fundo escuro da sidebar acompanha a altura do shell |
| Testes e build | `npm test -- --watch=false --include=src/app/demands/new-demand-page.spec.ts`; `npm run build`; `git diff --check` | 4 testes aprovados; build de produção aprovado sem avisos; diff limpo |
| Suíte Angular completa | `npm test -- --watch=false` | Compilação geral impedida por fixtures desatualizadas preexistentes em `customers-page.spec.ts` (campos obrigatórios/duplicidade de endereço); a alteração da demanda não modifica esses testes |
| Inspeção visual autenticada | Navegador com sessão válida | Não executada; Administrador local ainda não foi provisionado |

## Evidências de T15 a T17

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Equipe: sessão, papéis e isolamento | HTTP sem sessão, Administrador, Gestor, Operador e outra organização | Sem sessão 401; Administrador lista/cria/edita; Gestor e Operador recebem 403; IDs de outra organização recebem 404 |
| Criação e troca obrigatória | Criar conta com senha temporária; tentar operação; trocar senha própria com CSRF; autenticar com credencial antiga e nova | Conta exige troca; operação negada até troca; senha antiga recebe 401 e nova autentica; segredo/hash não são retornados |
| Edição de conta | Administrador atualiza nome/e-mail/papel/estado; consultar listagem e persistência | HTTP 200 e campos persistidos na organização correta |
| Proteção do último Administrador | Desativar último Administrador ativo via API | HTTP 409; conta permanece ativa |
| Alterações concorrentes | Duas sessões Administradoras reduzem simultaneamente seus próprios perfis | Uma alteração é aceita e a outra é recusada; exatamente um Administrador ativo permanece |
| Migração e backend | Migração `AddInitialPasswordChange`; `dotnet test backend/PitCrm.sln --configuration Debug` com `PITCRM_TEST_CONNECTION` e `PITCRM_CONNECTION_STRING` | Migração aplicada em schema de teste e `pitcrm_dev`; 7 testes aprovados, 0 falhas |

## Evidências de T18 — busca paginada e detalhe de demandas

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Busca textual e ID interno | Consulta por UUID exato, trecho da descrição e referência de chamado compartilhada | ID e descrição retornam a demanda correspondente; referência compartilhada retorna ambas as demandas |
| Filtros e paginação | Combinar cliente, local e responsável; consultar página 2 com tamanho 2 | Filtros limitam os registros; resposta informa total antes da paginação, página/tamanho solicitados e itens da página |
| Detalhe e relacionamentos | Abrir demandas com e sem responsável; verificar atividade, criador, cliente e local | Nomes relacionados e atividade são retornados; responsável ausente aparece nulo, e o atribuído inclui ID e nome |
| Autorização e isolamento | Consulta anônima, leitura pelos três perfis, ID de outra organização e referência externa no caminho de detalhe | Anônimo recebe 401; três perfis podem consultar; registro fora da organização retorna 404; referência externa não é aceita como ID de detalhe |
| Limites de paginação | Página zero, tamanho 101 e índice cujo offset excede o limite inteiro | Todos retornam 400; padrão é página 1 com 20 itens e máximo 100 |
| Suíte backend e build | `dotnet build backend/PitCrm.sln --configuration Debug --no-restore`; `dotnet test backend/PitCrm.sln --configuration Debug` | Build aprovado sem avisos/erros; 7 testes aprovados, 0 falhas, usando schemas temporários no PostgreSQL local |
| Ajuste da fixture preexistente | Suíte revelou CPF de teste fictício incompatível com a validação atual do cliente; substituído por CPF válido formatado | Teste integrado voltou a executar; documento segue normalizado para dígitos conforme regra existente |

## Evidências de T21 — listagem, detalhe e painel lateral de demandas

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Listagem e alternância | Teste de componente carrega item da API simulada, alterna entre Cards e Tabela e confere os dados | Ambas as apresentações exibem a demanda; alternância preserva o estado da listagem |
| Busca, filtros e páginas | Inspeção dos parâmetros da chamada `GET /api/demands` | Busca textual, cliente, local, responsável, página e tamanho são enviados; paginação apresenta intervalo e total retornados |
| Alinhamento dos filtros | Comparar os grupos Buscar, Cliente, Local e Responsável no toolbar; conferir regra responsiva | Todos têm rótulo acima do controle e os campos alinham pela base; em telas menores a busca ocupa a linha inteira |
| Filtros adicionais e ordenação | `GET /api/demands` recebe status, prioridade e `sortBy` | Valida filtros com os valores de domínio disponíveis; prioridade filtra e ordena Alta/Média/Baixa/sem prioridade; ordem recente continua padrão |
| Referência sequencial | Cadastrar duas demandas na API e pesquisar `DEM-0001` | API retorna referência numérica em DTO; números aumentam dentro da organização; busca pelo código localiza a demanda; UUID continua como chave interna |
| Aviso de criação | Criar e observar a confirmação; dispensar manualmente e aguardar o timeout | Mostra código curto, ação Ver demanda e fechar; desaparece após 7 segundos e não é persistido entre navegações/recargas |
| Escaneabilidade | Inspecionar status/prioridade/data e grade de cards | Recebida/status tem paleta própria; Alta/Média/Baixa têm cores independentes; data relativa mantém data completa no tooltip; card tem seta, hover e foco visível; em listagens pequenas, colunas sem cards ficam vazias |
| Paginação | Comparar Clientes e Demandas em desktop e regra responsiva | Demandas usa o mesmo divisor superior, respiro, altura de controles e distribuição mobile de Clientes |
| Filtros em larguras menores | Conferir status principal, popover de filtros secundários e limpeza | Status permanece visível; cliente/local/responsável/prioridade/ordenação ficam recolhidos; filtros ativos exibem “Limpar filtros”; controles mantêm foco de teclado |
| Cards e estados vazios | Teste de componente com 2, 5 e 12 cards, descrição longa, lista vazia e filtro sem resultados | Contagem de cards preservada, descrição tem limite visual de duas linhas; vazio sem demandas e sem correspondências exibem mensagens e ações distintas |
| Hierarquia e contraste | Revisar descrição, cliente/local, data relativa, código e prioridade | Descrição em destaque; cliente/local e metadados em tons secundários mais escuros; Alta em vermelho, Média em âmbar, Baixa neutra; data completa no tooltip |
| Tipografia e dimensões | Comparar escala, grade/card/toggle entre Clientes e Demandas pelas regras CSS | Cards seguem colunas 4/3/2/1 e dimensões de Clientes; toggle mantém medidas pareadas; build Angular aprovado |
| Detalhe e atividade | Abrir uma linha/card e responder `GET /api/demands/{id}` com cliente, local e atividade | Painel lateral exibe dados persistidos, descrição, referências, autor e linha do tempo; erro de indisponibilidade tem estado próprio |
| Cadastro lateral | Teste de componente abre Nova demanda, confirma o formulário dentro do diálogo lateral e fecha sem trocar rota | Abertura/fechamento funcionam; criação continua usando o formulário T14 e ao salvar fecha o painel e recarrega a página 1 |
| Navegação | Inspeção das rotas Angular e links da shell desktop/mobile | `/` mantém Visão geral; logo aponta para `/`; Demandas em ambos os menus aponta para `/demandas` e fica ativa somente na lista |
| Testes focados e build | `npm test -- --watch=false --include=src/app/shell/demands-home.spec.ts --include=src/app/demands/new-demand-page.spec.ts`; `npm run build` | 8 testes focados aprovados; build Angular de produção aprovado |
| Backend e migração | `dotnet test backend/tests/PitCrm.Api.Tests/PitCrm.Api.Tests.csproj --configuration Debug --no-restore` com connection string de teste e schema temporário | 7 testes aprovados; migration de numeração aplicada no schema temporário, inclusive seed e índice único; `pitcrm_dev` não foi alterado |
| Inspeção autenticada | Abrir listagem, filtros, tabela, detalhe e cadastro em navegador com sessão válida | Não executada; Administrador local ainda não foi provisionado |

O domínio disponibiliza somente o status inicial **Recebida** nesta entrega; o filtro lista esse valor e “Todas”. As transições comerciais foram explicitamente deixadas fora do escopo (D07). Atribuição rápida e alteração de status não foram criadas porque a API de edição ainda pertence a T20/T22 e as regras de transição seguem pendentes. A migração foi aplicada pela suíte de integração em schema temporário, mas ainda não no banco local `pitcrm_dev`. Inspeção autenticada continua pendente.

## Evidências de T24 — endereço principal do Cliente

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Contrato de Cliente | Inspeção dos DTOs e endpoints POST/PUT/GET/lista | Inclui endereço, localidade, cidade e estado opcionais; campos em branco normalizados para nulo; leituras devolvem os valores |
| Persistência | Migração `AddCustomerMainAddress` aplicada no PostgreSQL local `pitcrm_dev` | Quatro colunas nullable adicionadas; clientes sem esses dados continuam válidos |
| Interface | `npm run build` | Build Angular aprovado sem avisos; formulário de criar/editar e painel de detalhe exibem os quatro campos |
| Backend | `dotnet build backend/PitCrm.sln --configuration Debug --no-restore` | Aprovado, 0 avisos e 0 erros |
| Suíte automatizada | Testes backend e Angular | Não executados nesta alteração |

O cadastro e a seleção dos locais de atendimento continuam no fluxo existente: o local permanece associado ao cliente e é carregado ao selecionar o cliente na abertura da demanda. A integração autenticada em runtime não foi verificada nesta alteração.

## Evidências de T19 — equipe e troca obrigatória de senha

| Verificação | Procedimento | Resultado |
| --- | --- | --- |
| Administração de equipe | Teste de componente de listagem, cadastro com papel-base e envio de senha temporária | O formulário envia `POST /api/users` com antifalsificação e, após sucesso, mostra a credencial para entrega fora do sistema; a lista é atualizada |
| Catálogo de papéis no banco | Teste de integração aplica migrações até a anterior, grava um Administrador com ID legado e aplica a nova migração | Migração preserva o ID do Administrador existente e inclui Gestor e Operador; teste também confirma os três papéis no banco novo |
| Edição e último Administrador | Teste de componente envia `PUT /api/users/{id}` e simula resposta 409 da API | O formulário permanece aberto e a mensagem do backend é apresentada |
| Primeiro acesso | Testes dos guards autenticados e da tela de troca | Sessão com `mustChangePassword` é direcionada à troca e não entra na shell; rota de troca só abre enquanto a flag está ativa; nova senha e confirmação precisam coincidir |
| Autorização da interface | Teste do guard de equipe | Apenas a sessão com perfil Administrador entra na rota; os outros perfis voltam ao início; a API continua sendo a autoridade de permissão |
| Sessão e API | Teste do serviço de autenticação | Troca usa o token CSRF e o estado local só libera o fluxo após resposta bem-sucedida da API |
| Testes focados | `npm test -- --watch=false --include=src/app/team/team-page.spec.ts --include=src/app/auth/initial-password-change-page.spec.ts --include=src/app/core/auth.service.spec.ts --include=src/app/core/auth.guard.spec.ts` | 4 arquivos e 12 testes aprovados |
| Regressão backend | `dotnet test tests/PitCrm.Api.Tests/PitCrm.Api.Tests.csproj --configuration Debug --artifacts-path <diretório-temporário>` com `PITCRM_TEST_CONNECTION` e schemas temporários | Suíte completa: 7 testes aprovados, 0 falhas; migração mantém Administrador existente, semeia papéis ausentes e cadastro Operador retorna 201. Saída isolada evita sobrescrever assemblies da API local ainda em execução |
| Banco local | `dotnet ef database update --project src/PitCrm.Infrastructure --startup-project src/PitCrm.Api --configuration Release`; consulta a `AspNetRoles` em `pitcrm_dev` | Aplicou `AddOrganizationDemandNumbers` e `SeedBaseIdentityRoles`; consulta retornou Administrador, Gestor e Operador. API em Debug permaneceu ativa |
| Smoke test autenticado | Usuário repetiu o cadastro de equipe após aplicação da migração | Usuário confirmou que funcionou perfeitamente; cadastro autorizado pela API concluído |
| Build Angular | `npm run build` | Aprovado sem avisos; bundle inicial de 496,13 kB e CSS da tela dentro do orçamento |
| Formatação do diff | `git diff --check` | Aprovado; Git reportou apenas avisos de conversão LF/CRLF da configuração local |
| Inspeção autenticada | Abrir equipe e troca obrigatória em navegador com conta real | Pendente; esta sessão não provisionou Administrador local nem alterou credenciais |

A migração `20261120141438_SeedBaseIdentityRoles` registra Administrador, Gestor e Operador usando `NormalizedName` como chave de conflito: preserva papéis existentes e inclui somente os ausentes. O endpoint valida contra o catálogo estático e orienta aplicar migrações se o banco estiver desatualizado; não cria papéis durante cadastro/edição. A proteção do último Administrador continua sendo aplicada pela API de T17. A suíte Angular completa não foi executada; somente os quatro arquivos focados acima foram testados. A suíte completa do backend (7 testes) foi compilada e executada em saída temporária porque a API local mantinha os assemblies Debug bloqueados. As migrações pendentes foram aplicadas a `pitcrm_dev` em build Release, mantendo a API ativa; uma consulta confirmou os três perfis persistidos. O usuário confirmou sucesso no cadastro autenticado após a migração.

## Verificação de comportamento planejada

| Cenário | Critérios principais | Estado atual |
| --- | --- | --- |
| Preparar organização e primeiro Administrador; repetir provisionamento | PEO-01 | Backend verificado por teste e smoke test; demonstração integrada ainda em T23 |
| Entrar, consultar sessão e sair | PEO-02, PEO-03, PEO-04, PEO-25 | Backend T04 e integração Angular T06 verificados; interface validada em navegador |
| Trocar credencial inicial quando exigida | PEO-06 | API T16 verificada; tela/guards T19 aprovados em testes focados e build; inspeção autenticada aguardando provisionamento local e cenário integrado T23 |
| Administrador cadastra/edita equipe; Gestor/Operador recebem recusa | PEO-05, PEO-07 | APIs T15/T17 e proteção do último Administrador verificadas; T19 cobre interface, guarda de rota, cadastro/edição e conflito 409 em testes focados; cenário integrado T23 pendente |
| Administrador/Gestor mantêm clientes e locais; Operador consulta | PEO-08, PEO-09, PEO-10, PEO-11, PEO-27 | APIs e telas T07–T11 verificadas; T24 adiciona endereço principal opcional e passou nos builds; testes automatizados pendentes |
| Criar demanda sem chamado e sem responsável | PEO-12, PEO-13, PEO-14, PEO-15 | API T13 e formulário T14 verificados; recarga/consulta integrada ainda em T23 |
| Consultar responsáveis elegíveis; rejeitar referência incompatível na demanda | PEO-16, PEO-21 | T12 e vínculos de T13 verificados por HTTP; edição/concorrência pendentes em T20 |
| Buscar, filtrar e abrir detalhe após nova sessão | PEO-17, PEO-18, PEO-21 | API T18 verificada por HTTP para filtros, paginação, atividade e isolamento; interface T21 verificada por testes de componente com API simulada; integração autenticada e recarga entre sessões aguardam T23 |
| Editar demanda, registrar atividade e detectar versão antiga | PEO-19, PEO-20, PEO-22 | Não executado; tarefas T20 e T22 |
| Consultar ou gravar dados entre organizações A e B | PEO-21, PEO-25 | Não executado; tarefas T02–T23 conforme matriz de rastreabilidade |
| Simular falha transacional, resposta perdida e estados de interface | PEO-22, PEO-23, PEO-24 | Não executado; tarefas T13–T14, T20 e T22 |
| Percorrer as telas com teclado nas duas larguras propostas | PEO-26 | Não executado; tarefas T06, T10–T11, T14, T19 e T21–T23 |

## Limites

Perfil local da skill `tlc-spec-driven`. Os validadores originais dependentes do esquema upstream e provas de feature não foram executados; seu gate completo não está aprovado. T01–T19 cobrem estrutura, autenticação, autorização inicial, entrada, clientes/locais, criação/busca/detalhe de demanda e interfaces administrativas de equipe/senha; T21 implementa a interface de busca/detalhe de demanda e abertura lateral de criação; T24 adiciona endereço principal opcional do cliente. Edição de demandas e critérios restantes seguem pendentes. Login foi inspecionado no navegador; T19 e outras telas foram verificadas por testes de componentes/build, sem sessão autenticada no navegador.

Revisão documental e implementação T01–T18/T24 realizadas pelo mesmo agente, sem revisor independente ou delegação. O escopo da primeira entrega foi aprovado pelo usuário em 05/11/2026; os limites técnicos que continuam marcados como proposta não foram convertidos em regras comerciais.
