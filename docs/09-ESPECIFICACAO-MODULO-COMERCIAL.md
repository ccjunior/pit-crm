# PIT Obras — Especificação para conclusão do módulo Comercial

**Versão:** 1.0 (rascunho para validação)
**Produto:** PIT Obras | **Empresa:** Project IT
**Documento relacionado:** `08-OVERVIEW-PARA-REVISAO-ESPECIALISTA.md`
**Situação no projeto:** ampliação de escopo e execução em etapas confirmadas pelo usuário em 22/11/2026, após a recomendação de concluir primeiro a validação da T26. As decisões de produto ainda abertas continuam pendentes para suas respectivas etapas; a confirmação da sequência não define silenciosamente os detalhes dessas regras.
**Destinatário principal:** agente de IA responsável por revisar esta documentação e implementar o módulo.

---

## 0. Como usar este documento (instruções para a IA)

1. **Este documento é uma especificação, não a verdade absoluta sobre o código.** Antes de implementar qualquer item, compare-o com o código atual do PIT Obras (backend .NET 10, frontend Angular, PostgreSQL) e com o documento 08.
2. **Quando houver divergência** entre este documento e o código existente, não escolha silenciosamente. Registre a divergência na seção "Registro de divergências" (final do documento), proponha o ajuste e siga a decisão já tomada no código quando ela for equivalente ou melhor. Se a divergência afetar modelo de dados, segurança ou regra de negócio, pare e peça decisão ao responsável.
3. **Ajuste esta documentação** sempre que descobrir que algo está incorreto, incompleto ou desatualizado. Mantenha a seção "Decisões em aberto" atualizada.
4. **Implemente em fases** (seção 12), com testes em cada fase. Não avance de fase com a anterior sem os critérios de aceite cumpridos.
5. **Respeite a arquitetura existente:** monólito modular, Clean Architecture, Vertical Slice, ASP.NET Core Identity com cookie HttpOnly, isolamento por organização no backend.
6. **Segurança tem precedência sobre conveniência.** Em caso de dúvida, adote a opção mais restritiva e registre a dúvida.
7. **Nunca invente dados de domínio** (regras fiscais, alçadas, prazos). Quando faltar definição, implemente configurável por organização com valor padrão conservador e registre em "Decisões em aberto".

---

## 1. Objetivo e escopo

### 1.1 Objetivo
Concluir o módulo Comercial do PIT Obras, de modo que uma empresa de construção/reformas consiga receber, avaliar, orçar, negociar e fechar solicitações de orçamento com rastreabilidade, prazos, histórico e indicadores, e que a aprovação gere de forma segura o rascunho de demanda do módulo Operacional.

### 1.2 Dentro do escopo
- Solicitação de orçamento (entidade central), com origem manual ou por e-mail.
- Revisões de proposta dentro da solicitação, com composição de preço.
- Responsável, prazos, validade e follow-up.
- Histórico de atividades e comentários.
- Visita técnica pré-orçamento.
- Resultado da negociação e motivos estruturados.
- Aprovação e criação idempotente de rascunho de demanda (já parcialmente implementado).
- Conector de e-mail com extração assistida por IA (absorvendo a aplicação legada).
- Indicadores do funil.
- Testes, segurança e observabilidade do módulo.

### 1.3 Fora do escopo desta versão
- Geração de PDF/documento da proposta e envio ao cliente pelo sistema (permanecem externos).
- Assinatura eletrônica e portal do cliente.
- Leitura automática de e-mail de **envio** para registrar o envio (previsto como evolução futura, seção 13).
- Contratos (decisão própria; apenas deixar o vínculo opcional preparado, seção 6.4).
- Módulo Financeiro.

---

## 2. Terminologia (obrigatória em código, banco e interface)

| Termo | Significado |
|---|---|
| **Solicitação** (`QuoteRequest`) | Pedido de orçamento recebido de cliente/prospect. É a entidade central do comercial e existe mesmo que a empresa decida não cotar. |
| **Proposta / Revisão** (`ProposalRevision`) | Documento comercial elaborado pela empresa em resposta à solicitação. Uma solicitação tem 0..N revisões (A, B, C...). |
| **Rascunho de demanda** (`DemandDraft`) | Registro gerado pela aprovação, que só entra na operação após conferência e confirmação. |
| **Demanda** | Entidade do módulo Operacional. |

**Correção importante de modelagem:** o que chega por e-mail do cliente é uma **solicitação**, nunca uma proposta. O conector de e-mail cria **solicitações**. Revisões de proposta só são criadas na etapa de elaboração. A IA, portanto, não preenche campos de proposta (preços, itens orçados), apenas dados da solicitação (itens pedidos, descrição, local, prazo, referência do cliente).

> Se o código atual usa o nome "proposta" para a entidade central, avaliar renomeação ou, no mínimo, mapear claramente os nomes e registrar na seção de divergências.

---

## 3. Estado atual (conforme documento 08)

Implementado localmente: estrutura de dados, persistência, migração, endpoints, páginas iniciais, Kanban/lista, fila de rascunhos e aprovação com criação idempotente. Em 22/11/2026, as suítes locais passaram (backend 8/8; Angular 34/34), a inspeção autenticada cobriu Admin/Gestor e o build Angular passou com bundle inicial de 416,31 kB (limite 500 kB). A confirmação visual de rascunho como Operador e inspeção touch/mobile foram adiadas para o fechamento integrado do Comercial por orientação do usuário. A migração foi aplicada apenas ao banco local de desenvolvimento.

A Fase 0 foi auditada em 22/11/2026 e registrada na seção 14. As regras ainda abertas devem ser resolvidas antes das fases de implementação que dependem delas.

---

## 4. Fluxo e máquina de estados

