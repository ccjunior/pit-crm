---
name: pitcrm-frontend
description: "Implementar ou alterar telas, formulários, navegação e consumo da API no frontend Angular do PIT-CRM. Use em funcionalidades visuais e fluxos de interface; para regras do backend ou mudanças no banco use pitcrm-backend."
metadata:
  project: PIT-CRM
  version: "1.0.0"
---

# Frontend do PIT-CRM

## Contexto necessário

Leia [AGENTS.md](../../../AGENTS.md), [arquitetura técnica](../../../docs/04-ARQUITETURA-TECNICA.md), a especificação da entrega e os contratos de API relacionados. Consulte [escopo](../../../docs/01-DIRECAO-E-ESCOPO.md) e [fluxo](../../../docs/02-FLUXO-E-MODELO.md) para termos e comportamento operacional.

Confira a versão e os padrões Angular realmente adotados, os componentes reutilizáveis e os recursos visuais disponíveis. A versão exata, a biblioteca de componentes e a identidade visual ainda precisam ser registradas ao iniciar a aplicação; exemplos de skills não definem essas escolhas.

## Construir o fluxo

1. Identifique o resultado da tela, as ações disponíveis e os dados que a API fornece.
2. Defina os estados relevantes: carregando, vazio, sucesso, erro de validação, erro de comunicação e acesso negado. Use os estados que o fluxo realmente exige.
3. Organize a funcionalidade por área de negócio, seguindo os padrões existentes de componentes, serviços e navegação. Separe detalhes visuais do acesso à API.
4. Use os contratos reais. Quando a API ainda não estiver disponível, identifique qualquer dado simulado como recurso de desenvolvimento e registre a integração pendente.
5. Integre a sessão por cookie e o mecanismo de antifalsificação acordado com a API. A visibilidade de botões por permissão complementa a autorização do backend.
6. Use os nomes e marcos definidos no modelo operacional. Apresente valores e datas de forma consistente com as regras e os contratos estabelecidos.
7. Confira teclado, rótulos, foco, mensagens e comportamento responsivo dos estados alterados. Compare com a referência visual fornecida, quando houver.

## Evidências de entrega

Registre telas e estados implementados, contratos consumidos, verificações disponíveis e limitações. Diferencie validação com dados simulados de integração real. Ao comparar capturas, use as mesmas dimensões de viewport da referência.

Exemplo de uso: “Crie o formulário Angular de demanda conforme a especificação e o contrato da API, incluindo estados de erro e cliente sem locais cadastrados.”
