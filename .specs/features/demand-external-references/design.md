# Desenho — referências externas em demandas

## Modelo

- `DemandExternalReference`: `Id`, `OrganizationId`, `DemandId`, `Type`, `Value`.
- `Type`: texto normalizado com trim, obrigatório, 1–50 caracteres.
- `Value`: texto normalizado com trim, obrigatório, 1–100 caracteres.
- Relação e consultas limitadas ao `OrganizationId` e à demanda. Sem restrição única sobre tipo/valor.
- As antigas colunas `TicketReference` e `PurchaseOrderReference` deixam de ser fonte de dados após migração; antes disso, a migração as converte nos tipos `Chamado/OS` e `Ordem de Compra`.

## Contratos

- POST/PUT da demanda aceitam `externalReferences: [{ type, value }]`.
- GET do detalhe retorna `externalReferences`; a listagem pesquisa por tipo e valor sem retornar a coleção em cada card.
- Os DTOs de criação e edição refletem a coleção para que o formulário possa exibir o resultado persistido.
- A gravação da coleção participa da transação e da concorrência da demanda; toda edição continua exigindo a `version` atual e registrando atividade.

## Interface

- Formulário usa linhas repetíveis de tipo e valor, uma ação para adicionar e uma ação por linha para remover.
- Tipos sugeridos: Chamado/OS, Ordem de Compra, Código do Cliente, Contrato e Outro. Campo aceita texto livre.
- O detalhe mostra cada referência como tipo e valor. Se não houver referências, apresenta estado vazio discreto.

## Migração e compatibilidade

- Criar tabela e relacionamentos necessários ao escopo da organização.
- Copiar valores legados não nulos após trim; ignorar campos antigos vazios.
- Remover colunas legadas somente na migração que já fez a cópia.
- A aplicação e API são atualizadas em conjunto; testes de migração comprovam preservação dos valores e funcionamento sobre banco vazio.

## Operações de busca

- Usar `EXISTS`/`Any` correlacionado para não multiplicar linhas nem contagem de demandas.
- Aplicar a mesma busca case-insensitive e escape de `%`, `_` e `\\` já usado para descrição.
- Restringir a query pelo tenant antes de testar qualquer valor de referência.