### 4.1 Estados da solicitação
`Recebida → EmAvaliacao → EmElaboracao → Enviada → EmNegociacao → Aprovada`
Encerramentos: `NaoCotada`, `Recusada`, `Expirada`, `Cancelada`.

### 4.2 Regras
- As **transições válidas ficam definidas em um único lugar** do domínio (máquina de estados explícita), não espalhadas por handlers.
- Cada transição registra: usuário (ou `Sistema`/`Conector`), data/hora, estado anterior, estado novo e motivo quando aplicável.
- `NaoCotada` exige **motivo estruturado** (lista configurável por organização + observação livre).
- `Recusada`, `Cancelada` e `Expirada` também exigem motivo estruturado.
- Transições para `Enviada`, `Aprovada` e encerramentos são **registradas manualmente** por usuário autorizado (na v1 o envio é externo).
- `Expirada` pode ser aplicada por job agendado quando a validade da última revisão enviada vencer (seção 5.3).
- Reabertura de solicitação encerrada: decidir (ver "Decisões em aberto"); se permitida, exige permissão específica e registro.
- Transição inválida deve retornar erro de domínio claro (HTTP 409 ou 422, seguir padrão da API) e nunca alterar dados.

### 4.3 Concorrência
- Usar controle otimista (token de versão/`xmin`/rowversion), já adotado nas demandas.
- Aprovação concorrente (duas requisições simultâneas) deve resultar em **um único rascunho**.

---

## 5. Requisitos funcionais

### 5.1 Solicitação (cadastro e acompanhamento)
- **RF-01** Criar solicitação manualmente: cliente/prospect, local (opcional se ainda não definido), descrição do pedido, canal de origem, referência do cliente (RFQ/pedido/chamado), prazo desejado pelo cliente, responsável comercial.
- **RF-02** Prospect que ainda não é cliente: permitir registrar dados mínimos do prospect (nome, contato) sem criar cliente. Criação/associação de cliente e local continua restrita a Administrador/Gestor.
- **RF-03** Canal de origem (enum configurável): e-mail, telefone, WhatsApp, indicação, visita, licitação, outro.
- **RF-04** Kanban e lista exibem os **mesmos registros** (já definido); filtros por estado, responsável, cliente, período, origem, prazo vencido.
- **RF-05** Número único legível por organização (ex.: `SOL-2026-0001`), gerado de forma segura contra concorrência.
- **RF-06** Anexos da solicitação (e-mail original, arquivos do cliente, fotos), com controle de acesso por organização (seção 9.5).

### 5.2 Responsável, prazos e follow-up
- **RF-07** Responsável comercial obrigatório antes de sair de `Recebida` para `EmAvaliacao` (ou atribuição automática configurável).
- **RF-08** Dois prazos: **prazo do cliente** (quando ele quer a proposta) e **prazo interno** (meta da empresa). Indicador visual de atraso.
- **RF-09** **Validade** por revisão enviada. Job agendado marca a solicitação como `Expirada` quando vencer, respeitando regra configurável (ex.: só se não estiver em negociação ativa) e registrando a transição como feita pelo `Sistema`.
- **RF-10** Follow-up: data da próxima ação e lembrete. Visão "minhas pendências". Notificação interna (e-mail/in-app, conforme infraestrutura de notificações; ver seção 8).

### 5.3 Histórico de atividades e comentários
- **RF-11** Linha do tempo no detalhe da solicitação com: mudanças de estado, edições relevantes, comentários internos, ligações/reuniões registradas, e-mails vinculados, visitas, revisões criadas.
- **RF-12** Comentários internos com menção a usuários da organização.
- **RF-13** Registro de atividade manual (tipo: ligação, reunião, mensagem, observação) com data e autor.
- **RF-14** Linha do tempo é **append-only** (seção 9.4).

### 5.4 Visita técnica pré-orçamento
- **RF-15** Agendar visita técnica na solicitação (data, responsável, local).
- **RF-16** Registrar resultado: observações, medidas, fotos (anexos).
- **RF-17** Estados da visita: `Agendada`, `Realizada`, `Cancelada`, `Remarcada`. A solicitação pode ir para `EmElaboracao` sem visita (a visita é opcional, configurável como obrigatória por tipo de serviço no futuro).

### 5.5 Revisões de proposta e composição de preço
- **RF-18** Criar revisão dentro da solicitação, com identificação sequencial (A, B, C... ou 1, 2, 3; definir padrão), data, autor, observação do que mudou.
- **RF-19** Cada revisão tem **itens** com: descrição, unidade, quantidade, custo unitário (opcional/restrito), preço unitário, total. Campo de referência opcional a catálogo.
- **RF-20** **Margem e BDI:** parâmetros por revisão (percentuais), com valores padrão por organização. Cálculo determinístico no backend, com `decimal` de precisão explícita e regra de arredondamento documentada.
- **RF-21** **Visibilidade de custo/margem restrita por permissão.** Operador não vê custo/margem por padrão (decisão a confirmar).
- **RF-22** **Alçada de aprovação interna:** desconto ou margem abaixo do limite configurado exige aprovação de Gestor antes de a revisão poder ser marcada como enviada. Registrar quem aprovou e quando.
- **RF-23** Revisões enviadas são **imutáveis**. Qualquer alteração cria nova revisão. Deve ser possível **comparar** duas revisões (diff de itens, valores, condições).
- **RF-24** Catálogo/modelos de itens reutilizáveis por organização (fase posterior; importação de tabelas externas como SINAPI fica como avaliação futura).
- **RF-25** Condições comerciais da revisão (prazo de execução estimado, condição de pagamento, observações, exclusões), em campos estruturados + texto livre.
- **RF-26** Envio registrado: data, canal, destinatário, observação. Opcionalmente anexar o PDF enviado (gerado externamente) para guarda.

