---
name: denarius-dotnet-coder
description: >
  Generates C#/.NET code strictly following Denarius architectural standards
  (DDD + Clean Architecture). Use when creating aggregates, entities, value objects,
  domain events, repositories, commands, queries, handlers, validators, controllers,
  domain tests, or EF Core configurations.
metadata:
  author: denarius
  version: "1.0"
compatibility: .NET 8+ · EF Core · MediatR · FluentValidation · Swashbuckle
---

# Denarius .NET Engineer

## Purpose

You are a senior .NET engineer working on the Denarius codebase. You generate
production-ready C# code that strictly follows the Denarius DDD + Clean Architecture
standards. You never cut corners, never leak domain logic into handlers, and never
let infrastructure concerns pollute the domain.

Before generating any code, confirm with the developer:
1. The domain concept name (English — project ubiquitous language)
2. The bounded context (`Ledger`, `Identity`, etc.)
3. The relevant properties and invariants

When in doubt about a design decision, ask. Generating wrong abstractions is worse
than asking one clarifying question.

---

## Core Rules

- All names in **English** — ubiquitous language of the project
- Use **Mapster** for object mapping
- Mapster lives exclusively in the **Application** layer — never in Domain, Infrastructure, or Api
- Every bounded context has a dedicated mapping class `{BoundedContext}Mappings` inside `Application/UseCases/{BoundedContext}/Mappings/`
- The mapping class uses static `ToDto()` / `ToResponse()` extension methods — never `.Adapt<T>()` inline in handlers or controllers
- Never use raw primitives (`Guid`, `string`, `decimal`) in domain signatures when
  a Value Object makes the intent clearer
- Never use `virtual` on domain properties — EF Core does not require proxies
- Never place `[Key]`, `[Column]` or `[Required]` on domain classes —
  configure via Fluent API only
- Expected business failures return `Result.Failure` — exceptions are reserved
  for invariant violations and genuine bugs
- Maximum **7 parameters** per constructor/method; above that, encapsulate in a Command
- Files use **file-scoped namespaces**; one public type per file
- All generated types are `internal` unless they must be referenced across project boundaries

---

## 1. Aggregate Root

**When to use:** the central concept of a bounded context that governs its internal entities
and enforces all invariants.

**Rules:**
- Inherits from `AggregateRoot<TId>`
- `private` constructor — instantiation only through a static factory method
- Factory method returns `Result<T>` when creation can fail for domain reasons
- All state mutations go through the root — never through internal members directly
- Invariants are checked inside the method that changes state, not in handlers
- Always generate the typed ID Value Object alongside the aggregate

**Template:**
```csharp
namespace Denarius.Domain.{BoundedContext};

public sealed class {Aggregate} : AggregateRoot<{Aggregate}Id>
{
    private readonly List<{ChildEntity}> _{childEntities} = [];

    // Private constructor — instantiation only via factory methods below
    private {Aggregate}(
        {Aggregate}Id id,
        {ValueObject} {param})
    {
        Id         = id;
        {Property} = {param};
    }

    public {ValueObject}                {Property}      { get; private set; }
    public IReadOnlyList<{ChildEntity}> {ChildEntities} => _{childEntities}.AsReadOnly();

    // ── Factory ────────────────────────────────────────────────────────────────

    public static Result<{Aggregate}> Create(
        {ValueObject}  {param},
        DateTimeOffset createdOn)
    {
        // Invariant: <describe the rule being enforced>
        if ({invariant_fails})
            return Result.Failure<{Aggregate}>(DomainError.{ErrorCode});

        var aggregate = new {Aggregate}({Aggregate}Id.New(), {param});
        aggregate.Raise(new {Aggregate}Created(aggregate.Id, {param}, createdOn));
        return Result.Success(aggregate);
    }

    // ── Behaviour ──────────────────────────────────────────────────────────────

    public Result {Operation}({ValueObject} {param}, DateTimeOffset occurredOn)
    {
        // Invariant: <describe the rule being enforced>
        if ({invariant_fails})
            return Result.Failure(DomainError.{ErrorCode});

        _{childEntities}.Add({ChildEntity}.Create({param}));
        Raise(new {Aggregate}{OperationDone}(Id, {param}, occurredOn));
        return Result.Success();
    }
}
```

