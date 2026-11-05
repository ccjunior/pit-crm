# Skills locais e origem dos arquivos

## Instalação de 04/11/2026

As skills ficam em `.agents/skills/`, com escopo local ao PIT-CRM. Foram baixadas do repositório [Tech Leads Club / agent-skills](https://github.com/tech-leads-club/agent-skills), no commit `1da2195067c14ae5d44dc615b31cb26f50ebeb91`, usando o instalador do Codex.

Os pacotes incluem os scripts, referências e modelos presentes nas respectivas pastas de origem. O arquivo [skills.lock.json](../skills.lock.json) registra origem, revisão, adaptações e hashes SHA-256 dos arquivos instalados. Ele é um inventário local; não é um lockfile gerenciado pela CLI do Tech Leads Club.

## Catálogo adotado

| Skill | Uso | Tratamento local |
| --- | --- | --- |
| `tlc-spec-driven` | Especificação, tarefas, implementação e continuidade | Entrada adaptada; materiais originais preservados |
| `modular-design-principles` | Responsabilidades e contratos entre módulos | Pacote original |
| `create-adr` | Registro de decisões arquiteturais | Pacote original; destino definido no guia do projeto |
| `docs-writer` | Escrita e revisão de documentação | Entrada adaptada; referência original preservada |
| `coding-guidelines` | Implementação focada e verificável | Pacote original |
| `pitcrm-backend` | Implementação de funcionalidades da API e domínio | Criada para o PIT-CRM |
| `pitcrm-frontend` | Implementação de funcionalidades Angular | Criada para o PIT-CRM |
| `pitcrm-verification` | Verificação de entregas com evidências | Criada para o PIT-CRM |

## Adaptações

### tlc-spec-driven

A entrada local aplica o fluxo Specify → Design → Tasks → Execute de forma proporcional ao trabalho. O arquivo `UPSTREAM.md`, dentro da skill, preserva a entrada original para comparação. Scripts e referências não foram alterados.

- Autorizações já concedidas são respeitadas; questões de produto e conflitos reais continuam sendo encaminhados ao usuário.
- Uma especificação ou tarefa não concede, por si só, autorização para commit ou ações externas.
- A delegação depende de autorização e disponibilidade; uma revisão realizada pelo próprio autor é identificada como tal.
- Os validadores originais dependem dos formatos e pré-condições originais. O modo com provas em `HEAD` exige Git, commits autorizados, comandos de build/teste disponíveis e os demais pré-requisitos do pacote.
- O perfil padrão do PIT-CRM permite evidência local por critério sem obrigar commits ou revisão por subagentes. Nesse perfil, use `verification.md` e registre que o gate completo original não foi executado. Isso não equivale a uma aprovação desse gate.
- O índice de decisões em `.specs/STATE.md` aponta para ADRs e usa IDs `AD-NNN`, mantendo compatibilidade com referências do pacote sem duplicar a decisão completa.

### docs-writer

A entrada local usa a estrutura e as ferramentas disponíveis no PIT-CRM. Remove suposições sobre `packages/`, `CONTRIBUTING.md`, `docs/sidebar.json` e `npm run format`. A entrada original está em `UPSTREAM.md`; `references/style-guide.md` foi preservado e é usado quando compatível com a linguagem e as convenções locais.

## Créditos e licenças

Crédito ao **Tech Leads Club** pela curadoria e distribuição dos cinco pacotes importados. A `tlc-spec-driven` identifica Felipe Rodrigues como autor; `coding-guidelines` identifica `ale` e a fonte “Karpathy Guidelines”. Os metadados recebidos foram preservados nas entradas originais.

O [aviso de licença original](third-party/agent-skills-LICENSE.txt) foi incluído integralmente. O repositório distingue o software sob MIT do conteúdo de skills dos mantenedores sob CC-BY-4.0, ressalvadas indicações individuais. As entradas originais de `tlc-spec-driven` e `create-adr` declaram CC-BY-4.0; as demais entradas selecionadas não declaram um campo de licença próprio. Consulte os arquivos e os avisos originais ao redistribuir.

As modificações locais são as descritas acima. As três skills com prefixo `pitcrm-` são instruções próprias deste projeto, baseadas na documentação aprovada.

## Verificação da instalação

Conferência realizada em 04/11/2026:

- As oito entradas passaram no `quick_validate.py` da skill `skill-creator` do Codex. A dependência PyYAML foi preparada em diretório temporário, sem adicionar dependências à aplicação.
- Os 36 arquivos das skills instaladas correspondem aos hashes SHA-256 de `skills.lock.json`.
- Os 31 arquivos originais, incluindo as duas entradas preservadas em `UPSTREAM.md`, correspondem aos hashes dos blobs Git da revisão de origem.
- Foram conferidos 74 links locais em 21 documentos e entradas de skills.
- Os oito scripts Python importados passaram por análise de sintaxe; não foram executados nesta instalação.

Esses resultados verificam estrutura, referências, origem e formato. A validação de comportamento das skills será complementada pelo uso em tarefas reais; ainda não existe aplicação para testar.

## Atualização futura

1. Escolha uma revisão explícita do repositório de origem e leia as alterações relevantes.
2. Compare os pacotes com o inventário local. Preserve as adaptações e as instruções próprias.
3. Atualize a cópia original e reaplique as adaptações necessárias nas duas entradas modificadas.
4. Valide nomes, descrições, referências e comportamento; atualize os hashes e a revisão em `skills.lock.json`.

Evite sobrescrever as pastas adaptadas diretamente com um atualizador genérico. No Codex, as skills locais devem estar disponíveis no próximo turno; se não aparecerem, reabra o projeto.
