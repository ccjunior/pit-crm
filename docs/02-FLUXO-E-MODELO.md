# Fluxo operacional e modelo conceitual

## Fluxo de ponta a ponta

1. **Receber demanda:** registrar origem, cliente, local, descrição e referências externas disponíveis.
2. **Realizar visita técnica inicial:** quando necessária, é o primeiro atendimento da equipe.
3. **Preparar e enviar orçamento:** registrar valor e versão do orçamento.
4. **Obter autorização do orçamento:** a execução só é programada depois que o cliente autoriza.
5. **Programar e executar:** combinar data com cliente e técnico/prestador, acompanhar o serviço e registrar ocorrências.
6. **Documentar e concluir:** guardar fotos antes/durante/depois, vídeos, relatório e apresentar evidências ao cliente.
7. **Obter autorização para faturar:** aprovação do cliente para emitir NF, separada da aprovação do orçamento.
8. **Faturar:** registrar nota fiscal e envio ao cliente.
9. **Receber:** acompanhar previsão, eventual adiantamento e pagamento efetivo.
10. **Consultar histórico:** localizar todo o ciclo por demanda, cliente, local, chamado e período.

## Estados e marcos

As definições abaixo foram informadas pela operação piloto. O desenho final da interface ainda pode ser refinado.

| Estado/termo | Significado informado | Tratamento proposto |
| --- | --- | --- |
| Visita técnica programada | Primeiro atendimento/visita para avaliar a demanda | Agendamento separado da execução |
| Autorizado | Orçamento enviado e aprovado pelo cliente | Marco de aprovação do orçamento; libera o agendamento da execução |
| Execução programada | Serviço agendado após autorização do orçamento | Etapa/data própria, distinta da visita técnica |
| Executado | Concluído e pronto para faturar | Conclusão técnica; pode aguardar autorização de NF e faturamento |
| Pausado | Cliente ou empresa adia; pode ser por falta de mão de obra ou material | Registrar motivo, solicitante e condição/data de retomada |
| Cancelado | Cliente cancela a solicitação ou a autorização | Encerrar sem confundir com pausa ou garantia |
| Retrabalho em garantia | Nova atuação necessária dentro da garantia | Registrar como retrabalho e vincular à demanda original |
| Retificar | Orçamento enviado/autorizado precisa ser alterado | Criar nova versão e preservar a anterior |
| Faturar | Demanda concluída e pronta para emissão da NF | Marco/lista de faturamento, separado da execução |
| Aditivo | Acrescentar item ou ajustar orçamento/escopo | Alteração do orçamento com histórico e aprovação |
| ADD | Adiantamento recebido ou pago a prestador | Movimento financeiro de adiantamento, não aditivo |

**Duas aprovações diferentes:** o cliente aprova o orçamento antes da execução e autoriza a emissão da nota depois da execução/evidências. Não devem ser fundidas em um único status.

**Atribuição de equipe:** no processo piloto, cada demanda terá uma equipe ou prestador responsável.

**Abertura da demanda:** para a primeira entrega, o usuário confirmou em 04/11/2026 a obrigatoriedade de cliente, local, descrição e origem. O responsável operacional interno pode ser atribuído depois. Essa pessoa acompanha a demanda e não substitui a atribuição da equipe/prestador de execução.

## Entidades conceituais

- **Organização:** empresa cliente do PIT-CRM e fronteira de isolamento dos dados.
- **Usuário e papel:** pessoa que acessa a organização. Perfis iniciais: Administrador (acesso completo e gestão de usuários/configurações), Gestor (operação, indicadores e dados financeiros conforme permissão) e Operador (criar/alterar demandas, programação e evidências). Permissões podem ser ajustadas por organização/usuário.
- **Perfis opcionais/futuros:** Financeiro (custos, cálculo LL, notas e recebimentos); Prestador (somente demandas atribuídas, atualizações e evidências); Cliente (acompanhar/autorizar demandas próprias, fora do MVP até confirmar necessidade).
- **Cliente e contato:** empresa atendida e seus responsáveis.
- **Local de atendimento:** loja, unidade ou endereço de execução.
- **Demanda/ordem de serviço:** unidade principal do trabalho, com ID interno, origem, descrição, responsável e referências externas.
- **Referência externa:** chamado/ticket, ordem de compra ou código do sistema do cliente. Pode haver mais de uma e não substitui o ID interno.
- **Orçamento e revisão:** valor proposto, versões, aprovação, retificação e aditivo.
- **Programação/atribuição:** visita técnica, execução agendada e um técnico/equipe/prestador responsável por demanda.
- **Execução e evidência:** datas, resultado, fotos, vídeos, relatório e observações.
- **Retrabalho em garantia:** registro vinculado à demanda que originou a garantia.
- **Nota fiscal e recebível:** emissão, envio, previsão, recebimento e atrasos.
- **Despesa e adiantamento:** material comprado para uma demanda e pagamentos/recebimentos antecipados.
- **Cálculo LL e parâmetros por organização:** custo estimado manual por itens, percentual de imposto/administração e meta de LL; não compartilhar a regra de uma empresa com as demais.
- **Histórico de atividade:** alterações relevantes, responsáveis, datas e comentários.

## Regras a manter explícitas

- O chamado/ticket depende do cliente; pode não existir e pode não ser único entre clientes. Deve ser uma referência pesquisável, não a chave primária.
- Um cliente pode ter muitos locais e demandas. Normalmente há uma demanda por local, mas isso não deve ser imposto como unicidade rígida.
- “Autorizado” para orçamento e autorização para emitir NF após execução são aprovações diferentes.
- “Aditivo” muda escopo/valor; “ADD” é adiantamento financeiro.
- Na planilha LL, o custo estimado é digitado manualmente; imposto/administração é considerado fixo em 28% pela empresa piloto. O resultado chamado LL significa Lucro Líquido, e a meta mínima é 20%. Pela fórmula atual, o percentual é LL dividido pelo valor aprovado.
- Ari aprovou incorporar o cálculo ao PIT-CRM. Os parâmetros são da empresa piloto e devem ser configuráveis por organização; os nomes dos itens de custo ainda precisam ser definidos.