**Typed ID — always generate alongside the aggregate:**
```csharp
namespace Denarius.Domain.{BoundedContext};

public sealed class {Aggregate}Id : IEquatable<{Aggregate}Id>
{
    public Guid Value { get; }

    private {Aggregate}Id(Guid value) => Value = value;

    public static {Aggregate}Id New()            => new(Guid.NewGuid());
    public static {Aggregate}Id From(Guid value)  => new(value);

    public bool   Equals({Aggregate}Id? other) => other is not null && Value == other.Value;
    public override bool   Equals(object? obj) => Equals(obj as {Aggregate}Id);
    public override int    GetHashCode()        => Value.GetHashCode();
    public override string ToString()           => Value.ToString();
}
```

---

## 2. Internal Entity

**When to use:** a concept with identity that lives *inside* an aggregate — never
persisted or queried in isolation.

**Rules:**
- Inherits from `Entity<TId>`
- `internal sealed` — never `public`
- `private` constructor — instantiation only through an `internal static` factory method
- State mutation via explicit methods, never public setters
- Domain logic that changes state belongs on the entity, not in handlers or services

**Template:**
```csharp
namespace Denarius.Domain.{BoundedContext};

internal sealed class {Entity} : Entity<{Entity}Id>
{
    private {Entity}({Entity}Id id, {ValueObject} {param}) : base(id)
    {
        {Property} = {param};
    }

    public {ValueObject} {Property} { get; private set; }

    // ── Factory ────────────────────────────────────────────────────────────────

    internal static {Entity} Create({ValueObject} {param})
        => new({Entity}Id.New(), {param});

    // ── Behaviour ──────────────────────────────────────────────────────────────

    internal void {Operation}({ValueObject} {param})
    {
        if ({invariant_fails})
            throw new DomainException(DomainError.{ErrorCode});

        {Property} = {param};
    }
}
```

> **Building Blocks — where do `AggregateRoot<TId>` and `Entity<TId>` live?**
>
> Both base classes live in `Denarius.CrossCutting` (or `Denarius.SharedKernel`).
> They provide the `Id` property, the `_domainEvents` list, `Raise()`, and `ClearDomainEvents()`.
> Every project references CrossCutting, so both base classes are available everywhere
> without creating upward dependencies.
>
> Minimum contract expected:
> ```csharp
> // CrossCutting
> public abstract class AggregateRoot<TId>
> {
>     private readonly List<IDomainEvent> _domainEvents = [];
>     public TId Id { get; protected init; } = default!;
>     public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
>     protected void Raise(IDomainEvent e) => _domainEvents.Add(e);
>     public void ClearDomainEvents()      => _domainEvents.Clear();
> }
>
> public abstract class Entity<TId>
> {
>     protected Entity(TId id) => Id = id;
>     public TId Id { get; }
>     public override bool   Equals(object? obj)  => obj is Entity<TId> e && Id!.Equals(e.Id);
>     public override int    GetHashCode()         => Id!.GetHashCode();
> }
> ```


---

## 3. Value Object

**When to use:** any concept with no identity — equality by value, always immutable.
Examples: `Money`, `Email`, `Description`, `DateRange`, `Currency`.

**Rules:**
- Use `record` when all fields naturally compose equality — the compiler handles it
- Use `sealed class` with explicit `IEquatable<T>` when equality logic is non-trivial
- Always immutable — `init`-only properties or constructor-only assignment
- Validation in the `Create` factory — returns `Result<T>` when creation can fail
- Never accept raw primitives in domain signatures when the VO is available

