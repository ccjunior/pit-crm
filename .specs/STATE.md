# Estado do projeto

Atualizado em 14/11/2026.

## Situação

O nome do produto foi aprovado em 14/11/2026: **PIT Obras**. O posicionamento comercial é para empresas de construção civil e reformas. PIT-CRM continua sendo o nome interno do projeto e do repositório. O README, os documentos de produto e os rótulos visíveis do Angular já usam a marca aprovada.

A restilização orientada pela imagem enviada em 14/11/2026 abrange shell desktop/mobile, acesso, visão geral, clientes/locais e formulários de cadastro e demanda. A paleta Project IT foi conferida no site oficial. `npm run build` passou; o Angular mantém um aviso de orçamento no CSS da tela de clientes (7,86 kB; aviso em 6 kB, erro em 8 kB). A conferência visual local cobriu o acesso em desktop e 390 px; telas autenticadas não foram abertas porque o Administrador local ainda não está provisionado.

A API passou a registrar `UserSecretsId` e README documenta `ConnectionStrings:DefaultConnection` no .NET User Secrets. A connection string local foi salva fora do repositório; `dotnet run` iniciou sem variável de ambiente e `GET /api/health` respondeu `ok`. O processo temporário foi encerrado depois da verificação.

Documentação de descoberta, escopo, modelo operacional e arquitetura disponível. O usuário aprovou a primeira entrega operacional em 05/11/2026. T01–T17 estão concluídas e T24 está implementada/build verificada localmente; T18 é a próxima. Schema, provisionamento idempotente, sessão com antifalsificação, políticas iniciais, interface de entrada, clientes/locais, criação de demanda e gestão de equipe/senha inicial foram verificados. O PostgreSQL 18.4 do Compose está ativo em `127.0.0.1:5433`; as migrações da aplicação e `AddCustomerMainAddress` foram aplicadas em `pitcrm_dev`. O provisionamento inicial do Administrador falhou por senha sem maiúsculas/minúsculas e foi revertido; nenhum usuário inicial foi criado.

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

- Solicitação de 14/11/2026: cadastro de CEP via ViaCEP, detalhes e status do local, contatos por cliente/local, revisão multi-tenant e normalização/validação de CPF/CNPJ.
- Funcionalidade: [clientes-locais-contatos](features/clientes-locais-contatos/spec.md); desenho: [design](features/clientes-locais-contatos/design.md); tarefas: [tasks](features/clientes-locais-contatos/tasks.md); evidências: [verification](features/clientes-locais-contatos/verification.md).
- CEP, endereço de local, status, contatos com chave de organização e documentos numéricos com validação/normalização foram implementados. A revisão corrigiu contagens com agregações tenant-scoped; definiu local ativo como `Status == Active`; adicionou erro/retry de contatos, filtros em chips, máscara/validação de documento e CEP, seleção de UF, contato responsável/Maps e proteções de fechamento. Migrações de contato responsável e normalização de UF aplicadas ao banco local; build Release e Angular passaram.
- A porta local 5006 segue ocupada por `PitCrm.Api.exe` antigo no diretório Debug; a nova rota de contatos retorna 404 nessa instância. Não encerrei o processo do usuário. Reiniciar o backend após atualizar build/migrações para validar a interface atualizada.
- Refinamento visual solicitado: largura de card voltou a quatro colunas em desktop amplo, com altura mínima e espaçamento vertical ampliados; ações Locais/Contatos mantêm cor da paleta e paginação fica separada da grade. Build Angular passou sem avisos.
- Revisão visual adicional: ações Locais/Contatos ficaram neutras; chip explicita “N locais ativos”; demandas zeradas neutras e valores positivos em âmbar; Contatos (0) atenuado; textos pequenos escurecidos; documento válido mascarado e corpo do card abre Cadastro. Build Angular passou sem avisos.
- Refinamento seguinte: validação de CPF/CNPJ permanece no formulário com mensagens específicas antes de salvar; documento legado fora do padrão aparece cru com alerta âmbar e tooltip, documento vazio é omitido, e texto de endereço/documento ganhou contraste. O chip informa “N locais ativos” e o corpo do card mostra “Ver cadastro”. `npm run build` passou. A rota autenticada de contatos em 5006 respondeu 401 sem sessão; a lógica de contagem é tenant-scoped, mas o valor 0 de um cliente não pôde ser confirmado sem autenticação.
- Ajuste visual posterior: o alerta explica o formato não reconhecido por tooltip no hover e mensagem auxiliar em telas touch; “Ver cadastro” fica alinhado aos chips com chevron interno; endereço/documento ganharam mais contraste. Foi confirmado no fluxo que Locais e Contatos abrem a aba correspondente, enquanto o card abre Cadastro.
- Refinamento da aba Cadastro: campos agrupados em Identificação e Endereço; endereço resumido com link para Maps, CEP ausente atenuado e instruções destacadas. Documento legado recebe instrução de correção; edição aparece apenas nessa aba. O resumo apresenta datas e demandas recebidas disponíveis, sem inferir demandas abertas ou histórico por usuário. O diálogo modal prende o foco, trata Escape e devolve foco ao elemento acionador ao fechar.
- Ajustes de locais: lista compactada com pin do Maps ao lado de Editar, status maior em verde/âmbar/cinza, nome igual à rua não duplica o endereço, responsável exibe cargo/telefone e atalho de WhatsApp. Link manual opcional do Google Maps salvo em `CustomerLocation.MapUrl`, validado no Angular/API e aplicado por migração local; link substitui a busca textual automática. Estado vazio tem ação “Adicionar primeiro local” e a linha superior duplicada foi removida. Build .NET Release e Angular passaram; migração foi aplicada ao `pitcrm_dev`. Contagem de demandas abertas e campos de ciclo de obra seguem pendentes de regra/modelo.
- Refinamento de contatos: chips mostram contagens globais/por cliente/por local e rolam horizontalmente; rótulo Cliente foi encurtado. Estado vazio tem texto contextual e botão de inclusão que preserva o vínculo ativo. Formulário sugere funções de obra sem restringir texto, mascara telefone brasileiro e oferece atalho WhatsApp. Build Angular passou sem avisos.
- Pendências de produto/UI: não existem tipos/status de cliente, entidade e ciclo de vida de obra, estados terminais de demanda ou última atividade operacional. Filtros e métricas dependentes desses conceitos ficaram fora da implementação; cards informam locais ativos e demandas no estado Recebida. Testes automatizados, isolamento negativo multi-tenant, ViaCEP real e inspeção visual autenticada não foram executados; o Administrador local precisa ser provisionado.
- Decisão documental confirmada: CPF/CNPJ somente numéricos com validação de dígitos e armazenamento sem pontuação; CNPJ alfanumérico será rejeitado. Essa escolha não acompanha o padrão novo emitido pela Receita e deve ser reavaliada se o produto atender esses cadastros. Testes automatizados, verificações negativas multi-tenant e conferência visual autenticada não foram executados.

