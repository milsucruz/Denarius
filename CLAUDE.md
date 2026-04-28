# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build
dotnet build -c Release

# Test
dotnet test
dotnet test tests/Denarius.Api.Tests/Denarius.Api.Tests.csproj   # single project
dotnet test --no-build --verbosity detailed

# Clean
dotnet clean
```

## Architecture

Clean architecture with a strict layer dependency rule (outer layers depend on inner, never reverse):

```
Denarius.Api          → entry point (ASP.NET Core Web API)
Denarius.Application  → use cases; depends on Domain, Infrastructure, CrossCutting
Denarius.Infrastructure → data access, external services; depends on Domain, CrossCutting
Denarius.Domain       → entities, value objects, domain logic; depends only on CrossCutting
Denarius.CrossCutting → shared building blocks and shared kernel (no outward deps)
Denarius.IoC          → DI wiring; should depend on all layers EXCEPT Api
```

**Known issue:** `Denarius.IoC.csproj` currently references `Denarius.Api`, creating a circular dependency with `Denarius.Api` referencing `Denarius.IoC`. This must be resolved before the solution builds cleanly — remove the `Denarius.Api` reference from `Denarius.IoC`.

Test projects live under `tests/`: `Denarius.Api.Tests`, `Denarius.Domain.Tests`, `Denarius.Integration.Tests`.

## Code Quality

`Directory.Build.props` enforces globally:
- `TreatWarningsAsErrors=true` — all warnings break the build
- `EnforceCodeStyleInBuild=true` — `.editorconfig` style rules are compile-time errors
- `Nullable=enable` — nullability must be explicit
- `AnalysisLevel=latest-recommended` — Roslyn analyzers run on every build

Key `.editorconfig` rules: file-scoped namespaces required, private fields must use `_` prefix, no expression-bodied members for multi-statement methods.

## Key Packages (centrally managed via Directory.Packages.props)

| Concern | Library |
|---|---|
| ORM | EF Core 10 + SQL Server |
| Messaging | MassTransit 8 + RabbitMQ |
| Mapping | Mapster |
| Validation | FluentValidation |
| Resilience | Polly |
| Observability | OpenTelemetry + Jaeger, Serilog + Seq |
| API docs | Scalar.AspNetCore |
| Testing | xUnit, FluentAssertions, Testcontainers (MSSQL, RabbitMQ) |

Add new packages only via `Directory.Packages.props` (centralized version management); reference them without a version in individual `.csproj` files.

## Secrets & Settings

`appsettings.*.json` is gitignored except `appsettings.Development.json`, which is committed. Never put secrets in committed settings files.

## Development Rules

- One file per request, full content, no omissions
- Exact solution path for every file generated
- English only: names, code, comments
- No business logic in CrossCutting
- No direct Infrastructure reference from entry points (Api, Workers, Consumers)
- Balance is never stored — always projected via SUM of ledger entries

## Current Phase

Phase 1 — Foundation (in progress)
Next: docker-compose.yml

## Implementation Phases

1. Foundation — solution structure, tooling, docker
2. CrossCutting — Result<T>, DomainError, base contracts
3. Ledger Domain — aggregates, value objects, unit tests
4. Ledger Infra + Application
5. Api + IoC
6. Outbox + Messaging
7. Identity
8. Notifications
9. Analytics
10. Integration tests + CI
11. Observability
12. Cloud
