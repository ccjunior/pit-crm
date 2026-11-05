# Direção do produto e escopo do MVP

## Objetivo

Construir uma plataforma para organizar o trabalho de empresas de serviços, começando por serviços de campo, manutenção e reformas empresariais. O produto deve acompanhar uma demanda desde a entrada até a execução, faturamento e recebimento.

A estratégia é focar o primeiro produto nesse tipo de operação e manter os conceitos comuns configuráveis para outros segmentos de serviços. A plataforma não começa como um ERP genérico.

## Público inicial

Empresa piloto com serviços principalmente de civil e ar-condicionado, contratos fixos de manutenção preventiva/corretiva e trabalhos corretivos pontuais (spots). O trabalho ocorre em locais de clientes e pode envolver técnicos internos ou prestadores.

## Princípios

- Uma demanda recebe um identificador interno próprio.
- Chamado/ticket, ordem de compra e outras referências externas são pesquisáveis e vinculadas ao cliente e ao local; o chamado pode depender do cliente.
- Cliente, contato e local são registros distintos: um cliente pode ter várias demandas e vários locais.
- Aprovação de orçamento e autorização para emitir nota fiscal são momentos diferentes.
- Aditivo de orçamento e adiantamento financeiro são conceitos diferentes.
- O histórico do serviço, das alterações, das evidências e do faturamento precisa permanecer consultável.
- A separação entre empresas deve ser garantida no backend; dados nunca dependem apenas de filtros de interface.
- O acesso deve aceitar permissões específicas por perfil; a equipe operacional pode criar e alterar demandas, e outros perfis recebem apenas as ações necessárias.
- O usuário aprovou trazer o cálculo LL para o PIT-CRM. O custo será informado manualmente; para a empresa piloto, imposto/administração é 28% e a meta mínima de LL é 20%. Parâmetros devem ser configuráveis por organização.

## Escopo proposto para o MVP

### Usuários e permissões

- Perfis iniciais: **Administrador**, **Gestor** e **Operador**, com permissões ajustáveis por organização/usuário.
- **Administrador:** acesso completo, incluindo usuários, permissões e configurações da organização.
- **Gestor:** acompanha e altera a operação, consulta indicadores e dados financeiros e pode aprovar decisões conforme a configuração.
- **Operador:** cria e altera demandas, agenda visitas/execuções e registra atualizações e evidências. Toda a equipe operacional precisa poder criar e alterar demandas.
- **Financeiro** pode ser oferecido como perfil opcional para custos, cálculo LL, notas e recebimentos, sem edição do andamento operacional, caso exista essa divisão na equipe.
- **Prestador** pode ser um perfil futuro, limitado às demandas atribuídas, para atualizar execução e enviar evidências, sem acesso a dados financeiros ou a outros clientes.
- Acesso de **cliente** para acompanhar demandas ou autorizar serviços fica fora do MVP e será considerado se houver necessidade confirmada.

### Gestão de clientes e locais

Detalhamento confirmado pelo usuário em 04/11/2026 para a primeira entrega: **Administrador e Gestor** podem cadastrar e editar clientes e locais; **Operador** consulta esses cadastros e mantém o direito de criar/editar demandas. A organização e seu primeiro administrador serão criados de forma controlada; o administrador cadastra a equipe. O registro das respostas está no [contexto da primeira entrega](../.specs/features/primeira-entrega-operacional/context.md).

- Cadastro de clientes, contatos e locais de atendimento.
- Referências e instruções específicas por cliente/local.
- Histórico consolidado das demandas e faturamento.

### Demandas e execução

- Cadastro de demanda com origem (WhatsApp, ligação, e-mail ou outra), descrição, cliente, local, ticket/OC opcional, responsável e prioridade.
- Na primeira entrega, cliente, local, descrição e origem são obrigatórios na abertura; o responsável operacional interno é opcional e pode ser atribuído depois, conforme confirmação do usuário em 04/11/2026.
- Orçamento associado à demanda, aprovação do cliente e histórico de retificações/aditivos.
- Programação de visita e/ou execução, com técnico/prestador atribuído.
- Acompanhamento de status, notas internas e motivo de pausa/cancelamento.
- Registro de fotos antes/durante/depois, vídeos e relatórios técnicos.
- Registro de conclusão e envio de evidências ao cliente.

### Faturamento e recebimentos

- Autorização do cliente para emissão da nota após a execução.
- Número e data de emissão da NF, data de envio, valor, previsão e data efetiva de recebimento.
- Adiantamentos recebidos e pagamentos antecipados a prestadores, identificados como adiantamentos e ligados à demanda.
- Alertas/listas de recebimentos vencidos e histórico faturado por período.

### Custos da operação

- Valor fechado de prestador, normalmente como pacote total.
- Despesas de material ligadas à demanda.
- Cálculo LL dentro do PIT-CRM: custo estimado digitado manualmente por itens; imposto/administração de 28% para a empresa piloto; LL em valor e percentual, com meta mínima de 20% do valor aprovado.
- Confirmar os nomes/categorias dos itens do custo. Não presumir que sejam os mesmos componentes do pacote contratado de prestador.
- Não incluir no MVP uma contabilidade completa nem presumir que o pacote de prestador precisa ser decomposto em mão de obra, deslocamento e material.

### Painel operacional

- Demandas por etapa e responsável.
- Serviços executados aguardando autorização de faturamento ou emissão de NF.
- Notas emitidas aguardando recebimento e recebimentos vencidos.
- Histórico de faturamento por período.

## Fora do primeiro MVP

- Agendamento público para clientes marcarem horários, como agenda de consultas. O MVP precisa de programação interna de visitas e equipes em campo.
- Integrações automáticas com WhatsApp, e-mail ou sistemas dos clientes. Inicialmente, a origem pode ser registrada manualmente.
- IA, automações avançadas, folha de pagamento, estoque completo, contabilidade ou ERP genérico.
- Contabilidade completa ou regras financeiras genéricas para todos os segmentos. O cálculo LL entra no MVP para a empresa piloto, com parâmetros configuráveis por organização.
- Funis completos de prospecção e marketing; o MVP pode registrar origem e orçamento da demanda, com gestão comercial mais ampla em fase posterior.
