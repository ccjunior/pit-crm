# Arquitetura técnica

## Stack aprovada

- **Back-end:** ASP.NET Core com .NET 10.
- **Front-end:** Angular, usando uma versão estável com suporte no início da implementação.
- **Banco de dados:** PostgreSQL.
- **Aplicação web:** front-end e back-end separados, comunicando-se por uma API HTTP/JSON.

## Organização do back-end

- Começar como **monólito modular**: uma aplicação de back-end organizada por áreas de negócio, sem introduzir microserviços no MVP.
- Combinar **Clean Architecture** com **Vertical Slice**. As funcionalidades ficam agrupadas por caso de uso/área (por exemplo, Demandas, Clientes, Agenda e Faturamento), mantendo as regras de negócio separadas dos detalhes de API e persistência.
- Aplicar SOLID, clean code e padrões quando trouxerem clareza ou reduzirem acoplamento. Evitar abstrações e camadas genéricas sem necessidade concreta.
- A estrutura deve favorecer manutenção e evolução gradual, sem duplicar regras entre front-end e back-end.

## Autenticação, autorização e isolamento

- Usar **ASP.NET Core Identity** para gestão de usuários e autenticação por cookie.
- O cookie de autenticação deve ser `HttpOnly`, `Secure` em produção e usar `SameSite` adequado ao domínio e ao fluxo de autenticação.
- Como cookies são enviados automaticamente pelo navegador, proteger operações de escrita contra CSRF. Se front-end e API tiverem origens diferentes, restringir CORS a origens explícitas e configurar envio de credenciais e antifalsificação corretamente.
- Preferir publicar front-end e API sob a mesma origem, usando proxy reverso para encaminhar as chamadas da API, quando a infraestrutura permitir.
- Identity autentica usuários; as permissões do produto devem ser implementadas por regras de autorização próprias. Perfis-base: Administrador, Gestor e Operador, com permissões configuráveis.
- O isolamento por organização deve ser aplicado e validado no back-end em toda consulta e alteração de dados. A interface não é uma fronteira de segurança.

### Detalhes implementados para sessão (T04)

- Cookie Identity HttpOnly, SameSite=Lax, sessão não persistente e `SecurePolicy.Always` fora do ambiente Development.
- Login e logout exigem token antifalsificação no cabeçalho `X-CSRF-TOKEN`, obtido em `GET /api/auth/csrf`. A autenticação inválida retorna mensagem genérica.
- Cinco tentativas incorretas ativam bloqueio de cinco minutos via Identity. Endpoints de API retornam 401/403 sem redirecionamento para HTML.
- Cada requisição autenticada revalida no banco se a conta e a organização continuam ativas. A organização possui `IsActive` obrigatório, padrão `true`.
- Políticas iniciais: leitura de clientes/locais para os três papéis; edição desses registros para Administrador/Gestor; gestão de equipe apenas para Administrador; operação de demandas e troca da própria senha para os três.
- `ICurrentOrganizationContext` resolve o usuário e a organização da claim protegida pelo cookie, e cada requisição confere o ID e os papéis com os dados atuais no banco. Não se aceita organização enviada em campos operacionais como contexto de autorização.
- A implementação e os testes do fluxo e da matriz estão em `.specs/features/primeira-entrega-operacional/verification.md`; endpoints operacionais aplicam essas políticas nas tarefas correspondentes.

## Diretrizes de implementação

- Definir contratos de API e modelos de entrada/saída próprios, sem expor diretamente entidades de persistência.
- Manter decisões de persistência, bibliotecas complementares e detalhes de hospedagem documentados quando forem escolhidos na estruturação do repositório.
- Registrar versões suportadas e manter dependências atualizadas conforme o ciclo de suporte de cada tecnologia.

## Versões e bibliotecas escolhidas na primeira entrega

Registradas em 05/11/2026 durante T01. A compatibilidade do Angular foi conferida na documentação oficial; o Angular 22 está em suporte ativo e aceita Node.js 24.15 ou superior na linha 24. O runtime disponível localmente é Node.js 24.15.0.

| Componente | Escolha para o projeto | Evidência local / observação |
| --- | --- | --- |
| SDK .NET | 10.0.302, `rollForward: latestFeature` | Instalado na máquina durante T01; alvo dos projetos: `net10.0` |
| Runtime .NET / ASP.NET Core | 10.0.14 | Instalado localmente |
| Angular CLI e framework | 22.2.6 | Versão estável ativa escolhida para iniciar o frontend |
| Node.js | 24.15.0 | Instalado localmente; versão mínima compatível com Angular 22.2 |
| npm | 11.12.1 | Instalado localmente; dependências frontend travadas em `frontend/package-lock.json` |
| PostgreSQL | 18.4 | Cliente e serviço local instalados; Compose usa a imagem `postgres:18.4-alpine` |
| EF Core Design | 10.0.14 | Referência privada no projeto de inicialização API; CLI local em `backend/dotnet-tools.json` |
| ASP.NET Core Identity EF | 10.0.14 | Usuários, papéis e vínculos persistidos pelo `PitCrmDbContext` |
| Provedor EF Core para PostgreSQL | Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 | Versão estável disponível para EF Core 10 |
| UI Angular | CSS da plataforma, sem biblioteca de componentes | Tokens visuais próprios alinhados à identidade Project IT; sem biblioteca de componentes |

## Identidade visual do front-end

O sistema usa CSS próprio do Angular, sem adicionar biblioteca de componentes. A imagem de referência enviada para o PIT Obras orienta a composição do painel, a navegação lateral, os cartões, as listas e a disposição dos formulários. O conteúdo e as funções fictícias de RH da imagem não são requisitos do produto.

A paleta foi conferida no site da Project IT em 14/11/2026. O dourado `#E6B033` é a cor primária e `#E0C585` é uma variação de apoio. A interface mantém superfície clara e texto carvão para leitura, com o dourado em ações principais, foco e destaques. Estados de sucesso e erro preservam cores semânticas próprias.

O shell tem navegação lateral em desktop e navegação compacta em dispositivos móveis. As telas de clientes/locais e criação de demanda mantêm os campos, permissões e estados definidos nas fontes de produto; a restilização não introduz métricas ou dados ilustrativos sem origem na API.

O Compose destina-se apenas ao desenvolvimento local. A senha padrão é de conveniência local e não é configuração de produção. A persistência de dados usa o volume `pitcrm_postgres_data`.

## Referências oficiais consultadas em 13/10/2026

- [.NET releases and support](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)
- [Angular versioning and releases](https://angular.dev/reference/releases)
- [SameSite cookies in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-10.0)
- [Prevent CSRF attacks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)

Versões consultadas em 05/11/2026:

- [.NET releases and support](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [Angular releases and support](https://angular.dev/reference/releases)
- [Angular version compatibility](https://angular.dev/reference/versions)
- [Npgsql EF Core 10 release notes](https://www.npgsql.org/efcore/release-notes/10.0.html)
