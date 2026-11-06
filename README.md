# PIT-CRM

Plataforma de gestão comercial e operacional para empresas de serviços. O primeiro caso de validação é uma empresa de serviços e reformas empresariais, com foco em chamados, execução em campo, faturamento e recebimentos.

## Documentação

- [Direção do produto e escopo do MVP](docs/01-DIRECAO-E-ESCOPO.md)
- [Fluxo operacional e modelo conceitual](docs/02-FLUXO-E-MODELO.md)
- [Roadmap e decisões pendentes](docs/03-ROADMAP-E-PENDENCIAS.md)
- [Arquitetura técnica](docs/04-ARQUITETURA-TECNICA.md)
- [Levantamento do processo com a empresa piloto](LEVANTAMENTO-PROCESSO-OPERACIONAL.md)
- [Trabalho com agentes](docs/05-TRABALHO-COM-AGENTES.md)
- [Skills locais, origem e adaptações](docs/06-SKILLS-E-ORIGEM.md)
- [Decisões arquiteturais](docs/decisions/README.md)

## Orientação dos agentes

Comece por [AGENTS.md](AGENTS.md) e pelo [estado atual](.specs/STATE.md). As oito skills locais ficam em `.agents/skills/`; as especificações de funcionalidades serão mantidas em `.specs/features/`. A origem e os hashes das skills estão em [skills.lock.json](skills.lock.json).

## Primeira entrega em especificação

A [primeira entrega operacional](.specs/features/primeira-entrega-operacional/spec.md) reúne acesso à organização, equipe, clientes, locais e demandas. O [desenho técnico](.specs/features/primeira-entrega-operacional/design.md), o [plano de tarefas](.specs/features/primeira-entrega-operacional/tasks.md) e as [decisões confirmadas](.specs/features/primeira-entrega-operacional/context.md) estão disponíveis. As APIs de clientes e locais incluem cadastro, consulta e edição; a API de demandas inclui criação transacional. As telas de clientes/locais estão implementadas; o formulário e a consulta de demandas e a interface de equipe seguem no plano. A API de equipe permite listar, criar e editar contas, com proteção contra remoção do último Administrador ativo.

## Desenvolvimento local

Requisitos instalados e registrados em [arquitetura técnica](docs/04-ARQUITETURA-TECNICA.md): SDK/runtime .NET 10, Node.js 24.15.0 ou superior na linha 24, npm 11.12.1 e Docker com Compose. O serviço PostgreSQL desta configuração usa a imagem 18.4.

### PostgreSQL pelo Docker Compose

No PowerShell, a partir da raiz do projeto:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
docker compose up -d postgres
$env:PITCRM_CONNECTION_STRING = "Host=localhost;Port=5433;Database=pitcrm_dev;Username=pitcrm;Password=pitcrm_local_dev"
$env:PITCRM_TEST_CONNECTION = $env:PITCRM_CONNECTION_STRING
Set-Location backend
dotnet restore PitCrm.sln
dotnet tool restore
dotnet ef database update --project src/PitCrm.Infrastructure --startup-project src/PitCrm.Api
dotnet test PitCrm.sln
dotnet run --project src/PitCrm.Api
```

O arquivo `.env` é lido pelo Docker Compose para configurar o container PostgreSQL; a API .NET e os comandos do Entity Framework **não** leem esse arquivo. As variáveis `$env:PITCRM_CONNECTION_STRING` e `$env:PITCRM_TEST_CONNECTION` acima são definidas no processo do PowerShell e valem apenas naquele terminal. Mantenha a connection string coerente com `PITCRM_DB_NAME`, `PITCRM_DB_USER`, `PITCRM_DB_PASSWORD` e `PITCRM_DB_PORT` do `.env`. Com os valores padrão do `.env.example`, a porta publicada é `5433`.

Se você usar um PostgreSQL instalado diretamente no computador, não inicie o serviço `postgres` do Compose; defina `PITCRM_CONNECTION_STRING` com a porta, o banco e as credenciais dessa instalação. A porta padrão de uma instalação local costuma ser `5432`; use as credenciais reais dela.

Execute a API, testes e migrações no mesmo terminal onde definiu as variáveis. `PITCRM_TEST_CONNECTION` pode apontar para o mesmo banco local: cada teste cria um schema temporário, aplica as migrações nele e remove somente esse schema ao terminar.

Em outro terminal, valide a API e inicie o Angular:

```powershell
Invoke-RestMethod http://localhost:5006/api/health
Set-Location frontend
npm ci
npm start
```

O Angular fica em `http://localhost:4200` e encaminha `/api` para a API local em `http://localhost:5006`. O PostgreSQL do Compose fica disponível em `localhost:5433`; a porta evita conflito com uma instalação local padrão em `5432`. Para a imagem PostgreSQL 18, o volume nomeado `pitcrm_postgres_data` é montado em `/var/lib/postgresql`, caminho que mantém o diretório de dados versionado da imagem. `docker compose down` interrompe o PostgreSQL sem remover esse volume.

