# Primeira entrega operacional — especificação

**Estado:** escopo aprovado para implementação; detalhes explicitamente marcados como proposta continuam sujeitos à revisão quando forem necessários.
**Data:** 04/11/2026; aprovação do escopo em 05/11/2026.
**Autorização:** elaboração da especificação e aprovação do escopo como primeira entrega, conforme [context.md](context.md). A autorização não converte os detalhamentos propostos em regras confirmadas do piloto.
**Approved**: 2026-11-05 - user: "aprovo"

## Problema e resultado

A operação precisa registrar demandas e encontrá-las com seu cliente, local e responsável, dentro da própria organização. A primeira entrega deve permitir demonstrar esse caminho completo, com autenticação e persistência, antes de acrescentar orçamento, execução e faturamento.

Resultado demonstrável: uma organização é preparada, seu administrador acessa o sistema, cadastra a equipe e os cadastros necessários; um Operador abre uma demanda e consegue localizá-la e atualizar seus dados básicos após entrar novamente.

## Fontes e natureza das definições

- [Direção e escopo](../../../docs/01-DIRECAO-E-ESCOPO.md): capacidades do MVP e perfis-base.
- [Fluxo e modelo](../../../docs/02-FLUXO-E-MODELO.md): conceitos e regras operacionais.
- [Roadmap](../../../docs/03-ROADMAP-E-PENDENCIAS.md): recorte e pendências das próximas entregas.
- [Arquitetura](../../../docs/04-ARQUITETURA-TECNICA.md): stack, autenticação e isolamento.
- [Contexto](context.md): autorização deste trabalho e respostas literais D01–D03.

Os critérios abaixo detalham uma proposta de entrega. Regras preexistentes e D01–D03 são vinculantes. Campos adicionais, limites, códigos de resposta e comportamentos de interface são detalhamentos propostos para revisão, sem atribuí-los ao piloto como decisões já confirmadas.

## Escopo

- Provisionamento controlado de organização e primeiro administrador.
- Entrada, sessão, saída e credencial inicial da equipe.
- Cadastro e alteração de usuários da organização pelo Administrador, com atribuição dos três perfis-base.
- Cadastro, alteração e consulta de clientes e locais, conforme D01.
- Abertura, alteração de dados básicos, detalhe, listagem e busca de demandas.
- Autorização no backend, isolamento por organização e registro das alterações das demandas.

## Recorte das próximas entregas

| Capacidade | Tratamento nesta entrega |
| --- | --- |
| Contatos independentes do cliente | Preservar o conceito no modelo do produto; cadastro específico em entrega posterior |
| Orçamentos, visitas, execução, evidências e equipe/prestador | Preservar a evolução prevista no roadmap; esta entrega registra a demanda antes dessas etapas |
| NF, recebimentos, adiantamentos, custos e LL | Entrega financeira posterior, com as regras próprias |
| Configuração avançada de permissões por usuário/perfil | Aplicar a matriz inicial abaixo; painel de configuração granular será detalhado em entrega posterior |
| Importação de histórico, integrações e portal de cliente | Entregas posteriores conforme o roadmap |
| Criação pública de organização, contratação e troca de organização na interface | Fora deste primeiro recorte; aplicar D03 e um contexto de organização por sessão |

## Matriz inicial de acesso

| Ação | Administrador | Gestor | Operador | Base |
| --- | --- | --- | --- | --- |
| Consultar clientes e locais | Sim | Sim | Sim | D01 |
| Cadastrar/editar clientes e locais | Sim | Sim | Não | D01 |
| Consultar/criar/editar demandas | Sim | Sim | Sim | Escopo e fluxo da operação |
| Cadastrar equipe e atribuir perfil-base | Sim | Não | Não | D03 e escopo |
| Alterar credencial própria | Sim | Sim | Sim | Proposta de acesso seguro da entrega |

As restrições valem para todos os dados da organização, inclusive acesso direto pela API. A matriz completa de aprovações e dados financeiros continua pendente no roadmap.

## Dados mínimos e validações propostas

Os limites de texto são limites técnicos propostos. Remover espaços externos antes de validar campos de texto; campos obrigatórios com apenas espaços são inválidos. Preserve o conteúdo textual, apresentando-o como texto na interface.

