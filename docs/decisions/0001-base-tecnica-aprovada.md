# AD-001 — Base técnica do PIT-CRM

## Estado e origem

Aceita. Registro criado em 04/11/2026 para consolidar uma decisão preexistente, já documentada em [04-ARQUITETURA-TECNICA.md](../04-ARQUITETURA-TECNICA.md). Esta organização documental não constitui uma nova escolha de tecnologia.

## Contexto

O produto acompanha a operação de empresas de serviços e precisa preservar responsabilidades de negócio, permissões e isolamento entre organizações. A implementação ainda não foi iniciada.

## Decisão

- Backend ASP.NET Core com .NET 10; frontend Angular separado; PostgreSQL.
- Monólito modular combinando Clean Architecture e Vertical Slice, com funcionalidades agrupadas por caso de uso ou área de negócio.
- ASP.NET Core Identity com autenticação por cookie HttpOnly, Secure em produção, SameSite adequado e proteção contra CSRF nas operações de escrita.
- Permissões do produto e isolamento por organização aplicados no backend.
- Preferência por frontend e API sob a mesma origem com proxy reverso, quando a infraestrutura permitir.
- Contratos de entrada e saída próprios, sem exposição direta das entidades de persistência.

## Consequências

A estrutura inicial precisa permitir evolução por módulos e casos de uso sem duplicar regras entre frontend e backend. Bibliotecas complementares, versão estável do Angular, detalhes de persistência e hospedagem serão documentados quando efetivamente escolhidos, conforme a arquitetura existente.

As verificações de acesso devem cobrir permissões e isolamento entre organizações. O tamanho da estrutura e a adoção de padrões devem acompanhar as necessidades concretas do MVP.

## Referências

- [Direção e escopo](../01-DIRECAO-E-ESCOPO.md)
- [Arquitetura técnica](../04-ARQUITETURA-TECNICA.md)
