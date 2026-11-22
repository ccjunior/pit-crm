# PIT Obras — visão geral para revisão especializada

**Atualizado em 21/11/2026**
**Produto:** PIT Obras
**Empresa responsável:** Project IT

## Resumo do projeto

O PIT Obras é um sistema web para gestão comercial e operacional de empresas de construção civil e reformas. Seu objetivo é reunir em um só lugar o relacionamento com clientes, locais de atendimento, equipe, solicitações de orçamento, propostas e demandas de serviço, mantendo histórico das decisões e do trabalho executado.

O projeto está sendo construído como produto para empresas do ramo, não como reprodução rígida do processo de uma única empresa. Regras específicas podem variar entre organizações; permissões, parâmetros financeiros e isolamento dos dados devem considerar essa configuração. A primeira entrega prioriza a operação diária: organizar clientes e locais, registrar e acompanhar demandas e estruturar a entrada comercial que pode originá-las.

## Como o negócio deve funcionar

Existem dois caminhos para iniciar uma demanda:

1. **Abertura operacional direta:** um usuário registra manualmente uma demanda para um cliente existente, informando cliente, local, descrição e origem. O responsável pela execução pode ser definido depois.
2. **Origem comercial:** um cliente ou prospect solicita um orçamento. A empresa registra a solicitação, decide se vai cotar, elabora uma proposta e suas revisões, e registra o resultado informado pelo cliente. Se a proposta for aprovada, o sistema cria um rascunho de demanda. Esse rascunho só entra na operação depois que as informações necessárias forem conferidas e completadas.

No segundo caminho, os dados aprovados devem continuar rastreáveis até a demanda. Os itens da proposta aprovada podem ser copiados como subitens do serviço. Se o prospect ainda não for cliente, um Administrador ou Gestor cria ou associa o cliente e o local; o Operador pode então completar e confirmar o rascunho. Essa separação preserva a regra de que Operadores não criam clientes ou locais.

Contrato não é obrigatório para criar uma demanda nem para converter uma proposta aprovada no fluxo inicial. A gestão de contratos e o vínculo formal entre contrato e demandas ainda precisam de decisão própria. Mudanças posteriores no escopo contratado devem ser avaliadas como aditivos, mantendo separadas a proposta original, a demanda e as alterações aprovadas.

## Escopo comercial aprovado

O módulo comercial foi planejado para acompanhar uma solicitação de orçamento mesmo que a empresa decida não enviar proposta. O quadro Kanban e a lista exibem os mesmos registros; cada cartão representa uma solicitação/oportunidade, com suas revisões de proposta no detalhe.

O fluxo previsto é: **solicitação recebida → avaliação → elaboração → envio registrado → negociação → aprovação ou encerramento**. Os resultados de encerramento incluem não cotada, recusada, expirada ou cancelada. A decisão de não cotar deve ter motivo. Aprovação, envio e demais resultados são registrados manualmente por usuários autorizados, com histórico.

Na primeira versão, o sistema registra e acompanha os dados. O envio ao cliente e a produção de documento ou PDF são externos. Leitura automática de e-mail, assinatura eletrônica, portal do cliente e automações de comunicação não fazem parte deste recorte.

## Base técnica

- **Backend:** .NET 10 e ASP.NET Core.
- **Frontend:** Angular.
- **Banco de dados:** PostgreSQL.
- **Organização do software:** monólito modular, com Clean Architecture e Vertical Slice.
- **Autenticação:** ASP.NET Core Identity com cookie HttpOnly.
- **Segurança:** autorização e isolamento por organização são responsabilidades do backend. Os dados de uma organização não devem ficar acessíveis a outra.

## O que já foi implementado

