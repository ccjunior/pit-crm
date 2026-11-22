# Acompanhamento comercial — desenho preliminar de AC-T01

**Estado:** recomendação para decisão de produto; não autoriza migração nem alteração de código.

## Contexto auditado

O código atual usa `CommercialRequest`, com UUID, etapa, cliente ou prospect, canal (`WhatsApp`, `Ligação`, `E-mail`, `Outra`), descrição, versão de concorrência e atividades textuais. Não tem número comercial legível, responsável comercial, local, prazos, próxima ação ou referências do cliente. Revisões usam números inteiros. As consultas comerciais filtram explicitamente por organização; o EF Core não possui filtros globais. O relatório da Fase 0 está na seção 14 do documento 09.

O caminho de conversão já exige que cliente, local, descrição e origem estejam presentes antes de criar a demanda operacional. Para prospect, Administrador/Gestor cria ou associa cliente/local; Operador completa e confirma o rascunho.

## Recomendações

| Decisão de AC-T01 | Recomendação para avaliação | Efeito se aprovada |
|---|---|---|
| Identificador de solicitação | `SOL-AAAA-NNNN`, único por organização e ano, sequência crescente sem reutilizar números. Uma linha de sequência por organização/ano é atualizada atomicamente na mesma transação da criação. | Adiciona número legível e índice único composto por organização/número. Registros existentes recebem números por `CreatedAtUtc` e UUID como desempate, sem alterar seus UUIDs ou vínculos. |
| Atribuição | Atribuição manual. Pode ficar vazia enquanto `Recebida`, mas é obrigatória antes da transição para `EmAvaliacao`. Admin/Gestor escolhe um usuário ativo Admin/Gestor da mesma organização. Sem atribuição automática na primeira entrega. | Adiciona `ResponsibleUserId` anulável e validação de organização/estado no backend; mantém o fluxo comercial nas permissões atuais. |
| Canal e origem | Separar `SourceType` (`Manual`/`Email`) do canal informado pelo cliente. Iniciar com E-mail, Telefone, WhatsApp, Indicação, Visita, Licitação e Outro. Recomenda-se permitir configuração por organização por catálogo, sem transformar texto livre em filtro inconsistente. | Exige catálogo e consulta de opções por organização. O legado atual do PIT mapeia WhatsApp, Ligação e E-mail diretamente; `Outra` passa a Outro. Nenhuma solicitação atual terá origem Email automaticamente. |
| Referências do cliente | Permitir várias referências tipadas por solicitação, com tipo e valor, reaproveitando a convenção já usada em referências externas de demandas. Sugestões de tipo: RFQ/cotação, chamado/OS, pedido de compra e outra. | Adiciona coleção própria ao Comercial, pesquisável e isolada por organização; não altera nem presume unicidade global. Tipos de referência não são fixados como universais. |
| Local de prospect | Manter texto livre para descrever o local solicitado enquanto ainda não existe `CustomerLocation`; preservar esse texto após associar cliente/local. Não criar local automaticamente a partir da extração ou do rascunho. | Adiciona descrição textual e vínculo anulável. Conversão continua bloqueada até que Admin/Gestor associe um local real, conforme D01 e D02. |

Prazos e próxima ação fazem parte da AC-T03, não desta decisão de dados. Recomenda-se tratá-los como datas civis (sem hora) para prazo do cliente e prazo interno; a próxima ação pode guardar instante UTC. Fuso, regra de atraso, destinatário e lembrete precisam de decisão própria antes de implementar filtros/alertas. Não se propõe job, notificações, anexos ou visitas nesta tarefa.

## Compatibilidade e limites

- Preservar `CommercialRequest` e as tabelas existentes; não renomear para `QuoteRequest` na Fase 2.
- Não criar coluna de contrato sem entidade de contrato existente.
- Não alterar a estratégia atual de isolamento explícito por organização sem desenho arquitetural abrangente. Novas operações devem manter filtro de organização em todas as consultas, escritas e vínculos e ter testes negativos.
- A numeração retroativa e o catálogo configurável dependem da confirmação das decisões correspondentes. A migração deve ser transacional, reversível quando possível e não atribuir valores comerciais fictícios.
- A aprovação técnica da implementação não substitui decisões de negócio listadas abaixo.

## Decisões necessárias antes da migração

1. Aprovar `SOL-AAAA-NNNN` com reinício anual por organização e atribuição retroativa determinística, ou escolher outro formato/ciclo.
2. Confirmar responsável Admin/Gestor obrigatório antes de iniciar avaliação; informar se a atribuição automática ao criador deve ser considerada.
3. Confirmar catálogo de canais editável por organização ou lista fixa na primeira versão. Separar canal de entrada de origem Manual/Email.
4. Confirmar referências múltiplas tipadas ou uma referência única em texto.
5. Confirmar o campo de descrição do local de prospect e a exigência de associação a um local real antes da conversão.

Até essas respostas, AC-T01 não altera banco ou APIs. As tarefas AC-T02 e seguintes permanecem bloqueadas pelas dependências indicadas em `tasks.md`.
