# Trabalho com agentes

## Objetivo

Permitir que um agente entenda o trabalho autorizado, encontre suas fontes, execute uma entrega delimitada e deixe informações suficientes para a continuidade por outro agente.

## Onde cada informação fica

| Informação | Local |
| --- | --- |
| Entrada e instruções permanentes | [AGENTS.md](../AGENTS.md) |
| Escopo, regras, pendências e arquitetura | Documentos 01 a 04 e levantamento operacional |
| Decisões técnicas e justificativas | [decisions/README.md](decisions/README.md) |
| Procedimentos reutilizáveis por tarefa | `.agents/skills/<skill>/SKILL.md` |
| Requisitos e andamento de uma funcionalidade | `.specs/features/<funcionalidade>/` |
| Resumo para retomar o projeto | [.specs/STATE.md](../.specs/STATE.md) |
| Versão e integridade das skills | [skills.lock.json](../skills.lock.json) |

As regras de negócio são mantidas nos documentos de produto. Skills e especificações apontam para essas fontes e acrescentam apenas o procedimento ou os detalhes da entrega.

## Abrir um trabalho

1. Identifique objetivo e autorização na solicitação atual e nas decisões já registradas.
2. Consulte o estado, o roadmap e os documentos relacionados à entrega.
3. Carregue a skill adequada e somente as referências necessárias.
4. Confira o estado real dos arquivos e, quando existir, do repositório Git.
5. Registre dúvidas de produto que afetem a entrega. Pendências independentes não impedem o restante do trabalho autorizado.

Uma edição pequena pode usar critérios e verificação no próprio relato. Para uma funcionalidade com várias etapas, use a `tlc-spec-driven` adaptada.

## Documentos de uma funcionalidade

Crie a pasta quando a funcionalidade entrar em trabalho. Use nomes como `cadastro-de-demandas`.

| Arquivo | Conteúdo e momento de criação |
| --- | --- |
| `spec.md` | Objetivo, fontes, escopo, critérios observáveis e questões abertas |
| `context.md` | Respostas e decisões específicas, quando houver necessidade |
| `design.md` | Contratos, dados, estados e dependências quando existir decisão técnica relevante |
| `tasks.md` | Tarefas, dependências, andamento e critérios de verificação |
| `verification.md` | Evidências da execução, resultados e limitações |

Capturas e relatórios adicionais são criados quando usados pela validação. A ausência de um documento opcional é normal. Não preencha aprovações, resultados ou decisões com exemplos fictícios.

## Contrato de tarefa

Cada tarefa precisa responder:

- **Objetivo:** qual resultado observável ela entrega?
- **Fontes:** quais requisitos, decisões e arquivos a orientam?
- **Dependências:** o que precisa existir antes e quais tarefas ela libera?
- **Responsabilidade:** quem executa e qual área ou conjunto de arquivos está envolvido?
- **Aceite:** quais comportamentos concretos precisam estar presentes?
- **Verificação:** qual comando, teste ou inspeção comprova cada comportamento?
- **Andamento:** pendente, em execução, bloqueada ou concluída; evidência e próximo passo.

Exemplo de solicitação: “Use `$tlc-spec-driven` para especificar o cadastro de demandas com base nos documentos 01 e 02. Registre as pendências que dependem do piloto.” Essa solicitação autoriza a especificação; a implementação depende do escopo autorizado na conversa.

Para alterações de código já autorizadas, o agente pode detalhar tarefas e executá-las sem solicitar novamente uma autorização já concedida. Se surgir uma mudança de produto ou arquitetura, deve apresentar o conflito e obter a decisão correspondente.

## Trabalho delegado

Quando a delegação estiver autorizada, cada agente recebe o contrato da tarefa e o mínimo de fontes necessário. O coordenador define as interfaces compartilhadas e integra os resultados. Um relatório de transferência deve conter arquivos alterados, critérios atendidos, comandos executados, resultados, pendências e próximo passo.

A disponibilidade de uma skill não cria um subagente automaticamente. A configuração e a autorização da ferramenta de agentes continuam sendo necessárias.

## Verificar e encerrar

- Use critérios de aceite definidos antes da implementação e testes que detectem comportamentos incorretos.
- Se a tarefa afetar acesso, verifique permissões e isolamento entre organizações no backend.
- Para mudanças visuais, confira os estados relevantes e a referência disponível; registre dimensões das capturas quando houver comparação.
- Registre comandos reais e resultados. “Não executado” e “falhou” são estados distintos.
- Atualize o documento da funcionalidade e o estado do projeto com referências às evidências.

Na etapa atual, ainda não existem comandos de build ou teste da aplicação. Eles serão registrados quando a estrutura executável for criada. Os scripts das skills importadas possuem condições próprias descritas na entrada adaptada da `tlc-spec-driven`.

## Retomar um trabalho

Leia `AGENTS.md`, `.specs/STATE.md` e os arquivos da funcionalidade ativa. Confira o conteúdo real antes de confiar em um resumo. Reconcilie tarefas, alterações locais e evidências; prossiga pelo primeiro trabalho pendente dentro da autorização existente.
