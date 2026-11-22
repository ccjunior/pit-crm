# Verificação — referências externas em demandas

Data: 20/11/2026.

## Resultado

| Área | Verificação | Resultado |
| --- | --- | --- |
| Backend | `dotnet build backend/PitCrm.sln --configuration Debug` | Aprovado, 0 avisos e 0 erros. |
| Backend | `dotnet test backend/PitCrm.sln --configuration Debug` com `PITCRM_TEST_CONNECTION` para o PostgreSQL Compose | 8 aprovados, 0 falhas. Os testes criaram e removeram schemas temporários. |
| Migração | `DemandExternalReferenceMigrationTests` migra schema com dados legados e lê os registros convertidos | Aprovado; chamado e ordem de compra viraram referências tipadas, com espaços externos removidos. |
| Frontend | `npm test -- --watch=false` | 8 arquivos e 30 testes aprovados. |
| Frontend | `npm run build` | Aprovado. Bundle inicial 504,39 kB, 4,39 kB acima do orçamento de 500 kB. |
| Repositório | `git diff --check` | Aprovado. |

Os testes backend cobrem criação, atualização, detalhe, busca por tipo/valor, referências de tipos repetidos, validação, conflito de versão e isolamento organizacional. Os testes Angular cobrem edição de linhas repetíveis, validação de linhas incompletas, sugestões e exibição no detalhe.

## Migração e dados locais

A migração cria a tabela de referências, converte as antigas colunas `TicketReference` e `PurchaseOrderReference` e, somente depois, remove essas colunas. O teste de migração aplica a sequência em schema temporário com demanda legada sintética e confirma a preservação dos valores.

A migração nova **não foi aplicada ao banco `pitcrm_dev`**. Nenhum banco compartilhado ou publicado foi alterado. O fluxo autenticado ponta a ponta não foi repetido nesta entrega; a cobertura é por testes de integração da API e testes Angular.

## Conclusão

T25.1–T25.4 concluídas localmente. O esquema conceitual do esboço não mudou o ciclo de vida vigente: a demanda continua sendo criada quando a solicitação chega, conforme D02/D07. Uma entidade Lead e a conversão de proposta aprovada em demanda continuam não especificadas e fora deste escopo.
