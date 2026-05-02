# Clean Architecture Rules

These rules govern layer boundaries, dependency direction, and structural constraints across the Denarius codebase.

---

## 1. Layer Dependency Direction

Dependencies flow inward only. Outer layers depend on inner layers; inner layers never reference outer layers.

```
Api → Application → Domain → CrossCutting
         ↓                ↑
    Infrastructure ───────┘
IoC → Application, Infrastructure, Domain, CrossCutting (NOT Api)
```

Violations are build errors — do not suppress them with `#pragma` or project reference hacks.

---

## 2. Denarius.CrossCutting

- Contains only shared building blocks with zero outward dependencies: `Result<T>`, `DomainError`, base interfaces, extension methods.
- **No business logic.** No domain concepts, no persistence concerns, no HTTP references.
- Every other project may reference CrossCutting; CrossCutting references nothing inside this solution.

---

## 3. Denarius.Domain

- Contains aggregates, entities, value objects, domain events, domain services, and repository **interfaces**.
- Depends only on CrossCutting.
- No EF Core, no HTTP, no MassTransit, no infrastructure SDK of any kind.
- Never reference `Denarius.Infrastructure`, `Denarius.Application`, or `Denarius.Api` from here.

---

## 4. Denarius.Infrastructure

- Contains EF Core `DbContext`, migrations, repository implementations, external service clients, and ACL translators.
- Depends on Domain and CrossCutting.
- Implements interfaces declared in Domain — never exposes its own abstractions back to the domain.
- Never reference `Denarius.Application` or `Denarius.Api` from here.

---

## 5. Denarius.Application

- Contains command handlers, query handlers, FluentValidation validators, and application-level interfaces (e.g., `ICurrentUser`).
- Depends on Domain, Infrastructure interfaces (via Domain abstractions), and CrossCutting.
- **No domain logic inside handlers.** If a handler contains an `if` that enforces a business rule, move it to the aggregate.
- Never reference EF Core, MassTransit internals, or any HTTP concern directly.

### 5.1 ICurrentUser

`ICurrentUser` is an application-level interface that abstracts the identity of the authenticated caller. It is declared in Application and implemented in Api (via `HttpContext`), wired through IoC.

```csharp
// Application layer — declaration
public interface ICurrentUser
{
    UserId Id { get; }
    bool IsAuthenticated { get; }
}

// Api layer — implementation
internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public UserId Id =>
        UserId.From(Guid.Parse(accessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!));

    public bool IsAuthenticated =>
        accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}

// IoC layer — registration
services.AddHttpContextAccessor();
services.AddScoped<ICurrentUser, CurrentUser>();
```

Handlers receive `ICurrentUser` via constructor injection and never access `HttpContext` directly.

---

## 6. Denarius.Api

- Entry point: **conventional ASP.NET Core controllers**, middleware, and Swagger/OpenAPI config.
- Depends on Application and CrossCutting only.
- **Never reference Infrastructure directly.** All infrastructure is wired through IoC.
- Controllers are thin: deserialize the request, map to a command/query, call the handler, map the result to an HTTP response. No business logic.
- **Minimal API is not used in Denarius.** The project has multiple bounded contexts, a strict layering convention, and per-resource controller grouping — conventional controllers fit this structure better and offer cleaner `[Authorize]`, filter, and Swagger integration.

### 6.1 Controller Conventions

- One controller per aggregate resource: `LedgerController`, `AccountController`.
- Controllers inherit from `ControllerBase` (never `Controller` — no view support needed).
- Always `sealed`. Always inject `ISender` (MediatR) as the sole dependency — never inject repositories or domain services directly.
- Route prefix follows `api/{resource}` in kebab-case.
- Group endpoints by HTTP verb order: `GET` → `POST` → `PUT`/`PATCH` → `DELETE`.