- **Acesso e perfis:** autenticação, sessão e proteção contra falsificação de requisição; perfis-base Administrador, Gestor e Operador inseridos por seed de banco. A gestão de usuários é restrita ao Administrador.
- **Equipe:** APIs e telas para cadastro e edição de usuários, senha temporária e troca obrigatória da senha inicial.
- **Clientes e locais:** cadastro, busca, consulta e edição; dados de endereço; contatos associados a cliente/local; validação e normalização de CPF/CNPJ; CEP e links de mapa.
- **Demandas:** criação manual, listagem, busca, detalhe e edição; controle para evitar sobrescrita de alterações concorrentes; histórico de mudanças; referências externas tipadas e múltiplas; primeiros recursos para subitens.
- **Propostas e rascunhos:** estrutura de dados, persistência, migração de banco, endpoints, páginas iniciais, visualização em Kanban/lista e fila de rascunhos. O registro de aprovação foi conectado à criação idempotente de rascunho, para que repetir a operação não gere outro rascunho.
- **Interface:** navegação responsiva, visão geral e áreas de clientes, demandas, propostas e equipe. Foi estabelecido um padrão visual para formulários, filtros, botões e paginação.

## Situação atual e nível de validação

O núcleo de acesso, equipe, clientes, locais e demandas já passou por builds e níveis variados de testes e demonstrações locais. A edição de demanda e o isolamento entre organizações foram exercitados por testes automatizados; telas de clientes, locais e demandas também foram percorridas manualmente em ambiente local.

O módulo de propostas e conversão em demanda está **implementado parcialmente e ainda não concluído**. O build de produção do backend passou sem avisos na última verificação registrada. O build Angular passou nesta revisão, mas seu bundle inicial ficou em 520,37 kB, 20,37 kB acima do limite configurado de 500 kB. Ainda faltam testes integrados do fluxo comercial, validação autenticada ponta a ponta e inspeção visual e de interação das páginas de propostas e rascunhos. A migração do módulo foi aplicada ao banco local de desenvolvimento; isso não representa implantação em homologação ou produção.

## O que falta implementar ou decidir

### Finalizar propostas e conversão

- Testar criação e transições de solicitações, revisões, envio registrado e encerramento por não cotada/recusada/expirada/cancelada.
- Verificar aprovação e criação idempotente do rascunho, inclusive repetição, concorrência e isolamento entre organizações.
- Validar a fila de rascunhos com os diferentes perfis, vínculo de cliente/local e confirmação operacional.
- Confirmar que os itens aprovados são preservados como subitens da demanda e que a demanda confirmada entra no fluxo como recebida.
- Inspecionar as telas autenticadas, os estados de erro/vazio e o uso em dispositivos móveis.

### Completar o ciclo operacional

Ainda precisam ser concluídos ou detalhados os marcos de visita técnica, orçamento e revisões, autorização do cliente, agendamento, execução, registro de evidências, pausa, cancelamento, conclusão e retrabalho em garantia. É importante distinguir aprovação de orçamento, autorização para faturar e alterações de escopo por aditivo.

### Definir e implementar o financeiro

Faltam decisões sobre a matriz de permissões financeiras, eventual perfil Financeiro, categorias de custo e parâmetros configuráveis por organização. Depois dessas definições, o produto deve cobrir autorização de faturamento, notas fiscais, previsão e registro de recebimentos, adiantamentos, pagamentos a prestadores e despesas associadas às demandas.

### Outras pendências

- Identificar referências usadas por clientes que não trabalham com número de chamado.
- Definir o período e o processo de importação assistida do histórico de demandas, faturamento e recebimentos.
- Avaliar contratos, integrações de e-mail/WhatsApp e automações em etapas futuras, com base nas necessidades confirmadas e na maturidade dos registros.

## Perguntas para a revisão especializada

1. O fluxo entre solicitação comercial, revisões de proposta, aprovação, rascunho e demanda confirmada cobre as situações típicas de empresas de construção e reformas?
2. Quais informações e evidências devem ser obrigatórias em cada marco, especialmente aprovação do orçamento, autorização para iniciar e autorização de faturamento?
3. Como separar corretamente o escopo aceito na proposta, os subitens de execução e os aditivos solicitados durante o serviço?
4. O modelo de cliente, local, demanda e possível contrato representa bem as relações comerciais e operacionais do setor?
5. Quais controles de acesso, auditoria e isolamento entre empresas são necessários para dados comerciais e financeiros?
6. Quais regras devem ser padrão do produto e quais precisam ser configuráveis por empresa?
7. Que riscos de operação, faturamento ou recebimento não estão contemplados no escopo atual?
