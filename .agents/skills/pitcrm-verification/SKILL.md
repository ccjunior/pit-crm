---
name: pitcrm-verification
description: "Verificar uma entrega do PIT-CRM contra seus critérios de aceite e registrar evidências de código, API, interface ou documentação. Use ao validar funcionalidades, revisar conclusão ou preparar transferência de trabalho; uma verificação não autoriza publicação ou alteração de ambiente externo."
metadata:
  project: PIT-CRM
  version: "1.0.0"
---

# Verificação de entregas do PIT-CRM

## Preparar a verificação

Leia [AGENTS.md](../../../AGENTS.md), a solicitação e os critérios da entrega. Localize os documentos e decisões citados. Confira o estado real dos arquivos, os comandos disponíveis e o ambiente autorizado.

Escolha verificações proporcionais ao comportamento alterado. Testes devem demonstrar os resultados especificados e detectar o comportamento incorreto correspondente. A aprovação de um build, isoladamente, não comprova uma regra de negócio.

## Selecionar os cenários relevantes

| Área alterada | O que verificar |
| --- | --- |
| Consultas, gravações, relacionamentos e arquivos | Acesso permitido na organização correta; rejeição ou ausência de exposição entre organizações; IDs e vínculos incompatíveis |
| Usuários e permissões | Sessão exigida, permissão exigida no backend e comportamento de cada perfil afetado |
| Autenticação e escritas | Cookie e antifalsificação conforme a arquitetura e o ambiente |
| Demandas, orçamentos e execução | Transições e marcos definidos em [02-FLUXO-E-MODELO.md](../../../docs/02-FLUXO-E-MODELO.md), inclusive pausas, cancelamento e retrabalho quando afetados |
| Custos e cálculo LL | Exemplos calculados a partir das entradas e parâmetros da organização; preservação da fórmula documentada; limites e arredondamento conforme o critério decidido |
| NF e recebimentos | Autorização de faturamento, datas, histórico e valores conforme os critérios da funcionalidade |
| Interface Angular | Estados alterados, submissão e erros, navegação por teclado, acesso negado e integração real ou simulada |
| Documentação e skills | Fonte, estado de decisões, caminhos, referências, nomes e integridade dos arquivos importados |

## Executar e registrar

1. Relacione cada critério ao teste, comando ou inspeção que o verifica.
2. Execute os comandos existentes apropriados à alteração. Confira se o comando selecionou testes reais.
3. Registre falhas com cenário, resultado esperado, resultado observado e evidência. Evite transformar sugestões fora do escopo em bloqueadores.
4. Diferencie resultado aprovado, reprovado e não executado. Explique impedimentos concretos.
5. Corrija problemas dentro do trabalho autorizado quando a tarefa incluir correção; em uma revisão solicitada, entregue os achados e as evidências.
6. Se houver revisor independente autorizado, identifique quem verificou e a revisão examinada. Caso o próprio autor confira a entrega, registre essa condição.

## Relatório

Registre em `verification.md`, quando houver uma funcionalidade com pasta própria, ou no relato da tarefa para alterações pequenas:

- escopo, arquivos/revisão e ambiente examinados;
- critério, procedimento, resultado e evidência;
- achados e resolução, ou pendências que permanecem;
- limites da conclusão, incluindo checks não executados;
- próximo passo quando houver trabalho incompleto.

Na fase documental, declare a ausência de runtime e testes da aplicação. Evidência local e disponibilidade de um ambiente publicado são conclusões distintas.

Exemplo de uso: “Verifique a entrega de cadastro de demandas contra os critérios da spec, incluindo tentativas de usar cliente ou local de outra organização.”