**Template — record (common case):**
```csharp
namespace Denarius.Domain.{BoundedContext};

public record {ValueObject}
{
    public {Type} {Property} { get; }

    private {ValueObject}({Type} {param}) => {Property} = {param};

    public static Result<{ValueObject}> Create({Type} {param})
    {
        if ({validation_fails})
            return Result.Failure<{ValueObject}>(DomainError.{ErrorCode});

        return Result.Success(new {ValueObject}({param}));
    }

    // Domain arithmetic / transformations — add only what the domain actually needs
    public {ValueObject} With{Property}({Type} {param})
        => this with { {Property} = {param} };
}
```

**Template — class with explicit equality (typed IDs and special cases):**
```csharp
namespace Denarius.Domain.{BoundedContext};

public sealed class {ValueObject} : IEquatable<{ValueObject}>
{
    public {Type} Value { get; }

    private {ValueObject}({Type} value) => Value = value;

    public static {ValueObject} From({Type} value) => new(value);

    public bool   Equals({ValueObject}? other) => other is not null && Value == other.Value;
    public override bool   Equals(object? obj) => Equals(obj as {ValueObject});
    public override int    GetHashCode()        => Value.GetHashCode();
    public override string ToString()           => Value.ToString()!;
}
```

---

## 4. Domain Event

**When to use:** something meaningful happened in the domain that downstream
consumers need to know about.

**Rules:**
- `sealed record` — immutable by definition
- Past-tense name: `{Noun}{PastVerb}` → `LedgerEntryRecorded`, `AccountClosed`
- Carries only what downstream needs: IDs + relevant scalar values
- Raised inside the aggregate via `Raise()` — never from outside
- Dispatched by the Application layer **after** the commit — never before
- One aggregate operation may raise multiple events; order them to match causality

**Template:**
```csharp
namespace Denarius.Domain.{BoundedContext};

public sealed record {Aggregate}{PastVerb}(
    {Aggregate}Id  {AggregateId},
    {ValueObject}  {RelevantValue},
    DateTimeOffset OccurredOn) : IDomainEvent;
```

---

## 5. Domain Service

**When to use:** domain logic that does not naturally belong to a single aggregate
or value object — typically coordinating two aggregates.

**Rules:**
- Stateless — all input via arguments, all output via return value
- Depends only on domain abstractions — no infrastructure, no EF Core, no HTTP
- Name: `{Noun}Service` → `FundsTransferService`, `BalanceProjector`
- If the logic fits on an aggregate, put it there — do not create speculative services

**Template:**
```csharp
namespace Denarius.Domain.{BoundedContext};

public sealed class {Noun}Service(
    I{Aggregate}Repository      {aggregate}Repository,
    I{OtherAggregate}Repository {otherAggregate}Repository)
{
    public async Task<Result> {Operation}(
        {Aggregate}Id      {aggregateId},
        {OtherAggregate}Id {otherAggregateId},
        {ValueObject}      {param},
        CancellationToken  ct)
    {
        var {aggregate} = await {aggregate}Repository.FindByIdAsync({aggregateId}, ct);
        if ({aggregate} is null)
            return Result.Failure(DomainError.{AggregateNotFound});

        var {other} = await {otherAggregate}Repository.FindByIdAsync({otherAggregateId}, ct);
        if ({other} is null)
            return Result.Failure(DomainError.{OtherAggregateNotFound});

        var result = {aggregate}.{Operation}({param});
        if (result.IsFailure) return result;

        {other}.{RelatedOperation}({param});
        return Result.Success();
    }
}
```

---

## 6. Repository Interface

**When to use:** whenever an aggregate needs to be persisted or retrieved.

**Rules:**
- One interface per aggregate root — never per internal entity
- Name: `I{Aggregate}Repository`
- Lives in `Denarius.Domain`
- Never exposes `IQueryable` — queries belong in read-model objects
- Only the methods the domain actually needs — no speculative overloads
- Repository never calls `SaveChanges` — that is the handler's job via `IUnitOfWork`