### 5.6 Resultado, aprovação e conversão
- **RF-27** Registrar resultado informado pelo cliente: aprovada (indicando qual revisão), recusada, sem resposta/expirada, cancelada.
- **RF-28** Aprovação deve registrar: **revisão aprovada**, data, quem registrou, **evidência da aprovação** (e-mail, PO, mensagem; anexo ou referência obrigatória, configurável) e referência do cliente (pedido/chamado) quando houver.
- **RF-29** Aprovação cria **rascunho de demanda de forma idempotente** (já implementado; manter e testar). Os itens da revisão aprovada são copiados como subitens do rascunho em formato de **snapshot imutável**, preservando referência ao item e à revisão de origem.
- **RF-30** O subitem do rascunho **não carrega custo/margem** a menos que decisão posterior defina o contrário (impacta quem vê o quê no Operacional). Registrar em "Decisões em aberto".
- **RF-31** Fila de rascunhos: Administrador/Gestor vinculam/criam cliente e local; Operador completa e confirma o rascunho. Demanda confirmada entra no fluxo como `Recebida`.
- **RF-32** Aprovação, envio e encerramento exigem permissão específica (seção 9.2).

### 5.7 Indicadores do funil
- **RF-33** Painel com: solicitações recebidas por período, taxa de cotação, taxa de aprovação, tempo médio até o envio, tempo médio de decisão do cliente, valor em negociação, valor aprovado, distribuição de motivos de não cotação/perda, solicitações com prazo vencido, desempenho por responsável.
- **RF-34** Indicadores respeitam o isolamento por organização e permissões (valores monetários só para quem pode ver).
- **RF-35** Para a **IA do conector**, expor também: taxa de campos corrigidos por humano (seção 8.8).

---

## 6. Modelo de dados (diretrizes)

Esta seção descreve **intenção**; a IA deve adaptar aos nomes e convenções do código existente.

### 6.1 Entidades principais
- `QuoteRequest` (solicitação): `Id`, `OrganizationId`, `Number`, `Status`, `CustomerId?`, `ProspectName?`, `ProspectContact?`, `SiteId?`, `Description`, `OriginChannel`, `ClientReference?`, `ClientDeadline?`, `InternalDeadline?`, `ResponsibleUserId?`, `NextFollowUpAt?`, `SourceType` (Manual | Email), `SourceEmailMessageId?`, `CreatedAt/By`, `RowVersion`.
- `QuoteRequestStatusHistory`: transições (append-only).
- `QuoteRequestActivity`: comentários e atividades (append-only na prática; edição só com registro).
- `SiteVisit`: visita técnica.
- `ProposalRevision`: `Id`, `QuoteRequestId`, `RevisionCode`, `Status` (Rascunho | AguardandoAprovacaoInterna | Pronta | Enviada | Substituida), `ValidUntil?`, `BdiPercent`, `MarginPercent`, `PaymentTerms`, `ExecutionTerm`, `Notes`, `SentAt?`, `SentChannel?`, `SentTo?`.
- `ProposalRevisionItem`: `Description`, `Unit`, `Quantity`, `UnitCost?` (restrito), `UnitPrice`, `Total`, `CatalogItemId?`, `SortOrder`.
- `ApprovalRecord`: revisão aprovada, evidência, usuário, data, referência do cliente.
- `DemandDraft` e subitens: já existentes; garantir `SourceQuoteRequestId`, `SourceRevisionId`, `SourceItemId`.
- `LossReason` / `NoQuoteReason`: tabela configurável por organização.
- `Attachment` (transversal): ver 9.5.

### 6.2 Dinheiro e números
- Usar `decimal` com precisão/escala explícitas no banco (ex.: `numeric(18,2)` para valores, `numeric(18,4)` para quantidades/percentuais intermediários, ajustar conforme necessidade).
- Moeda registrada (BRL por padrão). Definir regra única de arredondamento e centralizá-la em um serviço de domínio testado.
- Preparar o modelo para futuras retenções/impostos sem implementá-los agora.

### 6.3 Multi-tenant
- Toda entidade com `OrganizationId`.
- **Global query filter** do EF Core por organização em todas as entidades de tenant.
- Avaliar **Row Level Security** do PostgreSQL como segunda camada para dados comerciais/financeiros.
- Teste automatizado que falha se uma entidade de tenant não possuir o filtro configurado.

### 6.4 Preparação para contratos (sem implementar)
- Deixar `ContractId` nulável apenas se já houver módulo/entidade de contrato; caso contrário, **não criar coluna órfã**. Registrar a decisão em "Decisões em aberto". Regra de dependência: Operacional/Financeiro referenciam contrato por ID, nunca o contrário.

### 6.5 Dependências entre módulos
- Sentido: **Comercial → Operacional → Financeiro**. O Comercial não conhece Operacional/Financeiro além do contrato de criação do rascunho.
- Comunicação por interface/evento explícito (ex.: `QuoteRequestApproved`), preferencialmente com **outbox** para consistência.
- Impor com **teste de arquitetura** (ex.: NetArchTest) para que a regra não se degrade.

---

## 7. Interface (Angular)

- Respeitar o padrão visual já estabelecido (formulários, filtros, botões, paginação).
- Rotas **lazy-loaded** por área (propostas, rascunhos, etc.). **Resolver o bundle inicial acima de 500 kB** como parte da Fase 1.
- Estados de erro, vazio, carregamento e permissão negada em todas as telas.
- Uso em dispositivos móveis validado (Kanban e detalhe em tela pequena).
- Detalhe da solicitação: cabeçalho (estado, responsável, prazos), abas ou seções para revisões, atividades, visita técnica, anexos, e-mails vinculados.
- Revisão com itens editáveis, totais recalculados pelo backend (o frontend apenas exibe/pré-visualiza).
- Fila de rascunhos com indicação clara do que falta para confirmar.
- Para solicitações originadas de e-mail: **tela de conferência** com campos extraídos, trecho de origem, indicador de confiança e ação de aceitar/corrigir por campo.

