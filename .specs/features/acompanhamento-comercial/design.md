# Acompanhamento comercial — decisões de AC-T01

**Estado:** decisões de produto confirmadas pelo usuário em 22/11/2026; AC-T02 implementada localmente, pendente de validação integrada e aplicação autorizada da migração.

## Contexto auditado

O código atual usa `CommercialRequest`, com UUID, etapa, cliente ou prospect, canal (`WhatsApp`, `Ligação`, `E-mail`, `Outra`), descrição, versão de concorrência e atividades textuais. Não tem número comercial legível, responsável comercial, local, prazos, próxima ação ou referências do cliente. Revisões usam números inteiros. As consultas comerciais filtram explicitamente por organização; o EF Core não possui filtros globais. O relatório da Fase 0 está na seção 14 do documento 09.

O caminho de conversão já exige que cliente, local, descrição e origem estejam presentes antes de criar a demanda operacional. Para prospect, Administrador/Gestor cria ou associa cliente/local; Operador completa e confirma o rascunho.

## Recomendações

| Decisão de AC-T01 | Recomendação para avaliação | Efeito se aprovada |
|---|---|---|
| Identificador de solicitação | Confirmado: `SOL-AAAA-NNNN`, único por organização/ano, sequência crescente sem reutilização; retroativos em ordem de criação, UUID como desempate. | Sequência transacional e índice único por organização/número; UUIDs e vínculos existentes preservados. |
| Atribuição | Confirmado: manual, pode ficar vazia em Recebida e é obrigatória antes de Em avaliação; Admin/Gestor atribui usuário ativo elegível da organização. | Campo anulável e validação de vínculo/etapa no backend. |
| Canal e origem | Confirmado: lista fixa igual para as organizações; canal separado de origem técnica Manual/Email. Opções: E-mail, Telefone, WhatsApp, Indicação, Visita, Licitação e Outro. | Valores são validados no backend; valores legados serão mapeados para opções equivalentes. |
| Referências do cliente | Confirmado: múltiplas referências tipadas por solicitação. Tipos iniciais: RFQ/cotação, chamado/OS, pedido de compra e outra. | Coleção própria, pesquisável e tenant-scoped, sem unicidade global. |
| Local de prospect | Confirmado: deixar o local para completar no rascunho. Aplicar D01: Administrador/Gestor cria ou associa cliente/local; Operador completa e confirma a demanda. | Não adicionar local obrigatório à solicitação. Conversão segue bloqueada até o rascunho ter vínculo válido de cliente/local. |

Prazos e próxima ação fazem parte da AC-T03, não desta decisão de dados. Recomenda-se tratá-los como datas civis (sem hora) para prazo do cliente e prazo interno; a próxima ação pode guardar instante UTC. Fuso, regra de atraso, destinatário e lembrete precisam de decisão própria antes de implementar filtros/alertas. Não se propõe job, notificações, anexos ou visitas nesta tarefa.

## Compatibilidade e limites

- Preservar `CommercialRequest` e as tabelas existentes; não renomear para `QuoteRequest` na Fase 2.
- Não criar coluna de contrato sem entidade de contrato existente.
- Não alterar a estratégia atual de isolamento explícito por organização sem desenho arquitetural abrangente. Novas operações devem manter filtro de organização em todas as consultas, escritas e vínculos e ter testes negativos.
- A migração preenche números retroativos por organização/ano, ordenados por criação e UUID, sem alterar UUIDs ou criar valores comerciais. O canal de origem técnica recebe `Manual` nos registros atuais; futuras entradas por integração podem registrar `Email` sem confundir essa origem com o canal comercial.
- A aprovação técnica da implementação não substitui decisões de negócio listadas abaixo.

## Limites ainda abertos

As decisões de datas, timezone, lembretes, motivos estruturados, atividades, visita, auditoria e anexos permanecem para AC-T03–AC-T07. Esta confirmação não autoriza antecipar essas regras.