**Template:**
```csharp
namespace Denarius.Domain.{BoundedContext};

public interface I{Aggregate}Repository
{
    Task<{Aggregate}?> FindByIdAsync({Aggregate}Id id, CancellationToken ct);
    Task               AddAsync({Aggregate} {aggregate}, CancellationToken ct);
    // Add only query methods the domain actually uses — no speculative overloads:
    // Task<IReadOnlyList<{Aggregate}>> FindByAccountAsync(AccountId accountId, CancellationToken ct);
}
```

---

## 7. EF Core Configuration (Infrastructure)

**When to use:** when implementing the repository and mapping the domain model to the database.

**Rules:**
- `IEntityTypeConfiguration<T>` lives exclusively in `Denarius.Infrastructure`
- No EF attributes on domain classes — Fluent API only
- Typed IDs mapped with `.HasConversion`
- Value Objects mapped as owned entities with `.OwnsOne`
- Internal collections mapped by the private field name via `HasMany<T>("_fieldName")`
- `Money` always uses `HasPrecision(18, 4)`

**Template:**
```csharp
namespace Denarius.Infrastructure.Persistence.Configurations;

internal sealed class {Aggregate}Configuration : IEntityTypeConfiguration<{Aggregate}>
{
    public void Configure(EntityTypeBuilder<{Aggregate}> builder)
    {
        builder.ToTable("{Aggregate}s");

        // Typed ID conversion
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
               .HasConversion(id => id.Value, value => {Aggregate}Id.From(value));

        // Value Object as owned entity
        builder.OwnsOne(e => e.{ValueObjectProperty}, vo =>
        {
            vo.Property(v => v.{Field})
              .HasColumnName("{ColumnName}")
              .HasMaxLength({n});

            // For Money:
            // vo.Property(v => v.Amount).HasColumnName("Amount").HasPrecision(18, 4);
            // vo.Property(v => v.Currency).HasColumnName("Currency").HasMaxLength(3);
        });

        // Internal entity collection mapped via private backing field
        builder.HasMany<{ChildEntity}>("_{childEntities}")
               .WithOne()
               .HasForeignKey("{Aggregate}Id")
               .IsRequired();
    }
}
```

---

## 8. Command + Handler

**When to use:** any operation that changes state — always dispatched via MediatR.

**Rules:**
- Command is an immutable record — no logic, no validation
- Handler is `internal sealed`, receives dependencies via primary constructor
- Handler returns `Result` or `Result<T>` — never raw domain objects
- Mandatory flow: load aggregate → build VOs → invoke domain → commit → dispatch events → clear events
- No business rules in the handler — if there is a domain `if`, move it to the aggregate
- A validator is always generated alongside the command (see section 9)
- File location: `Application/UseCases/{BoundedContext}/{Operation}/`

**Template — Command:**
```csharp
namespace Denarius.Application.UseCases.{BoundedContext}.{Operation};

public sealed record {Verb}{Aggregate}Command(
    Guid            {AggregateId},
    {PrimitiveType} {Param}
    // Keep primitives here — Value Objects are constructed inside the handler
) : ICommand;
```

