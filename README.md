# AutoReparos.App - Aplicação Principal (API, Frontend & Domínio)

> **Projeto:** AutoReparos - Sistema Integrado de Oficina Mecânica  
> **Fase:** Fase 3 Tech Challenge (13SOAT / 15SOAT FIAP)  
> **Componente:** Repositório 4/4 da Arquitetura Multi-Repo  
> **Stack:** C# .NET 10, ASP.NET Core Minimal APIs, Entity Framework Core, PostgreSQL 16, Angular 19 (Signals & Standalone), OpenTelemetry, Docker.  
> **Repositório Oficial:** [Grupo78-PosTech-15SOAT/AutoReparos.App](https://github.com/Grupo78-PosTech-15SOAT/AutoReparos.App)

---

## 1. Visão Geral

O **AutoReparos.App** é o repositório central contendo toda a lógica de negócio, APIs REST, persistência de dados e aplicação web de interface com o usuário da oficina mecânica:

| Projeto | Camada / Descrição |
|---|---|
| `AutoReparos.Domain` | Entidades puras, Value Objects, Enums, regras de negócio e interfaces de repositório (DDD) |
| `AutoReparos.Application` | Casos de uso, DTOs, orquestração de serviços de aplicação e validação |
| `AutoReparos.Infra` | `AppDbContext` EF Core, repositórios PostgreSQL, Identity, geração de JWT e integrações |
| `AutoReparos.API` | Endpoints Minimal APIs, Swagger, injeção de dependência e OpenTelemetry |
| `AutoReparos.Web` | Frontend moderno em Angular 19 com Standalone Components e Angular Signals |
| `AutoReparos.Domain.Tests` | 113 testes unitários de regras de domínio (xUnit + FluentAssertions) |
| `AutoReparos.Application.Tests` | 123 testes unitários de casos de uso (xUnit + NSubstitute) |
| `AutoReparos.IntegrationTests` | 67 testes de integração ponta a ponta com **Testcontainers PostgreSQL** |

---

## 2. Arquitetura e Estrutura de Pastas

A aplicação segue rigorosamente os princípios de **Clean Architecture** e **Vertical Slice**:

```
AutoReparos.App/
├── AutoReparos.Domain/             # Núcleo de regras de negócio sem dependências externas
├── AutoReparos.Application/        # Orquestração de Use Cases e DTOs
├── AutoReparos.Infra/              # Entity Framework Core, PostgreSQL e Identity
├── AutoReparos.API/                # Minimal APIs, middlewares e Dockerfile
│   └── Dockerfile                  # Multi-stage build .NET 10 SDK & ASP.NET Runtime
├── AutoReparos.Web/                # Frontend Angular 19 (Signals & Standalone)
│   ├── Dockerfile                  # Multi-stage build Node.js 22 + Nginx unprivileged
│   └── nginx.conf                  # Configuração SPA na porta 8080
├── AutoReparos.Domain.Tests/       # Suíte de testes unitários do Domínio
├── AutoReparos.Application.Tests/  # Suíte de testes unitários da Aplicação
├── AutoReparos.IntegrationTests/   # Testes de integração com Testcontainers
├── docker/                         # Configurações de containers e observabilidade
│   ├── servers.json                # Pré-configuração do pgAdmin 4
│   └── observability/              # OpenTelemetry Collector, Prometheus, Loki e Grafana
├── docker-compose.yml              # Orquestração completa local (API + Web + DB + Otel)
├── .github/workflows/ci.yml        # Pipeline CI/CD GitHub Actions
└── AutoReparos.App.slnx            # Solution .NET 10
```

---

## 3. Execução Local com Docker Compose

O repositório disponibiliza um ambiente completo pronto para execução local via Docker Compose:

```bash
# Subir toda a aplicação (PostgreSQL, API, Web, pgAdmin e Stack de Observabilidade)
docker-compose up -d --build
```

### Portas e Serviços Disponíveis

| Serviço | Container | URL de Acesso | Credenciais Padrão |
|---|---|---|---|
| **API REST & Swagger** | `autoreparos-app-api` | [http://localhost:8080/swagger](http://localhost:8080/swagger) | — |
| **Frontend Web** | `autoreparos-app-web` | [http://localhost:4200](http://localhost:4200) | — |
| **pgAdmin 4** | `autoreparos-app-pgadmin` | [http://localhost:5050](http://localhost:5050) | `admin@autoreparos.com` / `Admin@123` |
| **Grafana** | `autoreparos-app-grafana` | [http://localhost:3000](http://localhost:3000) | `admin` / `admin` |
| **Jaeger Traces** | `autoreparos-app-jaeger` | [http://localhost:16686](http://localhost:16686) | — |
| **Prometheus** | `autoreparos-app-prometheus` | [http://localhost:9090](http://localhost:9090) | — |
| **PostgreSQL** | `autoreparos-app-postgres` | `localhost:5432` | `admin` / `Admin@123` |

Para encerrar o ambiente:
```bash
docker-compose down
```

---

## 4. Execução Manual para Desenvolvimento

### Backend (.NET 10)
```bash
# Restaurar e compilar a solution
dotnet build AutoReparos.App.slnx

# Executar a API localmente
dotnet run --project AutoReparos.API/AutoReparos.API.csproj
```

### Frontend (Angular 19)
```bash
cd AutoReparos.Web
yarn install
yarn start
```

---

## 5. Suíte de Testes Automatizados (303 Testes)

A aplicação conta com 303 testes automatizados (100% de aprovação):

```bash
# Executar todos os testes
dotnet test AutoReparos.App.slnx

# Executar testes unitários de Domínio (113 testes)
dotnet test AutoReparos.Domain.Tests/AutoReparos.Domain.Tests.csproj

# Executar testes unitários de Aplicação (123 testes)
dotnet test AutoReparos.Application.Tests/AutoReparos.Application.Tests.csproj

# Executar testes de integração com Testcontainers (67 testes)
dotnet test AutoReparos.IntegrationTests/AutoReparos.IntegrationTests.csproj

# Executar testes unitários do Frontend Web
cd AutoReparos.Web && yarn test --watch=false
```

---

## 6. Pipeline de CI/CD

O workflow [`.github/workflows/ci.yml`](.github/workflows/ci.yml) executa a validação paralela:
1. **Backend:** Compilação .NET 10, testes unitários de domínio e aplicação, testes de integração com PostgreSQL e build da imagem Docker.
2. **Frontend:** Instalação via Yarn (`--frozen-lockfile`), compilação de produção do Angular e testes unitários.
- Todas as GitHub Actions são fixadas em hashes SHA de 40 caracteres conforme os padrões de segurança do SonarCloud.

---

## 7. Integração com o Repositório Pai

Este repositório é registrado como **git submodule** no [repositório pai AutoReparos](https://github.com/Grupo78-PosTech-15SOAT/AutoReparos) no caminho `submodules/AutoReparos.App/`.