---

## 8. Conector de e-mail com IA

### 8.1 Princípio
O conector **cria solicitações em estado `Recebida` com origem `Email`**, preenchidas como **sugestão**. Nunca cria proposta, nunca avança estado, nunca aprova, nunca cria cliente ou local. Uma pessoa confere e confirma.

### 8.2 Absorção da aplicação legada

Existe uma aplicação anterior com rotina de leitura de e-mail, envio de anexo para tratamento por IA via **OpenRouter** e coleta de informações.

- **Caminho do projeto legado:** `D:\Workspace\Novos Projetos\mello-machado-emailManager`

**Instruções à IA para esta absorção:**

1. **Auditoria primeiro, sem alterar o projeto legado.** Tratar o legado como somente leitura. Mapear:
   - como o e-mail é lido (protocolo IMAP/POP/API, frequência, autenticação, onde ficam as credenciais);
   - como anexos são obtidos e enviados à IA (tipos suportados, limites, conversão/OCR);
   - quais modelos/provedores e quais **prompts** são usados, e o formato da resposta;
   - como a resposta da IA é validada e persistida;
   - tratamento de erros, retentativas, duplicidade e logs;
   - dependências, linguagem/stack, configuração e segredos.
2. **Produzir um relatório de aderência** (curto) com: o que pode ser reaproveitado (lógica, prompts, mapeamentos de campos, heurísticas), o que deve ser reescrito para a arquitetura do PIT Obras e o que deve ser descartado. Incluir riscos de segurança encontrados.
3. **Não copiar credenciais, tokens ou segredos** do legado para o repositório do PIT Obras ou para este documento. Se encontrar segredos em texto claro, alertar o responsável para rotacioná-los.
4. **Reaproveitar o conhecimento, não necessariamente o código.** Prompts, campos extraídos e regras de mapeamento do legado são ativos valiosos; a implementação deve seguir Clean Architecture/Vertical Slice, multi-tenant e os requisitos de segurança abaixo.
5. **Adequações obrigatórias** em relação ao legado (verificar uma a uma e registrar o que já existia e o que foi adicionado): isolamento por organização, validação por schema, defesa contra prompt injection, varredura de anexos, idempotência/deduplicação, armazenamento do e-mail original, observabilidade, tratamento de LGPD, limites e custos de IA.
6. **Compatibilidade de dados:** se o legado tiver histórico de e-mails/extrações úteis, propor (sem executar) estratégia de importação, tratando como parte da "importação assistida de histórico" do documento 08.

### 8.3 Ingestão
- Conexão por **OAuth** (Microsoft Graph ou Gmail API) sempre que possível, em vez de IMAP com senha. Se o legado usa IMAP e o cliente exigir, tratar como exceção documentada.
- Credenciais **por organização**, criptografadas em repouso, com tratamento de expiração/revogação e alerta ao administrador.
- Caixas monitoradas configuráveis (pasta/label, remetentes permitidos opcionalmente, filtros de assunto).
- Ingestão **idempotente**: identificar e-mail por `Message-ID` + conta; reprocessar o mesmo e-mail nunca duplica solicitação.
- **Armazenar o e-mail original bruto (.eml)** e anexos originais (storage de objetos, criptografado, acesso por tenant) para auditoria e reprocessamento.

### 8.4 Agrupamento e deduplicação
- Agrupar por thread (`In-Reply-To`, `References`, ID de conversa do provedor), assunto normalizado, remetente e hash dos anexos.
- E-mail novo que pertence a solicitação existente é **anexado a ela** como atividade (ex.: "lembrete do cliente"), não cria outra solicitação.
- Em caso de incerteza no agrupamento, **não agrupar automaticamente**: sinalizar "possível duplicata" para decisão humana.
- Registrar a referência do cliente (RFQ/pedido) extraída para apoiar o agrupamento.

### 8.5 Pipeline assíncrono
Estados do e-mail processado (sugestão): `Recebido → Armazenado → EmVarredura → Bloqueado | EmExtracao → Extraido → AguardandoConferencia → Convertido | Descartado | Falhou`.
- Processamento por **fila e workers**, com retry com backoff e **dead letter queue** visível ao suporte/administrador.
- Limites explícitos: tamanho de anexo, quantidade, tipos suportados (PDF, imagens, planilhas, docx), tempo máximo por item.
- PDF escaneado/imagem: OCR (ou modelo com visão) tratado como etapa própria, com custo e qualidade medidos.
- Cada etapa registra logs estruturados com `OrganizationId`, `EmailId` e correlação, **sem** conteúdo sensível em log.

### 8.6 Extração por IA (OpenRouter)
- Usar o provedor/modelo de forma **configurável** (OpenRouter já usado no legado), com registro do modelo e versão do prompt em cada extração.
- A IA devolve **apenas JSON** validado por **schema estrito**; resposta fora do schema é rejeitada (não "consertada" silenciosamente) e vai para conferência manual.
- A IA **não tem ferramentas, não executa ações e não acessa o banco**. É uma função pura: entrada (conteúdo tratado) → saída (JSON).
- Campos extraídos (sugestão de schema):