- Solicitação inicial: “vamos”, para especificar a primeira entrega; aprovação posterior: “aprovo”, autorizando o escopo em 05/11/2026.
- Funcionalidade: [primeira-entrega-operacional](features/primeira-entrega-operacional/spec.md).
- Resultado: 27 critérios de aceite, desenho técnico atualizado e 24 tarefas de implementação com dependências e verificação planejada.
- Decisões confirmadas D01–D09: [context.md](features/primeira-entrega-operacional/context.md).
- D01: Administrador/Gestor cadastram e editam clientes/locais; Operador apenas consulta esses cadastros.
- D02: cliente, local, descrição e origem obrigatórios na abertura; responsável operacional interno opcional.
- D03: organização e primeiro administrador provisionados de forma controlada; administrador cadastra a equipe.
- Fontes de produto e roadmap atualizados com essas respostas; o restante da Etapa 0 permanece aberto.
- Verificação desta especificação: [verification.md](features/primeira-entrega-operacional/verification.md).
- Conferência documental original: 26 critérios, 23 tarefas e 40 dependências; D09 e T24 acrescentam endereço principal opcional do cliente e duas dependências. Os hashes dos 36 arquivos de skills foram preservados.
- Implementação: T01–T17 concluídas e T24 implementada/build verificada localmente em 05/11/2026; T18 é a próxima. D04–D09 registrados em [context.md](features/primeira-entrega-operacional/context.md). PEO-01 verificado por teste e smoke test CLI; persistência, sessão, matriz de papéis, Angular, consulta de responsáveis, clientes/locais, criação de demandas e APIs de equipe/senha verificados; ver [verification.md](features/primeira-entrega-operacional/verification.md). Limites técnicos restantes continuam identificados como proposta.

## Pendências de produto

Permanecem as questões da Etapa 0 em [03-ROADMAP-E-PENDENCIAS.md](../docs/03-ROADMAP-E-PENDENCIAS.md): restante da matriz de permissões (especialmente aprovações e financeiro), perfil Financeiro, categorias de custo, referências específicas dos clientes sem chamado e recorte de importação histórica. As permissões iniciais de clientes/locais e o acesso inicial estão resolvidos em D01–D03.

## Handoff

Execução: primeira entrega aprovada; T01–T17 concluídas e T24 implementada/build verificada localmente; T18 pendente. Repositório inicializado em `ccjunior/pit-crm`; commits iniciais e checkpoint de código T14–T17 estão em `origin/main`. O Compose foi ajustado ao caminho de volume do PostgreSQL 18; serviço ativo e conexão local em 5433 confirmada. As migrações estão aplicadas; o primeiro Administrador precisa ser provisionado novamente com uma senha que inclua letras maiúsculas e minúsculas. Não declarar a Etapa 0 inteira concluída.
