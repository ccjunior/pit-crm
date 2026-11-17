# AD-002 — Código sequencial de demanda por organização

## Estado e origem

Aceita em 17/11/2026, conforme solicitação do usuário no refinamento da listagem T21. O UUID continua sendo a identidade interna da demanda.

## Contexto

O código público derivado do UUID era extenso e difícil de citar. A tela precisa oferecer uma referência curta, legível e pesquisável, sem expor a chave técnica como código de atendimento. A numeração deve respeitar o limite organizacional do produto.

## Decisão

Cada organização terá sua própria sequência crescente, exibida como `DEM-` seguido do número com no mínimo quatro dígitos (por exemplo, `DEM-0042`). A demanda persiste o número inteiro em `ReferenceNumber`; a combinação de organização e número é única. A gravação do contador e da demanda ocorre na mesma transação. O UUID permanece nas rotas e como chave interna; a busca aceita o código legível.

Na migração, demandas existentes recebem números pela ordem de criação e UUID como desempate. O contador é inicializado no maior número atribuído a cada organização.

## Consequências

- Usuários podem informar o código em conversas e localizar a demanda sem conhecer o UUID.
- Organizações não compartilham sequência nem revelam o volume total de outras organizações.
- A atualização transacional do contador serializa apenas cadastros concorrentes da mesma organização; o índice único protege a integridade.
- A migração precisa ser aplicada antes do deploy desta versão. O código tem largura mínima de quatro dígitos e continua válido quando ultrapassar `DEM-9999`.
- Exclusões futuras podem deixar lacunas; o código é uma referência de negócio, não uma prova de quantidade contínua.

## Referências

- Pedido do usuário de 17/11/2026 para substituir o código longo por sequência por organização.
- [Listagem e detalhe de demandas (T21)](../../.specs/features/primeira-entrega-operacional/tasks.md)