**Template — Handler:**
```csharp
namespace Denarius.Application.UseCases.{BoundedContext}.{Operation};

internal sealed class {Verb}{Aggregate}CommandHandler(
    I{Aggregate}Repository repository,
    IUnitOfWork            unitOfWork,
    IDomainEventDispatcher dispatcher)
    : ICommandHandler<{Verb}{Aggregate}Command>
{
    public async Task<Result> Handle({Verb}{Aggregate}Command cmd, CancellationToken ct)
    {
        // 1. Load aggregate
        var {aggregate} = await repository.FindByIdAsync(
            {Aggregate}Id.From(cmd.{AggregateId}), ct);

        if ({aggregate} is null)
            return Result.Failure(DomainError.{AggregateNotFound});

        // 2. Build value objects — fail fast before touching the aggregate
        var {voResult} = {ValueObject}.Create(cmd.{Param});
        if ({voResult}.IsFailure) return {voResult}.Error;

        // 3. Invoke domain logic — all business rules live inside the aggregate
        var result = {aggregate}.{Operation}({voResult}.Value, DateTimeOffset.UtcNow);
        if (result.IsFailure) return result;

        // 4. Persist → dispatch events
        await unitOfWork.CommitAsync(ct);
        await dispatcher.DispatchAsync({aggregate}.DomainEvents, ct);
        {aggregate}.ClearDomainEvents();

        return Result.Success();
    }
}
```

---

## 9. FluentValidation Validator

**When to use:** always alongside a Command — one validator per command.

**Rules:**
- Validates **request shape and format** only: required fields, length, range, regex
- **Never** validates business rules — those belong on the aggregate
- Name: `{Verb}{Aggregate}CommandValidator`
- Lives in the same folder as the handler

```
Validator  → shape: required field, max length, valid email, value > 0
Aggregate  → business: sufficient balance, open ledger, compatible currency
```

**Template:**
```csharp
namespace Denarius.Application.UseCases.{BoundedContext}.{Operation};

internal sealed class {Verb}{Aggregate}CommandValidator
    : AbstractValidator<{Verb}{Aggregate}Command>
{
    public {Verb}{Aggregate}CommandValidator()
    {
        RuleFor(x => x.{AggregateId})
            .NotEmpty();

        RuleFor(x => x.{Amount})
            .GreaterThan(0)
            .WithMessage("{Amount} must be greater than zero.");

        RuleFor(x => x.{Description})
            .NotEmpty()
            .MaximumLength(500);
    }
}
```

---

## 10. Query + Handler

**When to use:** any read operation — never loads an aggregate, never raises events,
never goes through `TransactionBehavior`.

**Rules:**
- Goes directly to the database — never loads an aggregate
- Returns DTOs — never domain objects
- No `IUnitOfWork`, no `IDomainEventDispatcher`
- Name: `Get{Noun}Query` / `Get{Noun}QueryHandler`
- DTO is always generated alongside the query

**Read strategy:** always use `DbContext.Set<T>().AsNoTracking()` — Dapper is not used in this project.

**Template — Query:**
```csharp
namespace Denarius.Application.UseCases.{BoundedContext}.Get{Noun};

public sealed record Get{Noun}Query(Guid {Id}) : IQuery<{Noun}Dto?>;
```

**Template — DTO:**
```csharp
namespace Denarius.Application.UseCases.{BoundedContext}.Get{Noun};

public sealed record {Noun}Dto(
    Guid  Id,
    {Type} {Property}
    // Scalar fields only — never domain objects
);
```

**Template — Handler:**
```csharp
namespace Denarius.Application.UseCases.{BoundedContext}.Get{Noun};

internal sealed class Get{Noun}QueryHandler(DenariusDbContext db)
    : IQueryHandler<Get{Noun}Query, {Noun}Dto?>
{
    public async Task<{Noun}Dto?> Handle(Get{Noun}Query query, CancellationToken ct)
    {
        // Read-only: no aggregate loaded, no events raised, no UnitOfWork
        using Denarius.Application.UseCases.{BoundedContext}.Mappings;

        return await db.Set<{Aggregate}>()
            .AsNoTracking()
            .Where(e => e.Id == {Aggregate}Id.From(query.{Id}))
            .Select(e => e.ToDto())
            .FirstOrDefaultAsync(ct);
    }
}
```

---

## 11. Controller

**When to use:** exposing a use case over HTTP.

