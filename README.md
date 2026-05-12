# PcaImporter

Ferramenta interna pra cadastro em massa de itens no Plano de Contratações Anual (PCA) do Compras.gov.br, usada pela SUBG/SMS-RJ.

## Stack

- Backend: .NET 9, ASP.NET Core, Clean Architecture (Domain, Application, Infrastructure, Api)
- Persistência: SQLite via EF Core 9
- Comunicação real-time: SignalR
- Planilhas: ClosedXML
- Frontend: React 19, Vite, TypeScript, MUI v7
- Cliente SignalR: @microsoft/signalr

## Estrutura

```
PcaImporter/
├── src/
│   ├── PcaImporter.Domain/         # Entidades, value objects, enums
│   ├── PcaImporter.Application/    # Casos de uso, interfaces, DTOs
│   ├── PcaImporter.Infrastructure/ # EF Core, HttpClient, ClosedXML
│   ├── PcaImporter.Api/            # ASP.NET Core, SignalR, endpoints REST
│   └── PcaImporter.Web/            # Vite + React 19 + TS + MUI v7
├── tests/
│   ├── PcaImporter.Domain.Tests/
│   └── PcaImporter.Application.Tests/
├── docs/
│   ├── arquitetura.md
│   └── descobertas-compras.md
└── PcaImporter.sln
```

## Pré requisitos

- .NET SDK 9.0.x
- Node.js 22+
- npm 11+

## Setup inicial

```bash
# Restore e ferramentas locais
dotnet tool restore
dotnet restore

# Dependências do front
cd src/PcaImporter.Web
npm install
cd ../..

# Cria/atualiza o banco SQLite
dotnet ef database update --project src/PcaImporter.Infrastructure --startup-project src/PcaImporter.Infrastructure
```

## Rodando em desenvolvimento

### Modo rápido (recomendado)

Da raiz do projeto:

```powershell
.\dev.ps1
```

Sobe backend e frontend em duas janelas separadas, libera as portas se estiverem em uso e abre o browser em `http://localhost:5173`. Use `Ctrl+C` em cada janela para parar, ou:

```powershell
.\dev.ps1 -Stop
```

Flags opcionais:
- `-Stop` encerra os dois e sai
- `-NoBrowser` não abre o browser automaticamente

Pra duplo clique no Explorer, use `dev.cmd` (mesmo comportamento).

### Modo manual (dois terminais)

**Terminal 1, backend:**

```bash
dotnet run --project src/PcaImporter.Api
```

API sobe em `http://localhost:5062`. OpenAPI em `http://localhost:5062/openapi/v1.json`.

**Terminal 2, frontend:**

```bash
cd src/PcaImporter.Web
npm run dev
```

Front em `http://localhost:5173`. Vite faz proxy de `/api`, `/hubs` e `/swagger` pro backend.

Acesse `http://localhost:5173` no browser.

## Build de produção

```bash
dotnet publish src/PcaImporter.Api -c Release -o ./publish
```

O target `PublishRunWebpack` no `PcaImporter.Api.csproj` roda `npm run build` automaticamente e copia o `dist/` do front pra `wwwroot/` do backend. O resultado é um único processo que serve API e SPA na mesma porta.

## EF Core

Ferramentas `dotnet-ef` instaladas como ferramenta local (ver `.config/dotnet-tools.json`). Sempre rodar com `dotnet ef ...` da raiz do repositório.

Criar nova migration:

```bash
dotnet ef migrations add NomeDaMigration --project src/PcaImporter.Infrastructure --startup-project src/PcaImporter.Infrastructure --output-dir Persistencia/Migrations
```

Aplicar:

```bash
dotnet ef database update --project src/PcaImporter.Infrastructure --startup-project src/PcaImporter.Infrastructure
```

## Convenções

- Idioma: PT-BR para nomes de pastas, classes, métodos, variáveis e comentários, exceto termos técnicos consagrados (Controller, Repository, Dto, Mapper, Handler, etc).
- Encoding UTF-8, EOL CRLF, indent 4 espaços em C# e 2 em TS/JS/JSON.
- Sem commit de tokens, cookies, CPFs ou dados pessoais reais.

## Roadmap

Ver [docs/arquitetura.md](docs/arquitetura.md).
