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

A [primeira entrega operacional](.specs/features/primeira-entrega-operacional/spec.md) reúne acesso à organização, equipe, clientes, locais e demandas. O [desenho técnico](.specs/features/primeira-entrega-operacional/design.md), o [plano de tarefas](.specs/features/primeira-entrega-operacional/tasks.md) e as [decisões confirmadas](.specs/features/primeira-entrega-operacional/context.md) estão disponíveis. As APIs de clientes e locais incluem cadastro, consulta e edição; a API de demandas inclui criação transacional. As telas de clientes/locais estão implementadas; o formulário e a consulta de demandas e a gestão de equipe seguem no plano.

## Desenvolvimento local

Requisitos instalados e registrados em [arquitetura técnica](docs/04-ARQUITETURA-TECNICA.md): SDK/runtime .NET 10, Node.js 24.15.0 ou superior na linha 24, npm 11.12.1 e Docker com Compose. O serviço PostgreSQL desta configuração usa a imagem 18.4.

No PowerShell, a partir da raiz do projeto:

```powershell
Copy-Item .env.example .env
docker compose up -d postgres
$env:PITCRM_CONNECTION_STRING = "Host=localhost;Port=5433;Database=pitcrm_dev;Username=pitcrm;Password=pitcrm_local_dev"
$env:PITCRM_TEST_CONNECTION = $env:PITCRM_CONNECTION_STRING
dotnet restore backend/PitCrm.sln
dotnet test backend/PitCrm.sln
dotnet run --project backend/src/PitCrm.Api
```

Em outro terminal, valide a API e inicie o Angular:

```powershell
Invoke-RestMethod http://localhost:5006/api/health
Set-Location frontend
npm ci
npm start
```

O Angular fica em `http://localhost:4200` e encaminha `/api` para a API local em `http://localhost:5006`. O PostgreSQL do Compose fica disponível em `localhost:5433`; a porta evita conflito com uma instalação local padrão em `5432`. `npm run build` compila o frontend. `docker compose down` interrompe o PostgreSQL e mantém o volume local.

As ferramentas de migração ficam no manifesto local `backend/dotnet-tools.json`. O projeto inclui o `DbContext` e as migrações de organização, conta, clientes, locais e demandas. Configure a connection string pela variável local e rode no PowerShell:

```powershell
Set-Location backend
$env:PITCRM_CONNECTION_STRING = "Host=localhost;Port=5433;Database=pitcrm_dev;Username=pitcrm;Password=pitcrm_local_dev"
dotnet tool restore
dotnet ef migrations add MinhaAlteracao --project src/PitCrm.Infrastructure --startup-project src/PitCrm.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/PitCrm.Infrastructure --startup-project src/PitCrm.Api
```

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

## Estado atual

O escopo da primeira entrega foi aprovado em 05/11/2026. T01–T13 foram implementadas localmente; a próxima tarefa é integrar o formulário de demanda. Consulte [.specs/STATE.md](.specs/STATE.md) para andamento por tarefa. As regras do levantamento vêm das respostas do responsável pela operação; decisões ainda abertas continuam identificadas como propostas ou perguntas.