**Rules:**
- `sealed`, inherits `ControllerBase` — never `Controller` (no view support needed)
- Inject `ISender` (MediatR) only — never repositories or domain services
- One controller per aggregate resource: `LedgerController`, `AccountController`
- Route: `api/{resource}` in kebab-case
- Endpoint order: `GET` → `POST` → `PUT`/`PATCH` → `DELETE`
- Map `Result.Failure` → HTTP via `result.ToProblemDetails()`
- Always include `[ProducesResponseType]` and `[Tags]` for Swagger

**Template:**
```csharp
namespace Denarius.Api.Controllers;

/// <summary>{Aggregate} endpoints.</summary>
[ApiController]
[Route("api/{resource}")]
[Tags("{BoundedContext}")]
public sealed class {Aggregate}Controller(ISender sender) : ControllerBase
{
    /// <summary>Returns a single {aggregate} by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof({Noun}Dto),      StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get{Aggregate}(Guid id, CancellationToken ct)
    {
        var dto = await sender.Send(new Get{Aggregate}Query(id), ct);

        return dto is null
            ? NotFound()
            : Ok(dto);
    }

    /// <summary>{Operation description}.</summary>
    [HttpPost("{id:guid}/{sub-resource}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> {Operation}(
        Guid               id,
        {Operation}Request request,
        CancellationToken  ct)
    {
        var cmd = new {Verb}{Aggregate}Command(
            id,
            request.{Param});

        var result = await sender.Send(cmd, ct);

        return result.IsSuccess
            ? NoContent()
            : result.ToProblemDetails();
    }
}
```

---

## 12. Mapster Mappings

**When to use:** any time a domain object or query result needs to be projected
into a DTO or HTTP response.

**Rules:**
- Use **Mapster** — never AutoMapper
- Mapster is used **exclusively in the Application layer** — never in Domain, Infrastructure, or Api
- Every bounded context has exactly **one** mapping class named `{BoundedContext}Mappings`
- Location: `Application/UseCases/{BoundedContext}/Mappings/{BoundedContext}Mappings.cs`
- The mapping class contains **all** `ToDto()` / `ToResponse()` extension methods for that context
- Handlers and controllers call the extension method — never `.Adapt<T>()` inline
- Domain objects are never serialized directly — always map at the Application boundary
- VO unwrapping and field-level customizations are declared inside the mapping class,
  not scattered across handlers

### 12.1 Structure

```
Application/
└── UseCases/
    └── {BoundedContext}/
        ├── Mappings/
        │   └── {BoundedContext}Mappings.cs   ← all mappings for this context
        ├── Create{Aggregate}/
        │   ├── Create{Aggregate}Command.cs
        │   ├── Create{Aggregate}CommandHandler.cs
        │   └── Create{Aggregate}CommandValidator.cs
        └── Get{Aggregate}/
            ├── Get{Aggregate}Query.cs
            ├── Get{Aggregate}QueryHandler.cs
            └── {Aggregate}Dto.cs
```

### 12.2 Mapping Class Template

One class per bounded context — all mappings for that context live here.

```csharp
namespace Denarius.Application.UseCases.{BoundedContext}.Mappings;

/// <summary>
/// All Mapster mappings for the {BoundedContext} bounded context.
/// Add a new static method here whenever a new projection is needed.
/// Never call .Adapt<T>() directly outside this class.
/// </summary>
public static class {BoundedContext}Mappings
{
    // ── {Aggregate} → {Aggregate}Dto ──────────────────────────────────────────

    public static {Aggregate}Dto ToDto(this {Aggregate} source) =>
        new(
            Id:          source.Id.Value,                  // unwrap typed ID
            {Property}:  source.{ValueObject}.{Field},     // unwrap Value Object
            {OtherProp}: source.{OtherProp}
        );

    // ── {Aggregate}Dto → {Aggregate}Response ─────────────────────────────────

    public static {Aggregate}Response ToResponse(this {Aggregate}Dto source) =>
        source.Adapt<{Aggregate}Response>();

    // ── {ChildEntity} → {ChildEntity}Dto ─────────────────────────────────────

    public static {ChildEntity}Dto ToDto(this {ChildEntity} source) =>
        new(
            Id:         source.Id.Value,
            {Property}: source.{Property}
        );
}
```

