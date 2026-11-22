# Referências externas múltiplas em demandas

**Status:** Implementada e verificada localmente
**Autorização:** em 20/11/2026, o usuário pediu avanço para a próxima tarefa e orientou que o produto use entendimento generalizado para empresas de construção e reformas. A tarefa aplica essa direção a uma necessidade já prevista no roadmap: referências externas opcionais, pesquisáveis e vinculadas à demanda.

## Problema

O cadastro atual suporta apenas um chamado e uma ordem de compra como colunas fixas. Empresas de construção e reformas podem identificar a mesma demanda por mais de uma referência do cliente, e alguns clientes não usam chamado. Campos fixos deixam esses casos difíceis de registrar e pesquisar.

## Resultado

Uma demanda pode ter zero ou várias referências externas, cada uma com um tipo/descrição e um valor. Referências são opcionais, pesquisáveis e pertencem à organização da demanda. O identificador interno `DEM-nnnn` continua sendo a referência primária do PIT Obras.

## Escopo

- Substituir os campos fixos de chamado e ordem de compra por uma coleção de referências externas.
- Permitir descrições de tipo em texto livre, com sugestões usuais como Chamado/OS, Ordem de Compra, Código do Cliente, Contrato e Outro.
- Manter os campos opcionais; referências antigas devem ser migradas sem perda.
- Incluir, listar, alterar e remover referências junto ao formulário de criação/edição da demanda.
- Pesquisar demandas por tipo e valor de qualquer referência.
- Exibir referências no detalhe da demanda.

## Fora de escopo

- Catálogo configurável de tipos por organização.
- Regras de unicidade de referências.
- Integração automática com sistemas ou chamados dos clientes.
- Mudanças nas permissões, no fluxo de status ou no financeiro.

## Requisitos e critérios de aceite

**REF-01 — Referências opcionais.** WHEN um usuário autorizado cria ou edita uma demanda sem referências externas, THEN o sistema SHALL salvar a demanda normalmente.

**REF-02 — Referência completa.** WHEN uma referência é informada, THEN o sistema SHALL exigir tipo e valor não vazios, remover espaços externos e aceitar tipo de até 50 caracteres e valor de até 100 caracteres.

**REF-03 — Várias referências.** WHEN uma demanda contém mais de uma referência, THEN o sistema SHALL persistir e devolver todas, incluindo tipos repetidos, sem exigir unicidade do tipo ou do valor.

**REF-04 — Escopo organizacional.** O sistema SHALL persistir referências pela relação com a demanda e sua organização; ao pesquisar ou consultar uma demanda, SHALL retornar apenas referências pertencentes a ela e à organização autenticada.

**REF-05 — Busca.** WHEN um termo de busca corresponde parcialmente, sem diferenciar maiúsculas/minúsculas, ao tipo ou valor de uma referência, THEN a pesquisa SHALL incluir a demanda correspondente, respeitando o isolamento por organização.

**REF-06 — Detalhe e formulário.** WHEN um usuário abre o detalhe ou a edição de uma demanda, THEN a interface SHALL mostrar as referências persistidas e permitir acrescentar/remover linhas antes de salvar.

**REF-07 — Migração de legado.** WHEN a migração é aplicada a dados que possuem chamado ou ordem de compra, THEN o sistema SHALL criar referências dos tipos Chamado/OS e Ordem de Compra com os valores existentes antes de remover as colunas antigas.

**REF-08 — Concorrência.** WHEN a coleção de referências é salva durante uma edição, THEN o sistema SHALL usar a versão da demanda e salvar referências, dados básicos e atividade na mesma transação.

## Suposições adotadas

- Tipo é um rótulo livre de até 50 caracteres; as opções sugeridas na interface aceleram a entrada sem restringir os tipos que uma empresa usa.
- Tipo e valor podem se repetir. A demanda e o cliente relacionados fornecem o contexto, e nenhum dado atual autoriza uma regra de unicidade.
- A aplicação é entregue com frontend e backend juntos; endpoints internos existentes são atualizados para o novo contrato. A migração preserva os dados persistidos existentes.

## Verificação

- Testes backend de criação, edição, busca, isolamento, validação e compatibilidade da migração.
- Testes Angular para lista dinâmica, sugestões, preenchimento/remoção e preservação após conflito 409.
- `dotnet test backend/PitCrm.sln` e `npm test -- --watch=false`; builds backend/frontend conforme os comandos atuais do repositório.
- Evidências consolidadas em [verification.md](verification.md).

## Fontes

- [Roadmap e pendências](../../../docs/03-ROADMAP-E-PENDENCIAS.md), que define referências externas como opcionais, pesquisáveis, múltiplas e distintas do ID interno.
- [Direção e escopo](../../../docs/01-DIRECAO-E-ESCOPO.md), cadastro de demandas com ticket/OC opcional e outras referências.
- [Fluxo e modelo](../../../docs/02-FLUXO-E-MODELO.md), entidade Referência externa e dependência do chamado por cliente.
- [Código e testes atuais](../../../backend/src/PitCrm.Domain/Demands/Demand.cs), [endpoint](../../../backend/src/PitCrm.Api/Features/Demands/DemandEndpoints.cs) e [formulário](../../../frontend/src/app/demands/new-demand-page.ts).