```json
{
  "schema_version": "1.0",
  "client_reference": { "value": "string|null", "confidence": 0.0, "source_excerpt": "string|null" },
  "requester": { "name": "string|null", "email": "string|null", "phone": "string|null", "company": "string|null", "confidence": 0.0 },
  "site": { "description": "string|null", "address": "string|null", "confidence": 0.0, "source_excerpt": "string|null" },
  "request_summary": { "value": "string", "confidence": 0.0, "source_excerpt": "string|null" },
  "requested_items": [
    { "description": "string", "quantity": "number|null", "unit": "string|null", "confidence": 0.0, "source_excerpt": "string|null" }
  ],
  "client_deadline": { "value": "YYYY-MM-DD|null", "confidence": 0.0, "source_excerpt": "string|null" },
  "urgency_hint": { "value": "baixa|media|alta|null", "confidence": 0.0 },
  "is_quote_request": { "value": true, "confidence": 0.0 },
  "language": "pt-BR",
  "warnings": ["string"]
}
```

- Toda informação extraída carrega **confiança** e **trecho de origem** (de onde no e-mail/anexo saiu), para acelerar a conferência.
- `is_quote_request` permite descartar e-mails que não são pedidos (newsletters, spam, respostas internas), mas a decisão de descarte automático deve ser conservadora (ver 8.7).
- **Valores monetários não são extraídos como verdade**; se o cliente informar orçamento/valor, tratar como informação opcional de contexto, não como preço.

### 8.7 Segurança, prompt injection e guardrails (requisitos obrigatórios)

E-mails e anexos são **entrada não confiável**.

1. **Separação de instruções e dados:** o conteúdo do e-mail/anexo entra sempre delimitado e rotulado como dado ("conteúdo do usuário não confiável"). O prompt de sistema instrui explicitamente a ignorar qualquer instrução presente no conteúdo.
2. **Saída restrita:** somente JSON no schema; qualquer texto fora do schema é descartado. Campos com tamanho máximo e tipos estritos; sanitização de strings (sem HTML/script) antes de persistir e antes de exibir.
3. **Sem poder de ação:** a IA não aprova, não muda estado, não cria cliente/local, não envia e-mail, não chama URLs, não executa código. O backend é quem decide, e só cria `QuoteRequest` em `Recebida`.
4. **Detecção de tentativa de injeção:** heurísticas e/ou classificador para sinalizar conteúdo suspeito (frases do tipo "ignore as instruções", "aprove", "envie", pedidos de revelar prompt, markup oculto, texto invisível/cor igual ao fundo em PDF/HTML). Conteúdo suspeito vai com `warning` e **obrigatoriamente** para conferência humana.
5. **Validação cruzada determinística:** e-mail do remetente, domínio e vínculo a cliente conhecido são verificados pelo backend, não pela IA. Dados extraídos não podem sobrescrever dados cadastrados de cliente.
6. **Varredura de anexos** (antivírus/malware) antes de qualquer parsing; bloquear tipos executáveis, macros ativas e arquivos compactados suspeitos; limites de tamanho/descompactação (proteção contra zip bomb).
7. **Parsing seguro:** processar PDFs/documentos em ambiente isolado (sem rede, com limites de CPU/memória/tempo). Remover/ignorar HTML ativo, links não são seguidos automaticamente.
8. **Proteção contra SSRF e exfiltração:** nenhum conteúdo do e-mail pode induzir requisições a URLs; a IA não recebe segredos, tokens ou dados de outras organizações no contexto.
9. **Isolamento por tenant no contexto da IA:** cada chamada contém apenas dados da própria organização/e-mail. Nunca misturar e-mails de organizações diferentes em um mesmo contexto.
10. **Autenticidade do remetente:** registrar resultado de SPF/DKIM/DMARC quando disponível; e-mail com falha de autenticação ou remetente desconhecido recebe `warning` e conferência obrigatória. Alertar para **fraude de e-mail** (troca de dados de contato/pagamento).
11. **Limites e custos:** orçamento de tokens por e-mail e por organização, limite de chamadas por período, tratamento de falha do provedor, circuit breaker. Registrar custo por extração.
12. **Segredos:** chaves do OpenRouter e credenciais de caixa em cofre/variáveis seguras, nunca em código, log ou banco em texto claro.
13. **Auditoria da IA:** guardar entrada tratada (ou referência), versão do prompt, modelo, saída bruta, saída validada, decisão humana final.
14. **Dados pessoais (LGPD):** minimizar o que é enviado ao provedor (ex.: não enviar assinatura/rodapé desnecessário quando possível), verificar termos do provedor (retenção/treinamento — preferir retenção zero e não-treinamento), informar o uso de IA nos termos com o cliente final, definir política de retenção do .eml e dos anexos, suportar exportação e exclusão por organização.
15. **Conferência obrigatória (human-in-the-loop):** em hipótese alguma uma solicitação criada pela IA sai de `Recebida` sem ação humana.
16. **Kill switch:** administrador pode pausar o conector por organização.

### 8.8 Qualidade e melhoria contínua
- Registrar, por campo, se o humano **aceitou** ou **corrigiu** a sugestão. Métrica principal: taxa de correção por campo e por modelo/versão de prompt.
- Conjunto de **casos de teste de e-mails** (anonimizados), incluindo casos adversariais (prompt injection, e-mails vazios, anexos corrompidos, múltiplos pedidos no mesmo e-mail, resposta/encaminhamento, idioma misto) para regressão ao trocar prompt/modelo.
- Reprocessamento possível a partir do .eml original quando o prompt/modelo melhorar.

### 8.9 Entrega em etapas (conector)
1. **Etapa A — Ingestão e armazenamento:** leitura da caixa, armazenamento do .eml e anexos, deduplicação por Message-ID, criação de solicitação com **vínculo manual** (sem IA).
2. **Etapa B — Extração assistida:** pipeline com varredura, extração por schema, tela de conferência com confiança e trecho de origem, métricas de correção.
3. **Etapa C — Agrupamento e automações:** agrupamento por thread/referência, sinalização de duplicatas, regras de descarte conservadoras, (futuro) leitura de e-mails enviados para registrar envio da proposta.

---

## 9. Requisitos transversais