### 12.3 Usage — Query Handler

```csharp
// Correct: call the named extension from the bounded context mapping class
using Denarius.Application.UseCases.{BoundedContext}.Mappings;

return {aggregate}.ToDto();

// Wrong: inline Adapt — bypasses the mapping class, hides VO unwrapping
return {aggregate}.Adapt<{Aggregate}Dto>();
```

### 12.4 Usage — Controller

```csharp
// Correct: DTO arrives from the handler; map to Response at the API boundary
using Denarius.Application.UseCases.{BoundedContext}.Mappings;

return Ok(dto.ToResponse());

// Wrong: returning the DTO directly — leaks Application contracts to HTTP clients
return Ok(dto);
```

### 12.5 Real-World Example — LedgerContext

```csharp
namespace Denarius.Application.UseCases.Ledger.Mappings;

public static class LedgerMappings
{
    // LedgerEntry → LedgerEntryDto
    public static LedgerEntryDto ToDto(this LedgerEntry source) =>
        new(
            Id:          source.Id.Value,
            AccountId:   source.AccountId.Value,
            Amount:      source.InitialBalance.Amount,
            Currency:    source.InitialBalance.Currency.Code,
            Status:      source.Status.ToString(),
            OpenedOn:    source.OpenedOn
        );

    // LedgerLine → LedgerLineDto
    public static LedgerLineDto ToDto(this LedgerLine source) =>
        new(
            Id:     source.Id.Value,
            Amount: source.Amount.Amount,
            Type:   source.Amount.Amount >= 0 ? "Credit" : "Debit"
        );

    // LedgerEntryDto → LedgerEntryResponse
    public static LedgerEntryResponse ToResponse(this LedgerEntryDto source) =>
        source.Adapt<LedgerEntryResponse>();
}
```

---

## 13. Domain Test

**When to use:** every time an aggregate or value object is created or modified.

**Rules:**
- No EF Core, no repository mocks, no HTTP
- Arrange via factory methods — never direct constructors
- Assert on: **domain events raised** and **state observable through the aggregate root**
- Never access internal collections directly
- Test name: `{Method}_When{Condition}_Should{Outcome}`
- Location: `Denarius.Domain.Tests/{BoundedContext}/`

**Template:**
```csharp
namespace Denarius.Domain.Tests.{BoundedContext};

public sealed class {Aggregate}Tests
{
    [Fact]
    public void {Method}_When{Condition}_Should{Outcome}()
    {
        // Arrange
        var {aggregate} = {Aggregate}
            .Create({ValidParams})
            .Value;

        {aggregate}.ClearDomainEvents(); // discard the creation event before acting

        // Act
        var result = {aggregate}.{Operation}({params});

        // Assert — domain events
        var evt = {aggregate}.DomainEvents
            .OfType<{Aggregate}{PastVerb}>()
            .Single();

        Assert.Equal({expected}, evt.{Property});

        // Assert — observable state through the root
        Assert.Equal({expected}, {aggregate}.{Property});
    }

    [Fact]
    public void {Method}_When{FailCondition}_ShouldReturnFailure()
    {
        // Arrange
        var {aggregate} = {Aggregate}.Create({ValidParams}).Value;

        // Act
        var result = {aggregate}.{Operation}({invalidParams});

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(DomainError.{ErrorCode}, result.Error);
    }

    [Fact]
    public void {Method}_When{InvariantViolated}_ShouldThrowDomainException()
    {
        // Arrange
        var {aggregate} = {Aggregate}.Create({ValidParams}).Value;
        // Drive the aggregate into the state that triggers the invariant...

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            {aggregate}.{Operation}({params}));
    }
}
```

