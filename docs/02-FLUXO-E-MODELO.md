# Fluxo operacional e modelo conceitual

## Fluxo de ponta a ponta

1. **Registrar solicitação comercial (quando houver cotação):** cliente existente ou prospect pede orçamento; a equipe registra manualmente canal, resumo e contato conhecido.
2. **Avaliar se vai cotar:** manter a decisão e o motivo de não cotar quando a empresa optar por não preparar proposta.
3. **Preparar e enviar proposta:** registrar itens, valores e revisões; envio ao cliente é manual e sua data/canal ficam registrados.
4. **Registrar resultado comercial:** aprovação, recusa, expiração ou cancelamento são lançados manualmente. Uma revisão aprovada gera um único rascunho de demanda.
5. **Validar e confirmar demanda:** Operador completa dados operacionais; para prospect, Administrador/Gestor cria ou vincula cliente/local (D01). A confirmação exige cliente, local, descrição e origem, e coloca a demanda em “Recebida”.
6. **Entrada manual direta:** cliente existente também pode solicitar serviço sem passar pela proposta; a equipe cria a demanda pelo fluxo operacional existente.
7. **Realizar visita técnica inicial:** quando necessária, é o primeiro atendimento da equipe.
8. **Programar e executar:** combinar data com cliente e técnico/prestador, acompanhar o serviço e registrar ocorrências.
9. **Documentar e concluir:** guardar fotos antes/durante/depois, vídeos, relatório e apresentar evidências ao cliente.
10. **Obter autorização para faturar:** aprovação do cliente para emitir NF, separada da aprovação da proposta/orçamento.
11. **Faturar e receber:** registrar nota fiscal/envio, previsão, eventual adiantamento e pagamento efetivo.
12. **Consultar histórico:** localizar o ciclo comercial e operacional por solicitação, proposta, demanda, cliente, local, referência e período.

O Kanban/lista acompanha solicitações comerciais; um cartão contém a negociação e suas revisões. Demanda manual e demanda gerada por proposta convergem ao mesmo fluxo operacional após confirmação.

### Etapas da solicitação comercial

`Solicitação recebida` → `Em avaliação` → `Em elaboração` → `Enviada` → `Em negociação` → `Aprovada`.

Resultados terminais: `Não cotada` (decisão interna, com motivo obrigatório), `Recusada` (resposta do cliente), `Expirada` e `Cancelada`. Envio e aprovação são registrados manualmente; não há varredura ou envio automático de e-mail.

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

**Abertura da demanda operacional:** D02 exige cliente, local, descrição e origem na demanda confirmada. O rascunho criado por proposta aprovada pode aguardar dados faltantes e não aparece nas etapas/indicadores operacionais. Administrador/Gestor criam ou editam cliente/local; se o cliente já estiver vinculado, Operador pode selecionar um local existente desse cliente, completar os demais dados e confirmar. O responsável operacional interno é opcional e não substitui a atribuição da equipe/prestador de execução.

## Entidades conceituais

- **Organização:** empresa cliente do PIT Obras e fronteira de isolamento dos dados.
- **Usuário e papel:** pessoa que acessa a organização. Perfis iniciais: Administrador (acesso completo e gestão de usuários/configurações), Gestor (operação, indicadores e dados financeiros conforme permissão) e Operador (criar/alterar demandas, programação e evidências). Permissões podem ser ajustadas por organização/usuário.
- **Perfis opcionais/futuros:** Financeiro (custos, cálculo LL, notas e recebimentos); Prestador (somente demandas atribuídas, atualizações e evidências); Cliente (acompanhar/autorizar demandas próprias, fora do MVP até confirmar necessidade).
- **Cliente e contato:** empresa atendida, endereço principal opcional (CEP, endereço, bairro, cidade e estado; D09) e contatos identificados por nome, cargo/função, telefone e e-mail.
- **Contato de cliente/local:** pessoa de referência vinculada ao cliente ou a um local específico desse cliente; contatos locais não podem apontar para locais de outra organização ou cliente. Pode haver um único contato principal por vínculo cliente ou local. Um local pode também indicar um contato responsável, que deve ser do cliente ou desse próprio local.
- **Local de atendimento:** loja, unidade ou endereço de execução, associado ao cliente; inclui nome, endereço, bairro, cidade e status (Ativo, Pausado ou Concluído); a demanda seleciona um desses locais.
- **Documento de cliente:** CPF/CNPJ numérico, validado pelos dígitos verificadores, aceitando pontuação apenas na entrada e persistindo sem máscara. CNPJ com letras não é aceito por decisão do usuário em 14/11/2026.
- **Demanda/ordem de serviço:** unidade principal do trabalho, com ID interno, origem, descrição, responsável e referências externas.
- **Solicitação comercial/oportunidade:** entrada para avaliar uma cotação, vinculada a cliente existente ou aos dados de um prospect; pode terminar sem proposta.
- **Proposta e revisão:** negociação comercial da solicitação, com itens de descrição/valor e revisões preservadas; envio e aprovação são fatos registrados manualmente.
- **Rascunho de demanda:** estado intermediário criado uma única vez pela aprovação da proposta, pendente de validação; não é demanda operacional nem entra em seus indicadores.
- **Subitem de demanda:** parte do escopo de uma demanda, com descrição/valor copiados da revisão aprovada; não tem ciclo, cliente ou autorização próprios.
- **Referência externa:** chamado/ticket, ordem de compra ou código do sistema do cliente. Pode haver mais de uma e não substitui o ID interno.
- **Orçamento e revisão:** proposta antes da demanda, valor/itens por revisão e aprovação do cliente. Aditivo posterior altera escopo/valor da demanda, com histórico e aprovação.
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
- Cliente existente pode iniciar demanda diretamente; proposta aprovada de cliente ou prospect gera uma demanda em rascunho, completada pelo Operador depois dos cadastros de cliente/local autorizados.
- Cada solicitação representa uma negociação com revisões sequenciais; a revisão aprovada gera uma demanda com subitens e não duplica demandas em tentativas repetidas.
- “Autorizado” para orçamento e autorização para emitir NF após execução são aprovações diferentes.
- “Aditivo” muda escopo/valor; “ADD” é adiantamento financeiro.
- Na planilha LL, o custo estimado é digitado manualmente; imposto/administração é considerado fixo em 28% pela empresa piloto. O resultado chamado LL significa Lucro Líquido, e a meta mínima é 20%. Pela fórmula atual, o percentual é LL dividido pelo valor aprovado.
- Ari aprovou incorporar o cálculo ao PIT Obras. Os parâmetros são da empresa piloto e devem ser configuráveis por organização; os nomes dos itens de custo ainda precisam ser definidos.
