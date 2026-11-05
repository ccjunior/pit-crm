# Contexto e decisões — primeira entrega operacional

Data: 04/11/2026.

## Autorização deste trabalho

O usuário respondeu “vamos” à recomendação de especificar a primeira entrega utilizável: acesso à organização, clientes, locais e cadastro/listagem de demandas. Esta autorização cobre a elaboração e o refinamento dos documentos; não é registrada como aprovação de uma especificação ainda não apresentada.

## Decisões confirmadas

| ID | Pergunta | Resposta literal do usuário | Consequência |
| --- | --- | --- | --- |
| D01 | Quem pode cadastrar e editar clientes e locais na primeira entrega? | “Administrador e Gestor; Operador apenas consulta clientes e locais” | A administração desses cadastros pertence aos dois primeiros perfis; o Operador usa os registros para trabalhar com demandas. |
| D02 | Quais campos exigir na abertura da demanda? | “Exigir cliente, local, descrição e origem; responsável opcional” | O responsável operacional interno pode ser atribuído depois da abertura. |
| D03 | Como será o primeiro acesso? | “Criação inicial controlada; administrador cadastra a equipe” | A organização e seu primeiro administrador são provisionados de forma controlada; a gestão da equipe é interna à organização. |
| D04 | Documento e instruções do cliente são obrigatórios? | “Documento e instruções opcionais (Recomendado)” | Nome permanece obrigatório; documento e instruções são opcionais. |
| D05 | O endereço do local é obrigatório? | “Endereço obrigatório” | Nome e endereço do local são obrigatórios; instruções permanecem opcionais conforme a proposta anterior, pois a resposta alterou apenas o requisito do endereço. |
| D06 | Como representar a prioridade? | “Lista fixa Baixa/Média/Alta” | Prioridade opcional com valores Baixa, Média ou Alta; não é campo de texto livre. |
| D07 | Qual é o estado inicial da demanda? | “Usar ‘Recebida’ (Recomendado)” | Demandas novas iniciam em Recebida; transições posteriores permanecem fora desta entrega. |
| D08 | Como funciona a credencial inicial da equipe? | “Senha temporária e troca obrigatória (Recomendado)” | Administrador define credencial temporária, entrega fora do sistema e usuário troca no primeiro acesso antes de acessar a operação; sem envio automático de e-mail. |

As respostas D01–D03 foram recebidas em 04/11/2026; D04–D08 foram confirmadas em 05/11/2026 antes das tarefas dependentes. Os perfis-base e o direito da operação de criar/editar demandas já estavam documentados no escopo.

## Aprovação do escopo

Em 05/11/2026, após ser apresentada como direção a implementação da primeira entrega operacional, o usuário aprovou o escopo especificado com a resposta literal “aprovo”. Isso autoriza a implementação local da funcionalidade como primeira entrega. D04–D08 confirmam os detalhes de cadastro, prioridade, estado inicial e credencial descritos acima. Não autoriza commit, push, publicação ou alterações em ambientes externos.

## Limites de interpretação

- O responsável opcional de D02 é o responsável operacional interno. A atribuição de equipe/prestador para execução continua sendo um conceito distinto do fluxo completo.
- D01 resolve o acesso aos cadastros desta entrega. Aprovações de orçamento, permissões financeiras e o perfil Financeiro permanecem no roadmap.
- D03 não define cobrança, contratação da plataforma, convites por e-mail ou uma área pública de criação de empresas.
- Os limites técnicos de texto, paginação, rotas e organização das telas continuam como detalhamentos técnicos da especificação, não como regras comerciais declaradas pelo piloto.

## Fontes

- [Direção e escopo](../../../docs/01-DIRECAO-E-ESCOPO.md)
- [Fluxo e modelo](../../../docs/02-FLUXO-E-MODELO.md)
- [Roadmap](../../../docs/03-ROADMAP-E-PENDENCIAS.md)
- [Arquitetura](../../../docs/04-ARQUITETURA-TECNICA.md)
