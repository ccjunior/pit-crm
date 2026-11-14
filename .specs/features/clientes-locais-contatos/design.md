# Desenho: clientes, locais e contatos

## Persistência e domínio

- `Customer`: adicionar `PostalCode`; preservar `Locality` no modelo/API atual, apresentando-o como Bairro no formulário para evitar uma quebra desnecessária.
- `CustomerLocation`: adicionar `Neighborhood`, `City` e estado `Status`; nome e endereço já existem. Valores persistidos em enum/string controlada: Active, Paused, Completed; criação inicia Active.
- Nova entidade `CustomerContact`: sempre contém `OrganizationId` e `CustomerId`; `LocationId` nulo representa contato geral do cliente e preenchido representa contato daquele local. Associação local deve ter chave composta que prove mesmo cliente e organização.
- `IsPrimary`: opcional logicamente (falso por padrão), com no máximo um principal por cliente e um por local, garantido por índices únicos parciais. A seleção de novo principal desmarca o anterior dentro da transação.
- `CustomerLocation.ResponsibleContactId`: opcional, com FK composta para contato do mesmo tenant/cliente. A API permite contato geral do cliente ou contato específico desse local; contato usado como responsável não pode ser excluído.
- `CustomerLocation.MapUrl`: link HTTPS opcional para Google Maps; persistido no registro do local, sem coordenadas geográficas adicionais. A lista usa o link salvo ou gera uma busca textual pelo endereço.
- Todos os endpoints recebem organização apenas de `ICurrentOrganizationContext`, nunca do corpo ou query do cliente.

## API

- Atualizar DTOs e requests de cliente/local.
- Criar endpoints sob o cliente para contatos, aceitando `LocationId` opcional e oferecendo filtro por local; validar a relação cliente-local na organização atual.
- CPF e CNPJ são tratados em uma regra de domínio compartilhada: aceita números com pontos, barra, hífen e espaços como apresentação, valida formato/checksum e persiste somente dígitos. A busca compara dígitos e ignora pontuação tanto na consulta quanto em documentos numéricos legados.
- A decisão mantém CNPJ numérico e rejeita letras, mesmo que o novo padrão nacional possa emiti-las.

## Frontend

- Consultar ViaCEP no Angular após validação de oito dígitos, sem bloquear edição manual e sem substituir endereço atual automaticamente.
- Aplicar os campos retornados a logradouro, bairro/localidade, cidade e UF; CEP armazenado somente com dígitos.
- Formulários de contato separados por contexto visual, consumindo os mesmos contratos de API.
- Após a referência visual enviada pelo usuário, resultados podem ser vistos em cards responsivos ou tabela. Card apresenta avatar com cor determinística, documento formatado, endereço, contato principal/WhatsApp, locais ativos, demandas Recebidas e botões contadores de locais/contatos; o conteúdo abre detalhes e Editar fica no menu contextual.
- Busca atualiza após 300 ms; filtro/ordenação suportados por cidade/nome, alternância tabela/cards e paginação com intervalo e seletor de tamanho. Drawer lateral mantém o contexto da lista ao abrir cadastro, locais ou contatos.
- Critérios de status/tipo do cliente, obra ativa, demanda aberta e última atividade ficam pendentes de definição do modelo. Evitar projetar números sobre estados inexistentes.
- “Ativo” significa status do local igual a `Active`; `Paused` e `Completed` não contam. O drawer filtra contatos por chips e a aba Locais mostra responsável e link de mapa. Escape e clique fora usam a mesma confirmação de descarte.
- CEP segue `00000-000`; documento tem máscara CPF/CNPJ e checksum; estado é selecionado por código UF.
- Link do Google Maps é validado na API por HTTPS e lista permitida de hosts oficiais; a interface valida o mesmo formato antes do envio.
- Exclusão de contato apresenta confirmação contextual com nome do contato e do cliente/local; a API aplica a política de gestão, antifalsificação e filtro por organização antes de remover.

## Migração

- Uma migração aditiva adiciona colunas e tabela de contatos; dados existentes de local recebem status Active.
- Não executar migração no ambiente local durante esta entrega sem necessidade explícita; registrar comando e build da migração gerada.
