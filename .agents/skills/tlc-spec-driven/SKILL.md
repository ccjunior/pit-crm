---
name: tlc-spec-driven
description: "Especificar, planejar, executar e verificar funcionalidades com critérios observáveis, tarefas e continuidade em .specs. Use ao definir uma entrega, dividir trabalho ou retomar uma funcionalidade do PIT-CRM. Para edição documental isolada use docs-writer; para análise de fronteiras use modular-design-principles."
license: CC-BY-4.0
metadata:
  author: Felipe Rodrigues - github.com/felipfr
  version: "4.1.0"
  local_profile: "pitcrm-1"
  upstream_commit: "1da2195067c14ae5d44dc615b31cb26f50ebeb91"
---

# Desenvolvimento por especificação — perfil PIT-CRM

Adaptação da skill de Felipe Rodrigues distribuída pelo Tech Leads Club. A [entrada original](UPSTREAM.md), os scripts e as referências foram preservados. O fluxo de execução local é o descrito aqui; consulte as [adaptações e créditos](../../../docs/06-SKILLS-E-ORIGEM.md).

## Começar ou retomar

Leia [AGENTS.md](../../../AGENTS.md), [.specs/STATE.md](../../../.specs/STATE.md) e os arquivos da funcionalidade ativa. Confira os arquivos reais e, quando disponível, o estado Git. Carregue somente as fontes e referências necessárias à entrega.

Identifique o resultado e a autorização existentes. Continue o trabalho já autorizado sem pedir a mesma aprovação novamente. Pergunte quando houver decisão de produto aberta, conflito com uma fonte ou mudança de escopo/arquitetura que impeça uma escolha segura. Registre a resposta e sua origem; não invente uma aprovação.

## Dimensionar o trabalho

- Mudança pequena: objetivo, critérios e evidências podem ficar no relato; evite documentos sem utilidade concreta.
- Funcionalidade com várias etapas: crie `.specs/features/<funcionalidade>/spec.md` e `tasks.md`.
- Decisões técnicas relevantes: acrescente `design.md` com contratos, dados, responsabilidades e dependências.
- Perguntas com respostas relevantes: use `context.md` ou a seção de decisões da spec.
- Após executar: registre as evidências em `verification.md`.

Crie os arquivos quando existir conteúdo. O nome de uma pasta ou um exemplo de funcionalidade não constitui autorização de implementação.

## Specify → Design → Tasks → Execute

### Especificar

Registre problema, resultado, fontes, escopo autorizado, critérios de aceite observáveis e pendências. Use IDs estáveis para requisitos e critérios; uma formulação como “Quando X ocorrer, o sistema deve apresentar Y” ajuda a tornar o resultado verificável.

Leia [specify.md](references/specify.md) se precisar do formato detalhado e [discuss.md](references/discuss.md) para decisões em aberto. Reaproveite respostas nos documentos do projeto. Separe fatos verificáveis por leitura de decisões que cabem ao usuário.

### Desenhar quando necessário

Defina módulo responsável, contratos, dados, estados e dependências dentro da arquitetura aprovada. Use [design.md](references/design.md) como material de apoio. Registre decisões de projeto em ADRs e atualize o índice `Decisions` de `.specs/STATE.md` com um link para o registro.

### Dividir em tarefas

Cada tarefa entrega um resultado, identifica dependências e área de atuação, referencia os critérios e informa como verificá-los. Atualize o andamento e o próximo passo. Use [tasks.md](references/tasks.md) para detalhamento ou compatibilidade com os validadores originais.

### Executar e verificar

Execute as tarefas dentro da autorização existente e dos padrões do projeto. Use `pitcrm-backend`, `pitcrm-frontend` e `coding-guidelines` quando pertinentes. Verifique os critérios com a `pitcrm-verification`; feche achados do trabalho autorizado e registre limitações.

Uma tarefa só pode ser declarada verificada quando houver evidência correspondente. O padrão local permite evidências sobre os arquivos atuais sem exigir commit. Ações Git e externas seguem as autorizações do usuário.

## Referências e scripts originais

As referências [implement.md](references/implement.md), [validate.md](references/validate.md) e [memory.md](references/memory.md) trazem procedimentos completos do pacote original. Ao consultá-las, aplique o perfil local quanto a autorizações, commits, delegação e condições de execução. Leia o conteúdo antes de escolher um script.

Os scripts originais exigem Python 3.8+ e seus próprios formatos de documentos. Os validadores com execução/provas exigem também Git, commits autorizados, comandos reais de build/teste e os demais pré-requisitos descritos em `UPSTREAM.md`.

- Use `validate_spec.py` e `validate_tasks.py` para documentos deliberadamente escritos no esquema original, com os requisitos adicionais que esses scripts verificam.
- Use `validate_tasks.py --execute`, `check_commit.py`, `check_proofs.py` e `validate_verification.py` somente quando o trabalho estiver autorizado e preparado para o fluxo completo original. A aprovação da spec não concede permissão automática para commit.
- Quando o perfil local for utilizado, registre a validação por critério em `verification.md` e informe que o gate completo original não foi executado. Isso não é um resultado aprovado daquele gate.
- Resolva `references/` e `scripts/` a partir da pasta desta skill. Os dados da funcionalidade pertencem à raiz do PIT-CRM; use `--root` quando o script aceitar e o diretório atual for outro.

## Delegação e transferência

Delegue apenas quando houver autorização e ferramenta disponível. Defina objetivo, dependências, fontes, arquivos de responsabilidade e formato de retorno por agente. Faça a revisão independente quando autorizada; identifique uma conferência feita pelo próprio autor como tal.

Antes de transferir ou encerrar, atualize tarefas e `.specs/STATE.md` com fontes, evidências, pendências e o próximo passo. Priorize links para os documentos completos em vez de copiar as regras.
