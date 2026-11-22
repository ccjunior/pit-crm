# Roadmap e decisões pendentes

## Etapa 0 — Fechar as últimas regras com a empresa piloto

As respostas atuais definem o fluxo básico, aprovaram o cálculo LL dentro do PIT Obras e estabeleceram os perfis iniciais. Restam detalhes de parametrização e acesso:

- Completar com Ari a matriz de ações por perfil, especialmente aprovações e dados financeiros. Para a primeira entrega, o usuário confirmou em 04/11/2026: Administrador e Gestor cadastram/editam clientes e locais; Operador consulta esses cadastros e cria/edita demandas. A gestão da equipe cabe ao Administrador.
- Confirmar se há necessidade no piloto de um perfil Financeiro separado; Prestador e Cliente são possibilidades futuras, não requisitos do MVP.
- Quais categorias de custo devem aparecer como itens manuais na composição do custo estimado? O cálculo será integrado ao PIT Obras, com custo total manual, 28% de imposto/administração e LL em valor e percentual.
- Quais clientes não usam número de chamado e quais referências substituem esse código?
- Quais períodos e quais demandas antigas devem ser importados para o histórico faturado?

### Diretriz geral e propostas de configuração

Em 20/11/2026, o usuário orientou que o produto considere o entendimento de uma empresa do ramo de construção civil e reformas, em vez de assumir que os detalhes de uma única operação se aplicam a todas. Esta orientação autoriza recomendações generalistas; não transforma dados desconhecidos da empresa piloto em fatos. As propostas abaixo são defaults de produto para revisão, não regras aprovadas nem alegações de que toda empresa do setor opera da mesma forma.

| Tema | Proposta de default para empresas de construção e reformas | Limite e impacto |
| --- | --- | --- |
| Perfis e permissões | Administrador gerencia organização/equipe e possui acesso completo; Gestor conduz operação e recebe permissões financeiras conforme configuração; Operador cria/atualiza demandas, agenda e evidências, consulta clientes/locais e não acessa custos, margem ou recebíveis por padrão. Um perfil Financeiro pode ser habilitado quando houver separação de funções; ele trabalha com custos, LL, notas e recebimentos sem alterar execução. | A aprovação do orçamento e a autorização de faturamento são atos do cliente. Usuários internos registram quem autorizou, quando e a evidência; por padrão, somente Administrador/Gestor ou Financeiro autorizado pode registrar esses marcos. Regras por organização exigem implementação futura de permissões configuráveis. |
| Composição de custo estimado | Oferecer catálogo editável por organização com categorias iniciais sugeridas: materiais, mão de obra/equipe, deslocamento/logística, equipamentos/locações, serviços de terceiros e outros. Cada lançamento guarda descrição e valor; impostos/administração e meta de LL permanecem parâmetros separados do custo. | Não decompor automaticamente o pacote fechado de um prestador. Categorias são classificações opcionais do custo estimado, não uma afirmação sobre como cada contrato é formado. Os 28% e 20% registrados são parâmetros informados para a empresa piloto, não defaults universais do setor. |
| Referências externas | Manter o ID interno do PIT Obras e permitir várias referências opcionais por demanda, com tipo e valor: chamado/OS do cliente, ordem de compra, código de sistema do cliente, contrato/cotação e outra referência. Busca deve aceitar qualquer referência. | Referências não substituem o ID interno e não são globalmente únicas. A relação entre referência, cliente e local deve ser preservada; não presumir que todo cliente usa chamado nem impor unicidade sem validação de dados reais. |
| Importação de histórico faturado | Fazer importação assistida por arquivo com prévia, erros por linha e origem/rastreabilidade. Sugestão inicial: importar todas as demandas abertas e valores ainda não recebidos, mesmo anteriores ao período escolhido, mais o histórico encerrado dos últimos 24 meses; permitir escolher a data de corte antes da carga. | Vinte e quatro meses são uma sugestão operacional inicial, não prazo legal ou regra universal. Confirmar volume, qualidade das planilhas, necessidade de relatórios e período desejado antes de executar carga. Evitar sobrescrever registros operacionais já conferidos. |
| Momento de liberar o financeiro | Entregar primeiro o ciclo operacional (visita técnica, orçamento/revisões e autorização do cliente, execução/evidências) com marcos separados; em seguida implementar custos, faturamento e recebimentos com permissões próprias. | Preserva o fluxo confirmado e evita expor informação financeira antes de definir as permissões. O financeiro depende da confirmação dos defaults de acesso e da política de importação. |

Com a diretriz geral dada pelo usuário, a primeira tarefa de implementação decorrente desta orientação foi permitir referências externas múltiplas e tipadas por demanda. T25 está implementada localmente; consulte [especificação e verificação](../.specs/features/demand-external-references/spec.md). A solução segue o modelo já previsto: referências opcionais, pesquisáveis, vinculadas à demanda e sem substituir o ID interno; tipos são texto livre. As propostas de permissões financeiras, categorias de custo e importação histórica permanecem recomendações sujeitas a revisão antes dessas funcionalidades.

### Regras já respondidas

- Primeira entrega detalhada em [primeira-entrega-operacional](../.specs/features/primeira-entrega-operacional/spec.md), com as respostas confirmadas em [context.md](../.specs/features/primeira-entrega-operacional/context.md).
- Cliente, local, descrição e origem são obrigatórios na abertura; responsável operacional interno é opcional.
- A organização e o primeiro administrador são criados de forma controlada; o administrador cadastra a equipe.

- Visita técnica é o primeiro atendimento; execução agendada ocorre depois da autorização do orçamento.
- Cancelamento é quando o cliente cancela solicitação/autorização. Pausa é adiamento pelo cliente ou pela empresa, inclusive por falta de mão de obra ou material. Retrabalho é atuação em garantia.
- A operação pode criar e alterar demandas; podem ser definidas permissões específicas para outros perfis.
- O cálculo será incorporado ao PIT Obras. O custo estimado é informado manualmente; LL significa Lucro Líquido e a meta mínima informada é 20%.
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
- Cálculo LL no PIT Obras, configurável por organização: custo estimado manual por itens, imposto/administração (28% para o piloto), LL em valor e percentual e meta mínima de 20% sobre o valor aprovado.

## Expansão futura — depende de nova validação do posicionamento

O posicionamento aprovado para o PIT Obras é construção civil e reformas. A expansão para outros segmentos de serviços não faz parte do escopo atual e depende de nova validação.

- Configurar campos, etapas, parâmetros financeiros e modelos por organização.
- Apoiar outros fluxos de trabalho de serviços sem customizar o código para cada empresa.
- Reavaliar manutenção recorrente e contratos preventivos com base no uso do piloto.

## Etapa 4 — Integrações e automações

- Avaliar integração com WhatsApp, e-mail e sistemas dos clientes.
- Automatizar lembretes e transições repetitivas.
- Considerar IA depois que os registros operacionais e o histórico forem confiáveis.

## Critério para avançar do piloto

A equipe consegue cadastrar e localizar demandas, identificar responsável e próxima ação, acompanhar visita, execução e evidências, reconhecer o que está pronto para faturar, localizar recebimentos atrasados e consultar o histórico por cliente/local/período. A operação não depende de manter o mesmo dado manualmente em várias planilhas.
