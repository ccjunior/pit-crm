# ADR-003: Separar Comercial de Demandas na conversão de propostas

- **Data**: 2026-11-21
- **Status**: Proposta
- **Decisores**: Responsável pelo produto; equipe de implementação
- **Requisito**: T26, critérios PROP-05–PROP-17

## Contexto e problema

O PIT Obras mantém a criação manual de demandas e adicionará o fluxo de solicitação comercial, revisão de proposta e aprovação do cliente. A aprovação precisa gerar um rascunho sem registrar uma demanda incompleta na operação. O escopo da funcionalidade foi aprovado em 21/11/2026; a fronteira entre os módulos é uma decisão técnica de implementação e ainda não recebeu aprovação arquitetural explícita.

## Direcionadores

- Preservar a demanda operacional como unidade de trabalho.
- Manter revisões e resultados comerciais fora do ciclo operacional.
- Fazer aprovação e geração/atualização do rascunho atomicamente no PostgreSQL.
- Evitar duplicação quando uma aprovação for reenviada.

## Opções consideradas

- Colocar solicitações, propostas, revisões e demandas no módulo `Demands`.
- Separar `Proposals` e manter a conversão por contrato de aplicação e transação síncrona.

## Resultado proposto

Separar `Proposals` como dono das solicitações, revisões, itens comerciais e eventos de envio/aprovação. `Demands` permanece dono de rascunhos, demandas confirmadas e subitens executáveis. A aprovação chama o serviço de rascunho dentro da mesma transação; uma chave única por organização e solicitação comercial dá idempotência ao rascunho, que é atualizado por nova revisão aprovada até a conversão operacional.

## Consequências

- A fronteira mantém modelos e regras comerciais fora da unidade operacional de demanda.
- A transação síncrona evita consistência eventual entre aprovação e rascunho, aproveitando o banco único do monólito.
- A persistência fica acoplada à mesma unidade de trabalho EF Core; uma extração futura para serviços exigirá substituir o contrato transacional por outbox ou protocolo equivalente.
- Demanda e subitens guardam referências tenant-scoped para revisão e itens de origem.

## Referências

- [Especificação aprovada T26](../../.specs/features/propostas/spec.md).
- [Desenho do módulo](../../.specs/features/propostas/design.md).
- Autorização de escopo: aprovação do usuário em 21/11/2026 e confirmação de D01.