| Registro | Campo | Obrigatoriedade / regra |
| --- | --- | --- |
| Usuário da equipe | Nome, e-mail e perfil-base | Obrigatórios; nome até 200 caracteres; perfil entre Administrador, Gestor e Operador |
| Cliente | Nome de identificação | Obrigatório; 1 a 200 caracteres |
| Cliente | Documento e instruções | Opcionais, decisão D04; documento até 30 e instruções até 2.000 caracteres; não criar uma regra fiscal ou deduplicação por nome |
| Local | Cliente e nome de identificação | Obrigatórios; nome de 1 a 200 caracteres; pertence ao cliente indicado |
| Local | Endereço | Obrigatório, decisão D05; até 500 caracteres |
| Local | Instruções | Opcional; até 2.000 caracteres |
| Demanda | Cliente, local, descrição e origem | Obrigatórios conforme D02; descrição de 1 a 4.000 caracteres |
| Demanda | Origem | WhatsApp, ligação, e-mail ou outra, conforme o escopo existente |
| Demanda | Responsável operacional interno | Opcional; quando informado, deve ser usuário ativo da mesma organização |
| Demanda | Chamado/ticket e ordem de compra | Opcionais e pesquisáveis; até 100 caracteres cada; não são a chave interna nem globalmente únicos |
| Demanda | Prioridade | Opcional; valores fixos Baixa, Média ou Alta, decisão D06 |

Os IDs, organização, datas e autoria são gerados ou resolvidos no backend. O campo responsável não representa a equipe/prestador da execução. Uma futura alteração de pertencimento de um local entre clientes exige desenho próprio; nesta entrega, a edição do local altera seus dados descritivos.

## Critérios de aceite

Cada ID identifica um resultado que deverá ser demonstrado na implementação. As evidências planejadas estão em [tasks.md](tasks.md).

### Acesso e equipe

- **PEO-01:** Quando o provisionamento controlado for executado com dados válidos, o sistema deverá criar a organização e seu primeiro Administrador vinculados; repetir o mesmo provisionamento não deverá criar cópias nem trocar silenciosamente a credencial existente.
- **PEO-02:** Quando um usuário ativo informar credenciais válidas, o sistema deverá iniciar a sessão da sua organização e mostrar a identificação da organização e do usuário.
- **PEO-03:** Se as credenciais forem inválidas, o sistema deverá recusar a entrada com mensagem genérica, sem indicar se o e-mail pertence a outra organização.
- **PEO-04:** Quando o usuário sair, a sessão utilizada deverá perder acesso às operações protegidas.
- **PEO-05:** Quando o Administrador cadastrar um usuário da equipe e atribuir um perfil-base, a conta deverá pertencer à sua organização e receber apenas as ações correspondentes à matriz inicial. Gestor e Operador não poderão executar esse cadastro.
- **PEO-06:** Quando uma conta da equipe usar a credencial inicial, deverá concluir a troca da própria senha antes de acessar os cadastros operacionais. O Administrador define a senha temporária e a entrega fora da plataforma; não há envio automático de mensagens (D08).
- **PEO-07:** Se uma alteração removeria o último Administrador ativo da organização, o sistema deverá recusá-la, preservando uma conta capaz de administrar a equipe.

### Clientes e locais

- **PEO-08:** Quando Administrador ou Gestor salvar um cliente válido, o sistema deverá persistir o registro e permitir consultá-lo após recarregar a página; o Operador deverá receber recusa ao tentar a mesma gravação pela API.
- **PEO-09:** Quando Administrador ou Gestor alterar os dados de um cliente existente, o sistema deverá preservar seu ID e seus vínculos com locais e demandas.
- **PEO-10:** Quando o usuário consultar clientes, o sistema deverá listar somente os da organização da sessão e permitir busca por nome e documento informado.
- **PEO-11:** Quando Administrador ou Gestor salvar ou editar um local válido, o sistema deverá mantê-lo vinculado ao cliente selecionado da mesma organização; o Operador poderá somente consultar.
- **PEO-12:** Quando o usuário escolher um cliente no formulário de demanda, o sistema deverá oferecer somente seus locais; ao trocar o cliente, deverá limpar uma seleção de local que deixou de ser válida.

### Demandas