### 9.1 Autenticação
Manter ASP.NET Core Identity com cookie HttpOnly e proteção contra falsificação de requisição.

### 9.2 Autorização e permissões
- Perfis-base atuais: Administrador, Gestor, Operador.
- Definir **permissões granulares** para o módulo (ex.: `quote.view`, `quote.create`, `quote.edit`, `quote.transition`, `quote.approve`, `quote.close`, `quote.viewCost`, `quote.manageConnector`, `quote.manageReasons`) atribuíveis aos papéis, mesmo que inicialmente mapeadas de forma fixa. Isso evita retrabalho quando permissões forem configuráveis por empresa.
- Autorização sempre validada no backend.

### 9.3 Isolamento entre organizações
Conforme 6.3. Testes obrigatórios de acesso cruzado em **todos** os endpoints novos (ler, editar, listar, aprovar, baixar anexo).

### 9.4 Auditoria
- Mecanismo **geral** (não por módulo) de histórico: quem, quando, o quê, valor anterior e novo.
- Registros de auditoria **append-only**; sem edição/exclusão via aplicação.
- Cobrir: transições, aprovações, mudança de preço/margem, concessão de alçada, ações do conector, decisões sobre sugestões da IA.

### 9.5 Anexos e evidências
- Storage de objetos com chaves segregadas por organização, acesso **somente via backend** (URLs assinadas de curta duração, se usadas).
- Varredura de malware, limites de tamanho e tipos, nomes sanitizados, metadados (quem, quando, origem).
- Anexo associado a entidade (solicitação, revisão, visita, aprovação, e-mail).

### 9.6 Notificações
Mecanismo simples e transversal (in-app e e-mail) para follow-up, prazos, atribuição, falhas do conector e fila de conferência. Preferências de notificação por usuário em etapa posterior.

### 9.7 Observabilidade e operação
- Logs estruturados com correlação, sem dados sensíveis.
- Métricas: fila do conector, falhas, latência/custo de IA, e-mails aguardando conferência.
- Health checks, alertas para DLQ crescendo e credenciais de caixa expiradas.
- Estratégia de migração segura para homologação/produção, backups e ambiente de homologação antes do primeiro cliente real.

### 9.8 LGPD
Base legal, retenção, exportação e exclusão por organização para dados pessoais (CPF/CNPJ, contatos, e-mails, anexos). Registro de operadores/suboperadores (provedor de IA, storage).

---

## 10. Testes e critérios de aceite

### 10.1 Testes obrigatórios
- **Domínio:** transições válidas/inválidas da máquina de estados; cálculo de preço/BDI/margem e arredondamento; imutabilidade de revisão enviada; regra de alçada.
- **Integração (API + banco):**
  - fluxo completo: criar solicitação → avaliar → elaborar revisão → enviar → negociar → aprovar → rascunho → conferência → demanda confirmada;
  - encerramentos (`NaoCotada`, `Recusada`, `Expirada`, `Cancelada`) com motivo obrigatório;
  - aprovação **idempotente** (repetição e **concorrência** simultânea geram um único rascunho);
  - itens aprovados preservados como subitens snapshot com referência de origem;
  - validade e job de expiração;
  - permissões por perfil (Administrador, Gestor, Operador) em cada ação;
  - **isolamento entre organizações** em todos os endpoints;
  - ocultação de custo/margem para quem não tem permissão.
- **Conector:**
  - idempotência por Message-ID; reprocessamento sem duplicar;
  - agrupamento por thread e sinalização de duplicatas;
  - rejeição de JSON fora do schema;
  - **suíte adversarial** de prompt injection (instruções no corpo, em anexo PDF, texto invisível, tentativa de forçar aprovação/estado/cliente);
  - anexo malicioso/corrompido/gigante;
  - falha do provedor de IA, timeout, retry, DLQ;
  - garantia de que nenhuma ação da IA altera estado além de criar `QuoteRequest` em `Recebida`.
- **Arquitetura:** teste de dependência entre módulos; teste de que toda entidade de tenant possui filtro.
- **Frontend:** testes dos componentes críticos; inspeção visual e de interação autenticada ponta a ponta das telas de solicitações, revisões, rascunhos e conferência de e-mail; estados de erro/vazio; mobile.
- **Desempenho:** bundle inicial ≤ 500 kB com lazy loading; paginação nas listas e no Kanban.

### 10.2 Critérios de conclusão do módulo
- Todas as Fases 1–5 da seção 12 concluídas com testes passando.
- Nenhuma ação do conector avança estado sem humano.
- Isolamento entre organizações comprovado por teste em todos os endpoints do módulo.
- Aprovação concorrente comprovadamente gera um único rascunho.
- Build backend sem avisos; build Angular dentro do limite configurado.
- Relatório de aderência do legado concluído e adequações de segurança registradas.
- Documentação (este arquivo e o 08) atualizada com o que foi decidido/alterado.

---

## 11. Decisões em aberto (a IA deve manter esta lista atualizada)

1. Renomear a entidade atual de "proposta" para "solicitação" no código, ou manter e mapear?
2. Padrão de identificação das revisões (letras A/B/C ou números)?
3. Operador pode ver custo/margem? E o subitem da demanda carrega custo?
4. Limites de alçada (desconto/margem mínima): valores padrão e se são por organização ou por usuário/perfil.
5. Reabertura de solicitação encerrada: permitida? por quem?
6. Evidência de aprovação: obrigatória sempre, ou configurável por organização?
7. Visita técnica obrigatória para algum tipo de serviço?
8. Regra de expiração automática quando há negociação ativa.
9. Provedor/modelo padrão e política de retenção de dados no OpenRouter para o conector; uso de OCR vs modelo com visão.
10. Provedor de e-mail principal dos clientes (Microsoft 365, Google Workspace, outro) e se IMAP será exceção aceita.
11. Armazenamento de objetos (provedor, região, criptografia, retenção).
12. Mecanismo de fila (ex.: tabela/outbox com worker, ou broker dedicado) compatível com a infraestrutura atual.
13. Vínculo futuro com contrato (como o Comercial referencia preço tabelado).
14. Catálogo de itens e eventual importação SINAPI: entra neste módulo ou depois?
15. Mapeamento de permissões granulares por perfil e se serão configuráveis por organização.

