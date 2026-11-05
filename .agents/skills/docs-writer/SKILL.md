---
name: docs-writer
description: "Criar, revisar e atualizar a documentação Markdown do PIT-CRM com fontes, terminologia e links consistentes. Use para README, guias, escopo e documentação em docs; para registrar uma decisão arquitetural use create-adr e para especificar funcionalidades use tlc-spec-driven."
metadata:
  local_profile: "pitcrm-1"
  upstream_commit: "1da2195067c14ae5d44dc615b31cb26f50ebeb91"
---

# Documentação — perfil PIT-CRM

Adaptação da skill `docs-writer`, distribuída pelo Tech Leads Club. A [entrada original](UPSTREAM.md) e a [referência de estilo](references/style-guide.md) foram preservadas. Consulte os [créditos e adaptações](../../../docs/06-SKILLS-E-ORIGEM.md).

## Preparar

Leia [AGENTS.md](../../../AGENTS.md), o arquivo a alterar e suas fontes relacionadas. Identifique se a solicitação muda uma regra, registra uma decisão existente ou melhora sua apresentação. Use a linguagem e os termos do projeto.

O PIT-CRM ainda está na fase documental. Diferencie comportamento aprovado, comportamento implementado e proposta. Quando houver código, confirme os trechos relevantes antes de documentar o funcionamento.

## Escrever e sincronizar

1. Atualize o documento que é a fonte da informação. Preserve o histórico de decisões.
2. Confira as páginas que fazem referência à regra alterada; use links para evitar cópias desnecessárias.
3. Escreva em português claro. Preserve identificadores, nomes de API e cabeçalhos de arquivos usados como esquema por scripts.
4. Mantenha pendências explícitas. Uma decisão nova precisa da origem e autorização correspondentes; uma edição de texto não resolve uma dúvida de produto.
5. Use [references/style-guide.md](references/style-guide.md) quando precisar de orientação editorial, adaptando exemplos e ferramentas ao projeto real.
6. Para uma página nova, atualize o índice adequado no README ou no diretório responsável.

Use ferramentas disponíveis no ambiente para editar. Diretórios como `packages/`, arquivos `CONTRIBUTING.md` ou `docs/sidebar.json` e comandos como `npm run format` só são pertinentes se existirem no projeto e estiverem ligados à tarefa.

## Conferir a entrega

Releia as alterações, confira links locais e consistência com as fontes. Execute um formatador somente quando o projeto possuir configuração e comando adequados. Registre o resultado e qualquer limitação concreta; atualize o estado se a edição mudar o próximo trabalho.

Exemplo de uso: “Atualize o roadmap e os documentos relacionados com a decisão confirmada sobre categorias de custo, preservando as demais perguntas abertas.”
