# Clientes, locais e contatos

## Resultado

Completar o cadastro operacional de clientes e locais de atendimento, incluindo consulta de CEP, endereço detalhado, contatos e status do local, mantendo isolamento por organização.

## Fontes e autorização

- Solicitação do usuário em 14/11/2026: integração ViaCEP, nome/endereço/bairro/cidade em locais, contatos de cliente e local, status ativo/pausado/concluído, isolamento multi-tenant e normalização/validação de documento.
- Regras de acesso confirmadas em D01 da primeira entrega: Administrador e Gestor gerenciam clientes e locais; Operador consulta.
- D09 da primeira entrega: endereço principal de cliente é opcional.
- Decisão do usuário em 14/11/2026: manter CPF e CNPJ somente numéricos, normalizados para dígitos; CNPJ alfanumérico será rejeitado.

## Escopo

- Consulta de CEP no formulário do cliente e preenchimento assistido dos campos de endereço, bairro, cidade e UF; edição manual continua disponível.
- Nome do local já existe; acrescentar bairro e cidade ao endereço de atuação e status com valores Ativo, Pausado e Concluído.
- Local pode guardar opcionalmente um link HTTPS do Google Maps; quando definido, ele prevalece sobre a busca textual gerada pelo endereço.
- Contatos vinculados ao cliente e, opcionalmente, a um local desse mesmo cliente: nome, cargo/função, telefone e e-mail.
- Cada vínculo (cliente ou local) pode ter um contato principal. O principal do cliente aparece no card; principal de local fica restrito ao respectivo local.
- Local pode indicar um contato responsável já vinculado ao cliente ou ao próprio local; a referência é validada por organização/cliente. Contato em uso não pode ser excluído até ser removido da responsabilidade.
- Estado do cliente é armazenado como UF brasileira de duas letras; CEP e CPF/CNPJ têm máscara no formulário e a API valida os formatos.
- Reforçar `OrganizationId` nas entidades, filtros, buscas, paginação e referências; organização obtida da sessão autenticada.
- Normalização de CPF/CNPJ para dígitos e validação dos dígitos verificadores; aceitar pontuação usual apenas na entrada.

## Critérios de aceite

- AC-01: Ao consultar CEP válido, o formulário do cliente preenche dados de endereço disponíveis e permite ajustes manuais; CEP inválido, inexistente e falha de rede recebem mensagens distintas e não apagam dados existentes.
- AC-02: Cliente persiste CEP junto aos campos de endereço principal; endereço permanece opcional.
- AC-03: Local contém nome, endereço, bairro e cidade; status aceita somente Ativo, Pausado ou Concluído e locais existentes recebem status Ativo na migração.
- AC-04: Usuário autorizado cria, lista, edita e exclui contatos associados ao cliente ou a um local do mesmo cliente; contato expõe nome, cargo/função, telefone e e-mail; exclusão pede confirmação contextual.
- AC-05: Leituras e escritas de clientes, locais e contatos derivam organização do contexto autenticado; IDs de outra organização não revelam nem alteram dados.
- AC-06: A busca por documento encontra o cliente com ou sem pontuação na consulta e em documentos numéricos antigos formatados.
- AC-07: CPF e CNPJ numéricos são validados pelos dígitos verificadores; dados novos e atualizados persistem apenas com dígitos; CNPJ com letras é rejeitado.
- AC-08: Angular apresenta estados de carregamento, sucesso e erro da consulta CEP, rótulos acessíveis e os novos campos de locais e contatos.
- AC-09: Clientes aparecem em grade responsiva de cards em desktop e mobile; busca e paginação permanecem disponíveis. Cada card oferece Locais e Contatos e, conforme papel, Editar; contatos gerenciáveis oferecem Editar e Excluir.
- AC-10: Em cada escopo (cliente/local), no máximo um contato é marcado como principal; ao marcar outro, o anterior deixa de ser principal. A busca do cliente mostra seu contato principal.
- AC-11: Diretório oferece cards e tabela, busca com debounce, filtro de cidade, ordenação por nome/cidade e paginação com intervalo e tamanho selecionável; estados vazios diferenciam lista sem cadastros de filtro sem resultados.
- AC-12: Cards mostram documento formatado com tipo, endereço legível, quantidades de locais e contatos e WhatsApp para telefone principal. Exibem somente métricas já definidas: locais ativos e demandas no estado Recebida.
- AC-13: “Local ativo” significa `CustomerLocation.Status == Active`; Paused e Completed não entram na contagem. Total de locais inclui os três estados. A API calcula contagens agrupadas por organização e cliente.
- AC-14: Usuário pode definir o contato responsável do local entre contatos do cliente ou do próprio local; o servidor rejeita referências de outro cliente/local/organização. Cada local oferece link para abrir seu endereço no Google Maps.
- AC-15: Formulário mascara CEP, CPF e CNPJ, valida CPF/CNPJ antes do envio, seleciona UF por código e API rejeita estado fora da lista oficial. Valores legados conhecidos são normalizados para UF por migração.
- AC-16: Erro ao carregar contatos exibe erro e ação de nova tentativa, sem mensagem de lista vazia. Filtros de escopo em chips distinguem Todos, Cliente e cada local.
- AC-17: Escape e clique no backdrop fecham o drawer quando não há alterações; com alterações pendentes, pedem confirmação. Confirmação negada mantém o drawer aberto.
- AC-18: Ações Locais/Contatos têm estilo neutro; o card diferencia total de locais e quantidade ativa; zero demandas tem estilo neutro e valor positivo sinaliza atenção. Documento válido é apresentado com tipo e máscara; documento legado inválido não é exibido cru. O corpo do card abre o drawer na aba Cadastro.
- AC-19: A lista de locais evita repetir endereço quando o nome normalizado do local é igual à rua; mostra status com cor e contraste por situação, oferece atalho compacto do Maps, e usa link manual do Google Maps quando cadastrado. O link é opcional, aceita somente HTTPS em hosts de Maps reconhecidos e é isolado por organização/cliente via registro do local.
- AC-20: Chips de Todos, Cliente e local exibem contagens coerentes a partir dos contatos daquele cliente e rolam horizontalmente com muitos locais; rótulo do chip do cliente é curto. Estado vazio varia pelo escopo e oferece adicionar contato quando permitido, preservando o vínculo selecionado no formulário. O formulário sugere funções comuns sem impedir texto livre, mascara telefone brasileiro e oferece atalho WhatsApp.