---

## 12. Plano de implementação em fases

Em 22/11/2026, o usuário confirmou concluir a validação da T26 antes de implementar as capacidades adicionais e detalhar as regras conforme cada etapa. A T26 preserva seus critérios aprovados em [spec.md](../.specs/features/propostas/spec.md). As tarefas e evidências do recorte atual estão em [tasks.md](../.specs/features/propostas/tasks.md) e [verification.md](../.specs/features/propostas/verification.md).

| Ordem | Resultado e dependências | Critério de saída e verificação |
| --- | --- | --- |
| 0 | Auditoria do módulo e do legado, disponível na seção 14 | Registrar diferenças, reutilização possível e decisões pendentes; conferir fontes no código. |
| 1 | Concluir T26 e validar isolamento e fluxo comercial existente | Executar critérios PROP-01–PROP-18 com testes e inspeção autenticada; registrar pendências por critério. O usuário autorizou adiar a confirmação visual do rascunho como Operador e a inspeção touch/mobile para o fechamento integrado do Comercial. O lazy loading e o orçamento inicial têm evidência em verification.md. |
| 2 | Especificar e implementar acompanhamento comercial, com responsáveis, prazos, histórico, visita e motivos estruturados | Definir regras necessárias de responsabilidade, transições e configuração por organização; testar fluxo e isolamento; inspecionar interface. Anexos dependem de desenho próprio de armazenamento e acesso. |
| 3 | Especificar e implementar composição e revisão de preços, aprovação interna e evidências | Definir fórmula, arredondamento, alçadas, permissões e exigência de evidência; testar cálculos, imutabilidade, controle de acesso e conversão. |
| 4 | Especificar e implementar ingestão de e-mail em A, extração assistida em B e agrupamento em C | Definir provedor, retenção, armazenamento, fila e limites. Em cada etapa, testar isolamento, idempotência, falhas e conferência humana. |
| 5 | Entregar indicadores comerciais e de qualidade do conector após as fontes de dados correspondentes | Definir fórmulas e permissões; verificar resultados calculados e isolamento com dados controlados. |

Cada fase produz especificação e tarefas com critérios verificáveis quando seus detalhes estiverem definidos. As decisões transversais de isolamento, auditoria e anexos devem ser desenhadas antes da implementação que delas dependa, com registro técnico apropriado. O produto existente e a migração dos dados devem continuar funcionais durante a evolução.

### Fase 0 — Auditoria (sem codificar funcionalidade nova)
- Mapear o código atual do módulo de propostas/rascunhos e comparar com esta especificação.
- Auditar o projeto legado (seção 8.2) e produzir o relatório de aderência.
- Registrar divergências e decisões em aberto; solicitar decisão ao responsável nos itens bloqueantes.

### Fase 1 — Fechamento do que já existe
- Testes integrados do fluxo atual, concorrência da aprovação, isolamento entre organizações, permissões por perfil.
- Máquina de estados explícita e histórico de transições.
- Lazy loading e correção do bundle > 500 kB.
- Inspeção visual/interação autenticada das telas existentes.
- Verificar que filtros globais de tenant cobrem o módulo.

### Fase 2 — Operação comercial
- Responsável, prazos, validade, follow-up, número da solicitação, referência do cliente, canal de origem.
- Histórico de atividades e comentários; visita técnica.
- Motivos estruturados (não cotada/perda).
- Anexos (infraestrutura transversal básica) e auditoria geral, se ainda não existirem.

### Fase 3 — Composição de preço e revisões
- Itens, BDI/margem, cálculo determinístico, imutabilidade de enviada, comparação de revisões, alçada de aprovação interna, ocultação de custo.
- Evidência de aprovação; refinamento da conversão em subitens (snapshot com origem).

### Fase 4 — Conector de e-mail
- Etapa A, depois B, depois C (seção 8.9), sempre com a suíte de segurança (8.7 e 10.1).

### Fase 5 — Indicadores
- Painel do funil e métricas de qualidade da IA.

> Cada fase termina com: testes passando, documentação atualizada, divergências registradas e demonstração local autenticada.

---

## 13. Evoluções futuras (fora desta versão)
- Leitura de e-mails **enviados** para registrar automaticamente o envio da proposta.
- Geração de documento/PDF da proposta no sistema.
- Assinatura eletrônica e portal do cliente.
- Catálogo avançado e importação de tabelas de preço.
- Integração com WhatsApp.
- Contratos e aditivos (com decisão própria).

---

## 14. Registro de divergências (preenchido pela IA durante a implementação)

| Data | Item | Documento diz | Código/realidade | Decisão/ação | Responsável |
|---|---|---|---|---|---|
| | | | | | |
| 22/11/2026 | Escopo | E-mail com IA, custos e aprovação interna integram a conclusão | A T26 aprovada exclui integração de e-mail e aprovação interna e prevê preço por item | Usuário confirmou a ampliação por etapas após validação da T26; decisões de produto permanecem pendentes | Usuário |
| 22/11/2026 | Histórico | Transições com estado anterior e novo estruturados | Histórico comercial registra atividades textuais | Detalhar persistência e migração preservando histórico anterior | Project IT |
| 22/11/2026 | Isolamento | Filtros globais por organização | Consultas atuais usam filtros explícitos; não há filtros globais | Avaliar arquitetura e cobertura de consultas e escritas antes da adoção | Project IT |
| 22/11/2026 | Identificação | Solicitação chamada QuoteRequest | Entidade comercial chama-se CommercialRequest, revisões usam números | Preservar nomes existentes e mapear terminologia | Project IT |

