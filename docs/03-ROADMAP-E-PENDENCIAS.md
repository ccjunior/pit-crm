# Roadmap e decisões pendentes

## Etapa 0 — Fechar as últimas regras com a empresa piloto

As respostas atuais definem o fluxo básico, aprovaram o cálculo LL dentro do PIT-CRM e estabeleceram os perfis iniciais. Restam detalhes de parametrização e acesso:

- Completar com Ari a matriz de ações por perfil, especialmente aprovações e dados financeiros. Para a primeira entrega, o usuário confirmou em 04/11/2026: Administrador e Gestor cadastram/editam clientes e locais; Operador consulta esses cadastros e cria/edita demandas. A gestão da equipe cabe ao Administrador.
- Confirmar se há necessidade no piloto de um perfil Financeiro separado; Prestador e Cliente são possibilidades futuras, não requisitos do MVP.
- Quais categorias de custo devem aparecer como itens manuais na composição do custo estimado? O cálculo será integrado ao PIT-CRM, com custo total manual, 28% de imposto/administração e LL em valor e percentual.
- Quais clientes não usam número de chamado e quais referências substituem esse código?
- Quais períodos e quais demandas antigas devem ser importados para o histórico faturado?

### Regras já respondidas

- Primeira entrega detalhada em [primeira-entrega-operacional](../.specs/features/primeira-entrega-operacional/spec.md), com as respostas confirmadas em [context.md](../.specs/features/primeira-entrega-operacional/context.md).
- Cliente, local, descrição e origem são obrigatórios na abertura; responsável operacional interno é opcional.
- A organização e o primeiro administrador são criados de forma controlada; o administrador cadastra a equipe.

- Visita técnica é o primeiro atendimento; execução agendada ocorre depois da autorização do orçamento.
- Cancelamento é quando o cliente cancela solicitação/autorização. Pausa é adiamento pelo cliente ou pela empresa, inclusive por falta de mão de obra ou material. Retrabalho é atuação em garantia.
- A operação pode criar e alterar demandas; podem ser definidas permissões específicas para outros perfis.
- O cálculo será incorporado ao PIT-CRM. O custo estimado é informado manualmente; LL significa Lucro Líquido e a meta mínima informada é 20%.
- A empresa piloto considera fixos os 28% de imposto/administração.
- Uma equipe ou prestador por demanda é a regra do piloto.
- Perfis-base: Administrador (acesso completo e gestão de usuários/configurações), Gestor (operação, indicadores e dados financeiros conforme permissão) e Operador (cria/altera demandas, agenda e registra evidências).
- Toda a equipe operacional pode criar e alterar demandas; permissões devem poder ser ajustadas por perfil/usuário.
- Financeiro pode ser perfil opcional; Prestador pode acessar somente demandas atribuídas em etapa futura. Acesso de cliente fica fora do MVP até confirmação de necessidade.

## Etapa 1 — Núcleo operacional

- Organização, usuários e permissões básicas.
- Clientes, contatos e locais.
- Cadastro e busca de demandas com ID interno e referências externas.
- Visita técnica inicial e execução agendada como compromissos distintos.
- Orçamento, aprovação, retificação e aditivos com histórico.
- Atribuição de uma equipe/prestador por demanda.
- Atualizações, pausa, cancelamento, conclusão e evidências do serviço.
- Retrabalho em garantia vinculado à demanda original.

## Etapa 2 — Faturamento e controle financeiro operacional

- Autorização de faturamento após a execução.
- Registro de notas fiscais, envio, previsão e recebimento.
- Adiantamentos recebidos e pagamentos a prestadores.
- Despesas de materiais associadas à demanda.
- Acompanhamento de atrasos e relatórios de faturamento histórico.
- Cálculo LL no PIT-CRM, configurável por organização: custo estimado manual por itens, imposto/administração (28% para o piloto), LL em valor e percentual e meta mínima de 20% sobre o valor aprovado.

## Etapa 3 — Reutilização em outros segmentos de serviços

- Configurar campos, etapas, parâmetros financeiros e modelos por organização.
- Apoiar outros fluxos de trabalho de serviços sem customizar o código para cada empresa.
- Acrescentar manutenção recorrente e contratos preventivos com base no uso do piloto.

## Etapa 4 — Integrações e automações

- Avaliar integração com WhatsApp, e-mail e sistemas dos clientes.
- Automatizar lembretes e transições repetitivas.
- Considerar IA depois que os registros operacionais e o histórico forem confiáveis.

## Critério para avançar do piloto

A equipe consegue cadastrar e localizar demandas, identificar responsável e próxima ação, acompanhar visita, execução e evidências, reconhecer o que está pronto para faturar, localizar recebimentos atrasados e consultar o histórico por cliente/local/período. A operação não depende de manter o mesmo dado manualmente em várias planilhas.