```csharp
// Correct: thin, sealed, single dependency
[ApiController]
[Route("api/ledger")]
public sealed class LedgerController(ISender sender) : ControllerBase
{
    [HttpGet("{entryId}")]
    public async Task<IActionResult> GetEntry(Guid entryId, CancellationToken ct)
    {
        var result = await sender.Send(new GetLedgerEntryQuery(LedgerEntryId.From(entryId)), ct);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    [HttpPost("{entryId}/credits")]
    public async Task<IActionResult> RecordCredit(
        Guid entryId,
        RecordCreditRequest request,
        CancellationToken ct)
    {
        var cmd = new RecordCreditCommand(
            LedgerEntryId.From(entryId),
            request.Amount,
            request.Currency,
            request.Description,
            request.OccurredOn);

        var result = await sender.Send(cmd, ct);

        return result.IsSuccess
            ? NoContent()
            : result.ToProblemDetails();
    }
}

// Wrong: fat controller with business logic and multiple dependencies
public sealed class LedgerController(
    ISender sender,
    ILedgerEntryRepository repository,  // wrong — infrastructure in Api
    ILogger<LedgerController> logger) : ControllerBase
{
    [HttpPost("{entryId}/credits")]
    public async Task<IActionResult> RecordCredit(Guid entryId, RecordCreditRequest request)
    {
        var entry = await repository.FindByIdAsync(...); // wrong layer
        if (entry.IsClosed) return BadRequest("Ledger is closed"); // domain rule here
        ...
    }
}
```

### 6.2 Swagger (OpenAPI)

Denarius uses **Swagger UI** (via Swashbuckle) as the API documentation and manual testing interface. It generates an interactive UI from the OpenAPI spec produced automatically by ASP.NET Core.

- Swagger UI is enabled in development only — never expose it in production without authentication.
- `Swashbuckle.AspNetCore` is the sole package responsible for both spec generation and UI.
- All endpoints must carry `[ProducesResponseType]` attributes so the generated spec is accurate and complete.
- Group endpoints with `[Tags("Ledger")]` to match the bounded context name — this controls grouping in the Swagger UI.
- Document all request/response types with XML comments (`<summary>`) when the intent is not obvious from the type name.

```csharp
// IoC layer — Swagger registration
services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "Denarius API",
        Version = "v1"
    });

    // Include XML comments from all relevant assemblies
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));

    // Bearer token support
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Program.cs — Swagger UI (development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Denarius API v1");
        options.RoutePrefix = "swagger";
    });
}
```

```csharp
// Controller — Swagger metadata
/// <summary>Records a credit entry on an existing ledger.</summary>
[HttpPost("{entryId}/credits")]
[Tags("Ledger")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
public async Task<IActionResult> RecordCredit(
    Guid entryId,
    RecordCreditRequest request,
    CancellationToken ct)
{ ... }
```

### 6.3 DomainError → HTTP Mapping

`Result.Failure` carries a `DomainError`. The global exception handler and a result-mapping extension translate domain errors to RFC 7807 problem details consistently. This mapping lives in Api — never in handlers or domain objects.

```csharp
// Api layer — result extension
public static IActionResult ToProblemDetails(this Result result) =>
    result.Error.Code switch
    {
        DomainErrorCode.NotFound        => new NotFoundObjectResult(ProblemDetailsFor(result.Error)),
        DomainErrorCode.Conflict        => new ConflictObjectResult(ProblemDetailsFor(result.Error)),
        DomainErrorCode.Validation      => new UnprocessableEntityObjectResult(ProblemDetailsFor(result.Error)),
        _                               => new ObjectResult(ProblemDetailsFor(result.Error)) { StatusCode = 500 }
    };
```

Adding a new `DomainErrorCode` requires a corresponding entry in this switch — never return a raw `500` for a known domain failure.

---

## 7. Denarius.IoC

- Sole responsibility: dependency injection wiring.
- May reference Application, Infrastructure, Domain, and CrossCutting.
- **Must NOT reference Denarius.Api.** Api references IoC, not the other way around.
- All `IServiceCollection` extension methods live here, not scattered across layers.