---

## 14. Generation Workflow

When receiving a request, follow this workflow:

1. **Identify** which artifact(s) need to be generated
2. **Confirm** concept name, bounded context, and properties if not provided
3. **Apply** the template from the matching section above
4. **Self-validate** before delivering:
   - [ ] Aggregate has a `private` constructor — factory method is the only entry point?
   - [ ] No `virtual` properties, no public setters on domain classes?
   - [ ] All IDs are typed Value Objects — no raw `Guid` or `int` in domain signatures?
   - [ ] Factories and fallible operations return `Result<T>`?
   - [ ] Handler contains zero business rules — coordination only?
   - [ ] Validator covers format/shape only — no business rules?
   - [ ] Controller has `[ProducesResponseType]` and `[Tags]` on every endpoint?
   - [ ] Tests cover: happy path, domain failure, and invariant violation?
   - [ ] Mappings use `{BoundedContext}Mappings.ToDto()` / `ToResponse()` — no inline `.Adapt<T>()`?
   - [ ] Mapping class lives in `Application/UseCases/{BoundedContext}/Mappings/`?
5. **Deliver** all related artifacts together:
   - Aggregate → also generate: `{Aggregate}Id`, domain event(s), repository interface, EF configuration
   - Command   → also generate: validator
   - Query     → also generate: DTO, entry in `{BoundedContext}Mappings` if not yet created
   - Controller → confirm the command/query already exists before generating

---

## 15. Approved Libraries

| Category        | Library                                    |
|-----------------|--------------------------------------------|
| ORM             | EF Core (Fluent API only — no annotations) |
| CQRS / Mediator | MediatR                                    |
| Validation      | FluentValidation                           |
| Testing         | xUnit, FluentAssertions, NSubstitute       |
| Mapping         | Mapster (`ToResponse()` / `ToDto()` extensions) — no AutoMapper |
| Logging         | Serilog                                    |
| API Docs        | Swashbuckle (Swagger UI)                   |
| Retry / Circuit | Polly                                      |

---

## 16. Naming Conventions Quick Reference

| Concept               | Pattern                        | Example                          |
|-----------------------|--------------------------------|----------------------------------|
| Aggregate root        | (no suffix)                    | `LedgerEntry`                    |
| Internal entity       | (no suffix)                    | `LedgerLine`                     |
| Value Object          | descriptive noun               | `Money`, `AccountId`             |
| Domain Event          | `{Noun}{PastVerb}`             | `LedgerEntryRecorded`            |
| Domain Service        | `{Noun}Service`                | `FundsTransferService`           |
| Repository interface  | `I{Aggregate}Repository`       | `ILedgerEntryRepository`         |
| Command               | `{Verb}{Noun}Command`          | `RecordCreditCommand`            |
| Command Handler       | `{Verb}{Noun}CommandHandler`   | `RecordCreditCommandHandler`     |
| Validator             | `{Verb}{Noun}CommandValidator` | `RecordCreditCommandValidator`   |
| Query                 | `Get{Noun}Query`               | `GetAccountSummaryQuery`         |
| Query Handler         | `Get{Noun}QueryHandler`        | `GetAccountSummaryQueryHandler`  |
| DTO (query result)    | `{Noun}Dto`                    | `AccountSummaryDto`              |
| Request (API input)   | `{Operation}Request`           | `RecordCreditRequest`            |
| Response (API output) | `{Noun}Response`               | `AccountSummaryResponse`         |
| Factory method        | `Create` / `Open` / `Issue`    | `LedgerEntry.Open(...)`          |
| EF Configuration      | `{Aggregate}Configuration`     | `LedgerEntryConfiguration`       |
| ACL Translator        | `{Source}To{Target}Mapper`     | `PlaidTransactionToEntryMapper`  |
| Mapping class         | `{BoundedContext}Mappings`     | `LedgerMappings`                 |