### Auditoria preliminar do legado

**Relatório da Fase 0 — 22/11/2026.** Auditoria feita por leitura do código, configuração por nomes de campos/opções e testes existentes. O repositório legado permaneceu somente leitura. Arquivos `.env`, logs e valores de segredos não foram abertos nem copiados.

#### Módulo Comercial atual do PIT Obras

| Área | Estado observado no código | Consequência para as próximas fases |
|---|---|---|
| Modelo e nomes | A entidade central é `CommercialRequest`, revisões são inteiras (`1, 2, 3`) e itens guardam descrição/valor. Há rascunho com vínculos à solicitação e revisão aprovadas. | Manter um mapeamento explícito para a terminologia da especificação. Rebatizar entidades não é necessário para implementar a fase seguinte. Unidade, quantidade, custo, margem e BDI não existem ainda e pertencem à Fase 3. |
| Etapas e histórico | `CommercialRequest` centraliza as transições permitidas no domínio. `CommercialActivity` guarda ator, horário, tipo e texto, mas não campos estruturados de estado anterior/novo. O motivo de resultado é texto livre; apenas “não cotada” exige motivo no domínio atual. | A Fase 2 precisa preservar atividades legadas e projetar histórico de transição tipado antes de migrar dados. Motivos estruturados e regras para os outros encerramentos permanecem decisão de produto. |
| Acompanhamento | A solicitação atual não tem número legível, responsável comercial, local, referência do cliente, prazos, próxima ação ou origem e-mail/manual separada. A busca atual usa etapa, cliente, período de atualização e texto. | Campos, número e filtros da Fase 2 requerem desenho e migração compatível com registros existentes. |
| Segurança multiempresa | Não foi encontrado `HasQueryFilter` no EF Core. Os repositórios comerciais e de rascunho recebem `OrganizationId` e filtram explicitamente as consultas; relacionamentos do banco incluem chaves compostas com organização e a suíte PostgreSQL testa acessos cruzados. | A exigência documental de filtro global diverge da arquitetura atual. Não trocar o padrão sem análise de efeitos em todo o sistema; manter verificações negativas e avaliar uma segunda camada antes de anexos/e-mail. |
| Funcionalidades ainda ausentes | Não há visita técnica comercial, anexo comercial, comentário/atividade editável, lista configurável de motivos, job de expiração ou lembrete comercial. | Implementar somente depois de resolver desenho, autorização e infraestrutura das fases correspondentes. |
| Frontend e validação da T26 | Propostas e rascunhos têm rotas sob demanda. Admin/Gestor foram inspecionados; o usuário adiou para o fechamento do módulo a confirmação visual como Operador e inspeção touch/mobile. Backend passou 8/8, Angular 34/34 e o build inicial Angular está em 416,31 kB. | Considerar os critérios T26 exercitados no nível registrado; retomar aquelas duas inspeções no fechamento integrado do Comercial. |

#### Aplicação legada de e-mail

O legado usa ASP.NET Core/.NET 10, EF Core e PostgreSQL. A integração observada é IMAP via MailKit: uma rotina consulta periodicamente a caixa de entrada por organização e também há sincronização manual. As mensagens são identificadas por `Message-ID` (com UID IMAP como fallback); índices únicos por organização reduzem duplicidades. Há hash SHA-256 de anexos e mecanismos de reprocessamento com status, tentativas e eventos. Uma fila de sincronização usa canal em memória limitado e os trabalhos são persistidos no banco para recuperação.

As credenciais da caixa são guardadas protegidas via ASP.NET Data Protection, com opção legada de configuração. A aplicação persiste metadados e corpo da mensagem, e guarda anexos no sistema de arquivos configurado. A rotina de extração encontrada processa planilhas XLS/XLSX com ClosedXML/ExcelDataReader e chama OpenRouter; modelo e endpoint são configuráveis. O prompt orienta extrair somente fatos explícitos e a resposta usa JSON Schema estrito, com validação adicional na leitura. Há limites configuráveis de tamanho de arquivo e texto de entrada.

Não foi encontrada integração OAuth, nem preservação do e-mail original bruto `.eml`, varredura antivírus, deduplicação/agrupamento por thread ou uma defesa específica contra prompt injection. A análise do arquivo ocorre após o download para memória; o limite aplicado no storage precisa ser complementado por limite antes/durante a transferência e leitura. A proteção/backup do key ring do Data Protection, retenção e descarte de dados, região de armazenamento, LGPD, fila durável de falhas, monitoramento de custos e mitigação de abuso do serviço de IA não ficaram comprovados. O legado contém código e testes que podem informar prompts, mapeamento de campos e casos de planilhas, mas sua arquitetura e regras de criação não devem ser copiadas diretamente.

#### Encaminhamento da auditoria

1. Preservar `CommercialRequest` e as revisões atuais, com terminologia mapeada; não renomear tabelas na Fase 2.
2. Manter isolamento explícito por `OrganizationId` enquanto não houver decisão arquitetural sobre filtros globais; exigir cobertura negativa de tenant em cada novo caso de uso.
3. Resolver as decisões AC-T01 sobre identificação, responsável, referências/canais e local de prospect antes da migração e dos endpoints da Fase 2.
4. Para o e-mail, reaproveitar conhecimento de extração e testes do legado; reescrever integração na arquitetura do PIT Obras. OAuth, armazenamento bruto, limites prévios, segurança de anexos, retenção e defesa contra conteúdo malicioso são pré-requisitos de desenho da Fase 4.