```csharp
// IoC layer — organized by layer
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDenarius(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddDomain()
            .AddApplication()
            .AddInfrastructure(configuration);

        return services;
    }

    private static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Application.AssemblyReference.Assembly));
        services.AddValidatorsFromAssembly(Application.AssemblyReference.Assembly);
        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();
        return services;
    }

    private static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<DenariusDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Denarius")));

        services.AddScoped<ILedgerEntryRepository, LedgerEntryRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        return services;
    }
}
```

---

## 8. No Circular References

- The project reference graph must be a DAG (directed acyclic graph).
- Run `dotnet build` and treat any `CS0234` or project-reference cycle error as a blocker.
- If a circular dependency appears to be necessary, it is a signal that a concept belongs in a lower layer or CrossCutting — not a reason to add a circular reference.

---

## 9. Use Cases Are Single-Responsibility

- One command or query per handler class. Do not merge unrelated use cases.
- File location mirrors the use case: `Application/UseCases/Ledger/RecordCredit/RecordCreditCommandHandler.cs`.
- Handlers must not call other handlers. Shared logic belongs in a domain service or application service, not via handler chaining.

---

## 10. Cross-Cutting Concerns Belong at the Boundary

- Logging, tracing, and validation are applied via middleware or MediatR `IPipelineBehavior`, not inline inside handlers or domain objects.
- Exception-to-HTTP mapping lives in a global exception handler in Api — not scattered across controllers.
- Authorization checks live in Api middleware or Application pipeline — never inside domain methods.

### 10.1 Pipeline Behaviors

Behaviors execute in registration order and wrap every handler call. The expected behaviors in Denarius are:

| Behavior                  | Responsibility                                              |
|---------------------------|-------------------------------------------------------------|
| `LoggingBehavior<,>`      | Logs command/query name, duration, and outcome              |
| `ValidationBehavior<,>`   | Runs FluentValidation; short-circuits with `Result.Failure` |
| `TransactionBehavior<,>`  | Wraps commands in a DB transaction; commits on success      |

```csharp
// Application layer — ValidationBehavior skeleton
internal sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var failures = validators
            .Select(v => v.Validate(request))
            .SelectMany(r => r.Errors)
            .Where(e => e is not null)
            .ToList();

        if (failures.Count > 0)
            return CreateValidationFailure<TResponse>(failures);

        return await next();
    }
}
```

Query handlers must not be wrapped by `TransactionBehavior` — use a marker interface (`ICommand` vs `IQuery`) to differentiate.

---

## 11. DTOs and Contracts

- Request/response DTOs live in Application (commands, queries, result records).
- Domain objects are never serialized directly to HTTP responses. Map at the Api boundary.
- Never expose EF Core entities as API response types.

```csharp
// Correct: dedicated response DTO mapped at the Api boundary
public sealed record AccountSummaryResponse(
    Guid AccountId,
    decimal Balance,
    string Currency,
    int LineCount);

// In the controller
var dto = await sender.Send(new GetAccountSummaryQuery(accountId), ct);
return Ok(new AccountSummaryResponse(dto.AccountId.Value, dto.Balance, dto.Currency, dto.LineCount));

// Wrong: domain object or EF entity serialized directly
return Ok(ledgerEntry);   // exposes domain internals
return Ok(ledgerEntity);  // exposes persistence model
```

---

## 12. Persistence Isolation

- The domain model must be persistence-ignorant. EF Core configurations (`IEntityTypeConfiguration<T>`) live in Infrastructure and map to/from domain objects without leaking EF attributes into Domain classes.
- No `[Key]`, `[Column]`, or `[Required]` data annotations on domain entities — configure via Fluent API only.
- Migrations are generated and owned by Infrastructure; never modify domain classes to satisfy a migration.