## Decisão

- D-01: Manter documento somente numérico conforme escolha do usuário. Impacto: CNPJs alfanuméricos emitidos a partir de 2026 não poderão ser cadastrados; a decisão deve ser revista caso o produto precise atender essas empresas.
- D-02: Usar lista compacta no desktop e cards responsivos no mobile, com detalhes e relacionamentos do cliente em painel separado por abas. Avaliação independente recomenda priorizar comparação/densidade na busca e evitar listas aninhadas extensas nos cards.
- D-03: Após receber a referência visual do usuário em 14/11/2026, substituir o híbrido anterior por grade de cards em todas as larguras. A referência do usuário prevalece sobre a recomendação inicial de lista no desktop; busca e paginação ficam acima/abaixo da grade e os relacionamentos abrem em diálogo focado.
- D-04: Contato principal é uma marcação única por cliente ou por local. Restrição única parcial no banco preserva a regra sob concorrência; ao salvar um novo principal, o anterior no mesmo escopo é desmarcado.
- D-05: Aplicar as melhorias priorizadas pelo usuário: conteúdo principal do card abre detalhes, edição no menu de ações, contadores, WhatsApp, alternância tabela/cards, busca temporizada, filtro por cidade, ordenação suportada, paginação informativa, estado vazio e drawer lateral para detalhes/relacionamentos.
- D-06: O modelo não define tipo/status de cliente, entidade de obra, estados terminais de demandas nem última atividade operacional. Portanto, não implementar esses filtros/métricas ainda; mostrar apenas locais ativos e demandas Recebidas e usar ordenação por nome/cidade. A inclusão dos demais critérios requer regra de produto e ciclo de vida definidos.
- D-07: Contagem de locais ativos usa exclusivamente o status `Active`; locais pausados e concluídos não contam como ativos.
- D-08: Estado é persistido como UF de duas letras. A interface usa lista fechada e API rejeita texto livre; nomes completos legados conhecidos são normalizados em migração.
- D-09: A cor âmbar fica reservada para ação principal. Botões secundários usam fundo neutro; valores numéricos usam cor para distinguir zero de demanda pendente, não para sugerir estado positivo sem significado.
- D-10: O destino do Maps é opcional e fica persistido em `CustomerLocation.MapUrl`; se vazio, a interface gera uma busca com o endereço. A API aceita somente HTTPS dos hosts oficiais do Google Maps, incluindo os domínios de links curtos (`maps.app.goo.gl` e `goo.gl/maps`).
- D-11: Contagens por escopo de contato são calculadas a partir da lista completa do cliente, filtrada no servidor por organização/cliente, e aplicadas a todos os chips; grupos longos rolam horizontalmente. O campo função sugere opções usuais por `datalist`, mantendo texto livre.
