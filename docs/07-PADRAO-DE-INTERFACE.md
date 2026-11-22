# Padrão de telas de cadastro e edição

## Regra

Toda tela de criação ou edição de registros do PIT Obras deve seguir um padrão único de interação e estrutura. O formulário abre em um diálogo ou painel dedicado, separado da listagem e do quadro de trabalho. É proibido inserir o formulário diretamente no fluxo da página ou expandi-lo dentro de cards/listas.

Criação e edição da mesma entidade reutilizam a mesma estrutura e os mesmos componentes de campo. Variam o título, os valores iniciais e o texto da ação principal. A edição deve apresentar os dados atuais e respeitar as operações e permissões disponíveis na API.

## Estrutura obrigatória

1. Cabeçalho com contexto curto, título claro para a ação e botão de fechar acessível; o acionador inicial recebe foco conforme o padrão do diálogo.
2. Descrição breve quando necessária para orientar o preenchimento.
3. Campos agrupados por assunto, em grade responsiva e com rótulos persistentes.
4. Validação e erros próximos aos campos ou em aviso identificável por tecnologia assistiva.
5. Rodapé separado visualmente, com ação secundária para cancelar/fechar e uma ação principal para salvar/criar.
6. Em telas estreitas, o painel ocupa a largura disponível, os campos passam a uma coluna e os botões permanecem visíveis e utilizáveis.

O diálogo deve ser modal e acessível: nome associado ao título, fechamento por botão e Escape, foco contido enquanto aberto e devolvido ao acionador após fechar. Clique no fundo pode fechar quando isso não descartar alterações silenciosamente; se houver dados alterados, solicitar confirmação ou preservar o formulário.

## Aplicação

O cadastro de Clientes e a criação/edição de Demandas são referências existentes para esse padrão. Novos módulos devem segui-las. Telas preexistentes devem ser alinhadas quando forem tocadas em uma entrega autorizada, sem refatorações alheias ao escopo.

O cadastro de solicitação comercial/proposta deve usar o mesmo painel dedicado de criar/editar. Kanban, filtros e lista continuam na página ao fundo; o formulário não deve empurrar nem deslocar esses conteúdos.

## Listagens e comandos

- Ações primárias e secundárias usam `.primary-button` e `.secondary-button` globais, que seguem a paleta e as medidas do PIT Obras. Módulos não devem redefinir localmente suas cores ou forma.
- Diretórios usam painel, borda, raio e sombra do padrão de Clientes, com filtros agrupados acima dos resultados e adaptação responsiva.
- Paginação informa “Mostrando início–fim de total”, oferece seletor “Por página” com tamanhos coerentes com a API e usa botões compactos anterior/próxima com símbolos e rótulos acessíveis.
- Alternância de visualização (quando existir) segue o controle segmentado de Clientes: fundo neutro, opção selecionada em superfície branca e estado acessível exposto.
- Campos mantêm respiro entre linhas e entre o rótulo e o controle; campos de texto longo ocupam a grade inteira. Em telas estreitas, a grade passa a uma coluna.
- Texto de controles e placeholders de filtros herdam a tipografia global, com o mesmo tamanho, peso e cor de apoio usados em Clientes.

Propostas segue esses critérios na listagem, nos comandos e na paginação; os filtros adicionais de etapa, período e cliente permanecem específicos do módulo.

## Verificação de interface

- Confirmar que criar e editar abrem a mesma família de painel e estrutura de formulário.
- Confirmar cabeçalho, fechar/cancelar, ação primária, validações, foco e fechamento por Escape.
- Conferir layout em viewport desktop e estreita; nenhum campo ou ação pode ficar inacessível por rolagem ou overflow.
- Manter campos e validações específicos do domínio, sem sacrificar este padrão por uniformidade visual.
