---
name: pitcrm-backend
description: "Implementar ou alterar funcionalidades do backend do PIT-CRM em .NET 10, incluindo casos de uso, contratos da API, persistência, permissões e isolamento por organização. Use para endpoints, regras de domínio, autenticação e migrações; para telas Angular use pitcrm-frontend."
metadata:
  project: PIT-CRM
  version: "1.0.0"
---

# Backend do PIT-CRM

## Contexto necessário

Leia [AGENTS.md](../../../AGENTS.md), [arquitetura técnica](../../../docs/04-ARQUITETURA-TECNICA.md) e a especificação da entrega. Para regras operacionais, consulte as seções relevantes de [escopo](../../../docs/01-DIRECAO-E-ESCOPO.md) e [fluxo e modelo](../../../docs/02-FLUXO-E-MODELO.md).

Antes de implementar, confira quais projetos, bibliotecas, testes e padrões já existem. Na ausência de código, detalhe somente a estrutura necessária à entrega autorizada; registre as escolhas complementares quando forem feitas.

## Executar o caso de uso

1. Identifique o resultado observável, o módulo responsável, as permissões exigidas e os critérios de aceite.
2. Defina entrada, saída, validações e falhas da API. Use contratos próprios; mantenha regras de domínio fora dos detalhes de transporte e persistência.
3. Implemente o caso de uso seguindo Clean Architecture e Vertical Slice, com o tamanho de estrutura exigido pela funcionalidade. Preserve os contratos entre módulos existentes.
4. Resolva a organização a partir do contexto autenticado confiável. Valide o escopo também em gravações, relacionamentos, referências por ID e acesso a arquivos; filtros da interface são apresentação.
5. Aplique a autorização do produto no backend. Use Identity e cookies conforme a arquitetura, incluindo antifalsificação nas escritas. Reutilize o mecanismo existente quando disponível.
6. Se alterar persistência, registre a mudança de schema, as condições da migração e seu impacto nos dados. Execute migrações apenas no ambiente autorizado.
7. Execute os checks e testes relevantes disponíveis e atualize contratos ou decisões afetados.

## Pontos de atenção por funcionalidade

- Em demandas e orçamentos, preserve os identificadores, marcos de aprovação, revisões e distinções de status definidos no modelo.
- Em faturamento e recebimentos, confira as regras de autorização de NF, histórico e adiantamentos nas fontes de produto.
- Em custos e LL, use os parâmetros da organização e a fórmula documentada. Categorias e comportamentos ainda abertos precisam de decisão quando afetarem o caso de uso.

## Evidências de entrega

Relate o caso de uso implementado, contratos afetados, mudanças de persistência, verificações executadas e pendências. Para alterações de acesso ou isolamento, inclua verificações negativas com outra organização, acesso sem permissão e referências incompatíveis. Um teste só com a organização correta não comprova isolamento.

Exemplo de uso: “Implemente o endpoint de cadastro de demandas conforme a especificação aprovada, incluindo as validações de cliente e local da organização.”
