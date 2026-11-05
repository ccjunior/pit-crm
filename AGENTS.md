# Orientação dos agentes — PIT-CRM

## Comece por aqui

O projeto está em descoberta e preparação. Ainda não há aplicação, banco provisionado ou comandos de build/teste definidos. Consulte o estado atualizado em [.specs/STATE.md](.specs/STATE.md).

1. Leia [README.md](README.md), este arquivo e o estado atual.
2. Para escopo e regras, leia [direção e escopo](docs/01-DIRECAO-E-ESCOPO.md) e [fluxo e modelo](docs/02-FLUXO-E-MODELO.md).
3. Para código e arquitetura, leia [arquitetura técnica](docs/04-ARQUITETURA-TECNICA.md).
4. Confira as [pendências do roadmap](docs/03-ROADMAP-E-PENDENCIAS.md) e carregue somente a especificação e as skills relevantes à tarefa.

## Fontes e decisões

- Os documentos de produto e arquitetura definem as regras vigentes; o [levantamento](LEVANTAMENTO-PROCESSO-OPERACIONAL.md) fornece a origem operacional.
- Registre decisões técnicas significativas em [docs/decisions](docs/decisions/README.md), com referência ao requisito e à autorização existentes. Propostas devem ter status de proposta.
- Especificações detalham uma entrega. O estado atual resume o progresso e aponta para as fontes; nenhum deles altera silenciosamente o escopo aprovado.
- Diante de conflito real entre requisitos, apresente o conflito, as opções, a recomendação e o impacto antes de mudar escopo ou arquitetura.
- Diferencie regra confirmada, proposta e pendência. Não transforme campos de modelos ou exemplos de skills em requisitos do produto.

## Arquitetura e regras essenciais

- Use a base aprovada: .NET 10, Angular e PostgreSQL; monólito modular com Clean Architecture e Vertical Slice. Registre versões e bibliotecas efetivamente escolhidas ao iniciar a implementação.
- Autenticação: ASP.NET Core Identity com cookie HttpOnly. Permissões, isolamento por organização e proteção das escritas pertencem ao backend, conforme o documento de arquitetura.
- Preserve os marcos e conceitos do modelo: aprovação de orçamento, autorização de NF, aditivo, adiantamento e retrabalho. Consulte as regras completas na documentação de produto.
- Priorize gestão, operação, faturamento e recebimento conforme o roadmap. Parâmetros financeiros pertencem à organização.

## Seleção de skills

| Trabalho | Skill local |
| --- | --- |
| Especificar, dividir em tarefas e retomar uma funcionalidade | [tlc-spec-driven](.agents/skills/tlc-spec-driven/SKILL.md) |
| Definir ou revisar fronteiras entre módulos | [modular-design-principles](.agents/skills/modular-design-principles/SKILL.md) |
| Registrar uma decisão arquitetural já tomada | [create-adr](.agents/skills/create-adr/SKILL.md) |
| Criar ou atualizar documentação | [docs-writer](.agents/skills/docs-writer/SKILL.md) |
| Escrever ou alterar código com escopo focado | [coding-guidelines](.agents/skills/coding-guidelines/SKILL.md) |
| Implementar API, domínio e persistência | [pitcrm-backend](.agents/skills/pitcrm-backend/SKILL.md) |
| Implementar a interface Angular | [pitcrm-frontend](.agents/skills/pitcrm-frontend/SKILL.md) |
| Verificar uma entrega e registrar evidências | [pitcrm-verification](.agents/skills/pitcrm-verification/SKILL.md) |

Use a entrada local das skills adaptadas. As condições do projeto prevalecem sobre exemplos e procedimentos genéricos dos arquivos de referência importados. Origem, versões e adaptações estão em [docs/06-SKILLS-E-ORIGEM.md](docs/06-SKILLS-E-ORIGEM.md).

## Execução e continuidade

- Conclua o trabalho já autorizado. Consulte as decisões existentes antes de perguntar novamente. Resolva escolhas técnicas reversíveis dentro dos padrões documentados.
- Para funcionalidades com várias etapas, use `.specs/features/<funcionalidade>/` conforme o [guia](docs/05-TRABALHO-COM-AGENTES.md). Crie cada documento quando houver conteúdo real.
- Cada tarefa deve indicar resultado, dependências, área de atuação, critérios de aceite e verificação. Atualize o andamento antes de encerrar ou transferir o trabalho.
- Quando a delegação estiver autorizada e disponível, defina responsáveis, arquivos sob responsabilidade de cada agente, dependências e quem integra. Evite edições concorrentes dos mesmos arquivos.
- Autorizações existentes continuam válidas dentro do seu escopo. A instalação de skills não autoriza commits, push, deploy ou mudanças em ambientes externos; execute essas ações quando estiverem autorizadas pelo usuário.
- Use comandos existentes e confirme os diretórios antes de executá-los. Após criar a aplicação, documente no README os comandos reais de desenvolvimento, build, testes e migrações.

## Critério de conclusão

Verifique os critérios de aceite com evidências proporcionais à alteração. Para mudanças em código, execute os testes e checks relevantes disponíveis. Para documentação, confira fontes, links, consistência e status das decisões. Registre limitações de execução e diferencie implementação, verificação local e validação de ambiente publicado.

Ao terminar, atualize o estado e os documentos afetados e informe o resultado, as verificações e as pendências concretas. Os detalhes do processo e os exemplos estão em [docs/05-TRABALHO-COM-AGENTES.md](docs/05-TRABALHO-COM-AGENTES.md).