```csharp
// Infrastructure layer — EF configuration, domain class stays clean
internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
               .HasConversion(id => id.Value, value => LedgerEntryId.From(value));

        builder.OwnsOne(e => e.InitialBalance, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Amount").HasPrecision(18, 4);
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3);
        });

        builder.HasMany<LedgerLine>("_lines")
               .WithOne()
               .HasForeignKey("LedgerEntryId")
               .IsRequired();
    }
}
```

---

## 13. Testing Boundaries

- `Denarius.Domain.Tests` — pure unit tests; no EF Core, no HTTP, no containers.
- `Denarius.Application.Tests` — handler tests with mocked repositories and dispatcher.
- `Denarius.Api.Tests` — controller/integration slice tests with `WebApplicationFactory`.
- `Denarius.Integration.Tests` — end-to-end tests using Testcontainers (MSSQL, RabbitMQ).
- Tests must not reference layers they are not testing (e.g., Domain tests must not reference Infrastructure).

```
Domain.Tests         → Domain, CrossCutting
Application.Tests    → Application, Domain, CrossCutting  (mock Infrastructure)
Api.Tests            → Api, Application  (WebApplicationFactory, in-memory DB)
Integration.Tests    → all layers  (Testcontainers, real DB and broker)
```

---

## 14. File and Namespace Conventions

- Namespace matches the folder path exactly: `Denarius.Application.UseCases.Ledger.RecordCredit`.
- One public type per file; file name equals the type name.
- File-scoped namespaces are required (enforced by `.editorconfig`).

---

## 15. End-to-End Request Flow

This is what a complete write operation looks like across all layers. Every layer does exactly its own job — nothing more.

```
HTTP POST /api/ledger/{entryId}/credits
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│  Api — LedgerController                                     │
│  1. Deserialize request body → RecordCreditRequest (DTO)    │
│  2. Map to RecordCreditCommand (Application contract)       │
│  3. sender.Send(cmd)                                        │
└───────────────────────┬─────────────────────────────────────┘
                        │  MediatR pipeline
                        ▼
              LoggingBehavior        (logs start)
              ValidationBehavior     (runs FluentValidation)
              TransactionBehavior    (opens DB transaction)
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│  Application — RecordCreditCommandHandler                   │
│  1. repository.FindByIdAsync(cmd.EntryId)                   │
│  2. Money.Create(cmd.Amount, cmd.Currency)                  │
│  3. entry.RecordCredit(money, description, occurredOn)      │
│     └─ Domain: validates invariants, raises domain event    │
│  4. unitOfWork.CommitAsync()                                │
│  5. dispatcher.DispatchAsync(entry.DomainEvents)            │
│  6. entry.ClearDomainEvents()                               │
│  7. return Result.Success()                                 │
└───────────────────────┬─────────────────────────────────────┘
                        │
              TransactionBehavior    (commits transaction)
              LoggingBehavior        (logs outcome + duration)
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│  Api — LedgerController (continued)                         │
│  8. result.IsSuccess → 204 NoContent                        │
│     result.IsFailure → result.ToProblemDetails()            │
└─────────────────────────────────────────────────────────────┘
```

---

## 16. Quick Reference — What Goes Where

| Concept                         | Layer          |
|---------------------------------|----------------|
| Aggregate, Entity, Value Object | Domain         |
| Domain Event                    | Domain         |
| Repository interface            | Domain         |
| Repository implementation       | Infrastructure |
| EF Core DbContext & migrations  | Infrastructure |
| EF `IEntityTypeConfiguration`   | Infrastructure |
| External HTTP/SDK client        | Infrastructure |
| Command / Query handler         | Application    |
| FluentValidation validator      | Application    |
| Pipeline behavior               | Application    |
| `ICurrentUser` (interface)      | Application    |
| `ICurrentUser` (implementation) | Api            |
| Controller (conventional)       | Api            |
| Swagger / OpenAPI config        | Api            |
| Global exception handler        | Api            |
| `DomainError` → HTTP mapping    | Api            |
| DI registration                 | IoC            |
| `Result<T>`, `DomainError`      | CrossCutting   |