- **PEO-13:** Quando um dos três perfis abrir uma demanda com cliente, local, descrição e origem válidos, o sistema deverá persistir o registro mesmo sem responsável, chamado ou ordem de compra.
- **PEO-14:** Quando uma demanda for criada, o sistema deverá atribuir um ID interno próprio; demandas poderão compartilhar referências externas sem que isso confunda suas identidades.
- **PEO-15:** Se um campo obrigatório estiver ausente, exceder seu limite ou contiver valor inválido, o sistema deverá recusar a gravação e identificar o campo correspondente, preservando os dados digitados no formulário.
- **PEO-16:** Se o local informado não pertencer ao cliente da demanda, o sistema deverá recusar a gravação; se o responsável for informado, deverá existir e estar ativo na mesma organização.
- **PEO-17:** Quando o usuário buscar demandas por ID interno, chamado/OC ou trecho da descrição, o sistema deverá retornar somente registros correspondentes da organização, com paginação e filtros por cliente, local e responsável.
- **PEO-18:** Quando abrir o detalhe de uma demanda, o usuário deverá encontrar seus dados salvos e a identificação de cliente, local e responsável, incluindo a indicação de responsável ainda não atribuído.
- **PEO-19:** Quando um dos três perfis alterar os dados básicos válidos de uma demanda, o sistema deverá preservar seu ID e registrar a alteração. Toda demanda começa em “Recebida” (D07); transições de orçamento/execução pertencem às próximas entregas.
- **PEO-20:** Se uma gravação usar uma versão de demanda anterior à já salva, o sistema deverá recusar a substituição silenciosa e orientar o usuário a recarregar os dados atuais.

### Isolamento, falhas e interface

- **PEO-21:** Quando uma sessão tentar consultar, alterar ou vincular dados de outra organização, o sistema deverá impedir o acesso e a gravação, inclusive por ID direto, busca, campos manipulados e totais das listas.
- **PEO-22:** Quando uma demanda for criada ou alterada, seus dados, referências e registro de atividade deverão ser confirmados juntos; uma falha nessa gravação não deverá deixar uma parte persistida.
- **PEO-23:** Quando o formulário estiver enviando uma gravação, deverá impedir novo envio simultâneo pela mesma ação. Se a resposta de rede não puder ser confirmada, deverá preservar o preenchimento e informar a necessidade de conferir a lista antes de tentar novamente, sem reenviar automaticamente.
- **PEO-24:** Quando uma tela de lista carregar, ficar vazia, falhar ou receber acesso negado, deverá apresentar um estado identificável e a ação disponível para o usuário, sem apresentar uma falha de comunicação como lista vazia.
- **PEO-25:** Se uma operação protegida for chamada sem sessão, sem permissão ou sem a proteção de escrita exigida pela arquitetura, a API deverá recusá-la; a interface deverá tratar a recusa sem divulgar dados protegidos.
- **PEO-26:** Quando o usuário percorrer os formulários com teclado em viewports de 390 e 1.280 pixels de largura, deverá conseguir identificar rótulos, foco, erros e ações; listas longas deverão permitir navegação sem ocultar o acesso ao detalhe.

## Exceções e limites da proposta

| Situação | Tratamento proposto |
| --- | --- |
| Cliente com vários locais | Seleção de local filtrada pelo cliente; não impor unicidade de demanda por local |
| Cliente sem chamado ou OC | Abrir normalmente, usando o ID interno |
| Cliente/local com nome igual | Permitir; o ID identifica o registro |
| Solicitação HTTP repetida manualmente | Pode representar nova demanda; esta entrega não deduplica por ticket, descrição ou local |
| Falha de banco | Operação atômica, erro identificável e nenhuma confirmação falsa de sucesso |
| Conflito de edição | Resposta de conflito, sem sobrescrever a versão mais recente |
| Cadastro ainda incompleto | Somente os campos obrigatórios acima impedem a gravação; endereço do local é obrigatório (D05), enquanto o responsável da demanda é opcional |
| Material visual | Nenhuma referência ou identidade visual foi fornecida; os esboços são funcionais e propostos |

## Decisões encerradas e pendências

D01–D08 estão confirmadas em [context.md](context.md). Limites de texto, paginação e outros parâmetros operacionais desta especificação continuam como detalhamentos técnicos propostos; não são regras comerciais adicionais do piloto.

As pendências de Financeiro, categorias de custo, tipos de referência específicos por cliente e recorte da importação histórica permanecem no roadmap e não impedem a elaboração desta entrega. Esta especificação não declara a Etapa 0 inteira concluída.

## Critério de conclusão da futura implementação

Demonstrar os critérios PEO-01 a PEO-26, incluindo um ciclo completo com Administrador, Gestor e Operador e tentativas de acesso cruzado entre duas organizações. Registrar os testes, inspeções de interface e limitações em `verification.md`. Na fase atual, esse arquivo registra apenas a revisão documental.