As ferramentas de migração ficam no manifesto local `backend/dotnet-tools.json`. O projeto inclui o `DbContext` e as migrações de organização, conta, clientes, locais, demandas e troca obrigatória de senha inicial. A configuração local acima aplica as migrações já existentes. Para **criar uma migração depois de alterar o modelo**, use o comando abaixo no mesmo terminal:

```powershell
dotnet tool restore
dotnet ef migrations add MinhaAlteracao --project src/PitCrm.Infrastructure --startup-project src/PitCrm.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/PitCrm.Infrastructure --startup-project src/PitCrm.Api
```

Se abrir um terminal novo, defina novamente `PITCRM_CONNECTION_STRING` antes de iniciar a API, executar testes ou aplicar migrações.

Não coloque a connection string ou senha em arquivos versionados. O valor de exemplo usa a senha local do `.env.example`; ajuste-o ao ambiente ativo. Os testes PostgreSQL criam um schema isolado e o removem ao terminar.

### Provisionar a primeira organização e Administrador

Depois de aplicar a migração, mantenha o mesmo ID da organização em qualquer repetição do comando. A senha é solicitada sem eco no terminal e fica apenas no ambiente do processo:

```powershell
$env:PITCRM_SETUP_ORGANIZATION_ID = [guid]::NewGuid().ToString()
$env:PITCRM_SETUP_ORGANIZATION_NAME = "Empresa piloto"
$env:PITCRM_SETUP_ADMIN_NAME = "Nome do administrador"
$env:PITCRM_SETUP_ADMIN_EMAIL = "admin@empresa.example"
$securePassword = Read-Host "Senha inicial do Administrador" -AsSecureString
$env:PITCRM_SETUP_ADMIN_PASSWORD = [System.Net.NetworkCredential]::new("", $securePassword).Password
try {
    dotnet run --project backend/src/PitCrm.Api -- provision
} finally {
    Remove-Item Env:PITCRM_SETUP_ADMIN_PASSWORD
}
```

O comando não escreve a senha em logs nem em arquivos. Guarde o `OrganizationId` informado na saída para uma repetição futura. Ao repetir com o mesmo ID e e-mail, ele informa que a organização já está provisionada e mantém a credencial existente.

Depois de provisionar, inicie a API e o Angular conforme as instruções acima e abra `http://localhost:4200/entrar`. Entre com o e-mail e a senha definidos no provisionamento. A primeira conta Administrador usa essa senha; as contas da equipe cadastradas depois recebem senha temporária e precisam trocá-la no primeiro acesso.

## Estado atual

O escopo da primeira entrega foi aprovado em 05/11/2026. T01–T17 e T24 foram implementadas localmente; a interface de equipe e senha inicial está em T19. Consulte [.specs/STATE.md](.specs/STATE.md) para andamento por tarefa. As regras do levantamento vêm das respostas do responsável pela operação; decisões ainda abertas continuam identificadas como propostas ou perguntas.
