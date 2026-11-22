# Direção do produto e escopo do MVP

## Objetivo

O produto chama-se **PIT Obras** e é desenvolvido pela Project IT. Ele organiza o trabalho de empresas de construção civil e reformas, acompanhando solicitações comerciais e demandas desde a avaliação de orçamento ou abertura operacional até a execução, o faturamento e o recebimento.

O posicionamento do produto é específico para empresas de construção civil e reformas. A plataforma não começa como um ERP genérico.

## Público inicial

Empresa piloto com serviços principalmente de civil e ar-condicionado, contratos fixos de manutenção preventiva/corretiva e trabalhos corretivos pontuais (spots). O trabalho ocorre em locais de clientes e pode envolver técnicos internos ou prestadores.

## Princípios

- Uma demanda recebe um identificador interno próprio.
- O trabalho pode nascer de uma demanda criada manualmente para cliente existente ou de uma proposta aprovada, que origina uma demanda em rascunho para validação operacional.
- Propostas e demandas são registros distintos: a proposta registra a negociação comercial; a demanda acompanha a execução aprovada.
- Uma proposta aprovada gera uma demanda com subitens que representam o escopo acordado; a aprovação não libera execução até a demanda ser completada e confirmada.
- Chamado/ticket, ordem de compra e outras referências externas são pesquisáveis e vinculadas ao cliente e ao local; o chamado pode depender do cliente.
- Cliente, contato e local são registros distintos: um cliente pode ter várias demandas e vários locais.
- Aprovação de orçamento e autorização para emitir nota fiscal são momentos diferentes.
- Aditivo de orçamento e adiantamento financeiro são conceitos diferentes.
- O histórico do serviço, das alterações, das evidências e do faturamento precisa permanecer consultável.
- A separação entre empresas deve ser garantida no backend; dados nunca dependem apenas de filtros de interface.
- O acesso deve aceitar permissões específicas por perfil; a equipe operacional pode criar e alterar demandas, e outros perfis recebem apenas as ações necessárias.
- O usuário aprovou trazer o cálculo LL para o PIT Obras. O custo será informado manualmente; para a empresa piloto, imposto/administração é 28% e a meta mínima de LL é 20%. Parâmetros devem ser configuráveis por organização.

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
- O cadastro do cliente inclui endereço principal opcional: CEP, endereço, bairro, cidade e estado (D09), com consulta assistida por CEP. Esse endereço não substitui os locais de atendimento, que permanecem associados ao cliente.
- Contatos possuem nome, cargo/função, telefone e e-mail, associados ao cliente ou a um local dele. Local de atendimento possui nome, endereço, bairro, cidade e status Ativo, Pausado ou Concluído.
- CPF/CNPJ deve ser numérico, válido e armazenado sem pontuação. CNPJ alfanumérico fica rejeitado conforme decisão do usuário em 14/11/2026.
- Referências e instruções específicas por cliente/local.
- Histórico consolidado das demandas e faturamento.

### Demandas e execução

- Registrar solicitações de orçamento para cliente existente ou prospect e avaliar se a empresa prepara uma proposta; manter o motivo quando não cotar.
- Criar propostas com itens de descrição/valor e revisões sucessivas, registrando manualmente envio e aprovação do cliente. Administrador e Gestor gerenciam propostas; envio e documentos ocorrem fora do sistema no primeiro recorte.
- Uma proposta aprovada origina automaticamente um rascunho de demanda. Administrador/Gestor criam ou vinculam cliente/local quando se tratar de prospect, preservando D01; Operador completa os dados operacionais restantes e confirma a demanda.
- Manter a criação manual de demanda para cliente existente. Na demanda operacional confirmada, cliente, local, descrição e origem são obrigatórios; o responsável operacional interno é opcional e pode ser atribuído depois (D02).
- Itens da versão aprovada são copiados como subitens da demanda, mantendo a revisão de origem e os valores aprovados. Demandas manuais também podem ter subitens opcionais.
- Exibir o fluxo comercial em Kanban e lista; cartão representa solicitação/oportunidade e suas revisões ficam no detalhe.
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
- Cálculo LL dentro do PIT Obras: custo estimado digitado manualmente por itens; imposto/administração de 28% para a empresa piloto; LL em valor e percentual, com meta mínima de 20% do valor aprovado.
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
- Campanhas de marketing, automação/leitura de e-mails, envio automático de proposta, portal de cliente, assinatura eletrônica e geração de PDF comercial.
- Gestão de contratos guarda-chuva; demandas podem ganhar vínculo contratual quando esse módulo for especificado.
