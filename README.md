# AutoReparos.App

> Módulo principal do sistema AutoReparos — API REST (.NET 10), camada de domínio (DDD), infraestrutura (EF Core + PostgreSQL) e frontend Angular 19.

## 📋 Visão Geral

Este repositório contém toda a aplicação principal do AutoReparos:

| Projeto | Descrição |
|---|---|
| `AutoReparos.Domain` | Entidades, Value Objects, Enums e contratos de repositório (DDD puro) |
| `AutoReparos.Application` | Casos de uso, DTOs, serviços de aplicação e validação |
| `AutoReparos.Infra` | DbContext EF Core, repositórios PostgreSQL, Identity, JWT |
| `AutoReparos.API` | Endpoints Minimal API, Swagger, OpenTelemetry, DI |
| `AutoReparos.Web` | Frontend Angular 19 (Standalone Components + Signals) |
| `AutoReparos.Domain.Tests` | Testes unitários do domínio (xUnit + FluentAssertions) |
| `AutoReparos.Application.Tests` | Testes de aplicação (xUnit + NSubstitute) |
| `AutoReparos.IntegrationTests` | Testes de integração (Testcontainers PostgreSQL) |

## 🏗️ Arquitetura

```
Clean Architecture + DDD + Vertical Slice

API → Application → Domain ← Infra
                              ↕
                          PostgreSQL 16
```

## 🚀 Desenvolvimento Local

### Backend

```bash
# Restaurar e compilar
dotnet build AutoReparos.App.slnx

# Executar testes
dotnet test AutoReparos.App.slnx

# Executar API localmente
dotnet run --project AutoReparos.API/AutoReparos.API.csproj
```

### Frontend

```bash
cd AutoReparos.Web
yarn install
yarn build
yarn test --watch=false
```

## 🧪 Stack de Testes

- **xUnit** — Framework de testes
- **FluentAssertions** — Asserções fluentes
- **NSubstitute** — Mocking
- **Testcontainers** — PostgreSQL real para integration tests
- **Bogus** — Geração de dados fictícios

## 📦 Integração com Repositório Pai

Este repositório é registrado como **git submodule** no [repositório pai AutoReparos](https://github.com/Grupo78-PosTech-15SOAT/AutoReparos) em `submodules/AutoReparos.App/`.

## 📄 Licença

Projeto acadêmico — FIAP Tech Challenge (SOAT) — Grupo 78.
