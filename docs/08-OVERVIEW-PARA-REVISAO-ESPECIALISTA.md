# PIT Obras — visão geral para revisão especializada

**Atualizado em 21/11/2026**  
**Nome interno do repositório:** PIT-CRM  
**Produto:** PIT Obras, da Project IT

## O que é o projeto

O PIT Obras é um sistema web para organizar a gestão comercial e operacional de empresas de construção civil e reformas. A proposta é acompanhar clientes, locais, equipe, solicitações comerciais e demandas em um fluxo único, preservando histórico, responsabilidades e separação dos dados por organização.

A base técnica aprovada é .NET 10, Angular e PostgreSQL, organizada como monólito modular com Clean Architecture e Vertical Slice. A autenticação usa ASP.NET Core Identity com cookie HttpOnly. Permissões e isolamento entre organizações são aplicados no backend. Mais detalhes: [arquitetura técnica](04-ARQUITETURA-TECNICA.md).

## O que já foi implementado

- **Acesso e organização:** autenticação, sessão, proteção contra falsificação de requisição, políticas iniciais e perfis-base Administrador, Gestor e Operador. Os perfis são carregados por migração seed.
- **Equipe:** APIs e interface para gestão de usuários, senha temporária e troca obrigatória de senha, com acesso à gestão restrito ao Administrador.
- **Clientes e locais:** cadastro, consulta e edição, endereço principal, contatos, validação e normalização de CPF/CNPJ, CEP, link de mapa e isolamento por organização.
- **Demandas:** abertura manual, busca, listagem, detalhe e edição com controle de concorrência e histórico de alterações; referências externas múltiplas; início do suporte a subitens.
- **Interface:** shell responsivo, acesso, visão geral e telas operacionais. A documentação define um padrão global para telas de cadastro e edição.
- **Propostas — T26, em implementação:** estrutura de solicitações comerciais e revisões de proposta, Kanban/lista, endpoints e telas de acompanhamento, registro manual de aprovação e criação idempotente de rascunho de demanda. Há também uma fila para completar e confirmar esses rascunhos.

As evidências e limites por funcionalidade estão no [estado do projeto](../.specs/STATE.md) e nas verificações ligadas às [especificações](../.specs/features/README.md). “Implementado” não significa que toda a funcionalidade esteja validada em ambiente publicado.

## Fluxos de negócio definidos

Há dois caminhos para iniciar uma demanda:

1. Criar a demanda manualmente para um cliente existente.
2. Registrar uma solicitação comercial de um cliente ou prospect, elaborar e revisar uma proposta e registrar manualmente o resultado. A aprovação cria um rascunho de demanda, que é completado e confirmado antes de entrar na operação.

Uma demanda confirmada deve ter cliente, local, descrição e origem. Itens da revisão aprovada podem ser copiados como subitens, mantendo o escopo aceito e sua rastreabilidade. O fluxo comercial não exige contrato como condição de conversão; gestão de contratos permanece fora do recorte atual. Envio automático, leitura de e-mail, assinatura eletrônica e geração de PDF também não fazem parte da primeira versão do módulo de propostas.

## O que falta implementar ou concluir

### Conclusão da T26 — propostas e conversão

O código e a interface-base foram criados, mas a entrega ainda está marcada como **em andamento**. Faltam testes de fluxo e endpoints, validação autenticada ponta a ponta, verificação de idempotência/concorrência e inspeção visual e de interação das telas de proposta e rascunho. A migração T26 foi aplicada somente ao banco local `pitcrm_dev`; isso não comprova implantação em outros ambientes. O build Angular passou nesta revisão, mas o bundle inicial de 520,37 kB excedeu em 20,37 kB o orçamento configurado de 500 kB. Durante esta revisão, a API local foi observada escutando a porta 5006; seu processo não foi iniciado nem alterado por esta atividade.

### Próximas áreas do produto

- Fechar as regras ainda abertas do piloto: matriz de permissões, eventual perfil Financeiro, categorias de custo, referências específicas de clientes sem chamado e período/recorte para importação do histórico.
- Completar o ciclo operacional: visita técnica, orçamento e revisões, autorização do cliente, agendamento e execução, evidências, cancelamento/pausa, conclusão e retrabalho em garantia, conforme roadmap e decisões pendentes.
- Implementar faturamento e financeiro operacional: autorização de faturamento, notas fiscais, recebimentos, adiantamentos, pagamentos e despesas. As permissões e parâmetros financeiros por organização precisam ser definidos antes dessa etapa.
- Planejar importação assistida do histórico operacional e financeiro, depois de conhecer as planilhas, qualidade e período necessário.
- Considerar integrações e automações (e-mail, WhatsApp e outras) somente em etapa futura, quando os fluxos e dados operacionais estiverem estáveis.

O detalhamento vigente está no [roadmap](03-ROADMAP-E-PENDENCIAS.md), no [fluxo e modelo](02-FLUXO-E-MODELO.md) e nas [tarefas da T26](../.specs/features/propostas/tasks.md).

## Pontos para o especialista avaliar

- O fluxo entre solicitação, versões da proposta, aprovação, rascunho e demanda confirmada representa adequadamente uma empresa de construção e reformas?
- A divisão entre subitens da demanda e futuros aditivos está clara para preservar o escopo aprovado e registrar mudanças posteriores?
- Quais controles de aprovação, evidências e permissões são importantes antes de iniciar faturamento e recebimentos?
- Há lacunas de domínio, auditoria ou isolamento multiempresa que devem ser resolvidas antes de concluir a T26 ou avançar para o financeiro?
- Quais requisitos devem ser gerais do produto e quais devem continuar configuráveis por organização?

## Referências do projeto

- [Direção e escopo](01-DIRECAO-E-ESCOPO.md)
- [Fluxo e modelo](02-FLUXO-E-MODELO.md)
- [Roadmap e pendências](03-ROADMAP-E-PENDENCIAS.md)
- [Arquitetura técnica](04-ARQUITETURA-TECNICA.md)
- [Padrão de telas de cadastro e edição](07-PADRAO-DE-INTERFACE.md)
- [Estado atualizado e evidências](../.specs/STATE.md)
