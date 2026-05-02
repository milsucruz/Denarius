# Domain-Driven Design Rules

These rules govern how domain concepts are modeled and enforced across the Denarius codebase. They apply to every layer but are especially binding inside `Denarius.Domain` and `Denarius.Application`.

---

## 1. Ubiquitous Language

- Every concept that exists in the business domain **must** have exactly one name used consistently across code, tests, PR descriptions, and conversations.
- Never use synonyms for the same concept (`Account` vs `Wallet`, `Entry` vs `Movement`). Pick one and enforce it everywhere.
- When a business expert introduces a new term, update all existing usages before adding new code.
- Class names, method names, and variable names are the documentation — they must read like domain prose, not technical jargon.

---

## 2. Bounded Contexts

- Each bounded context owns its own model. Never share domain classes across contexts — use separate classes even if they look similar.
- Context boundaries map to top-level namespaces: `Denarius.Domain.Ledger`, `Denarius.Domain.Identity`, etc.
- Cross-context communication happens only through published Domain Events or explicit Anti-Corruption Layer (ACL) translators — never by direct object reference.
- When a concept appears in two contexts (e.g., `UserId`), represent it as a typed identifier (Value Object) in each context independently.

---

## 3. Aggregates

- An **Aggregate** is a cluster of domain objects treated as a single unit of consistency.
- Only the **Aggregate Root** may be referenced by external objects or repositories. Never inject or query internal aggregate members directly.
- All state changes to an aggregate go through the root — no direct property mutation on child entities from outside the aggregate.
- Aggregates must enforce their own invariants at all times; the aggregate is **always valid** after any operation.
- Keep aggregates small. If an aggregate grows beyond a handful of entities, split it.
- Aggregates reference other aggregates only by **typed ID** (Value Object), never by object reference.
- Example root: `LedgerEntry` — all entries within a ledger period are governed through it.

```csharp
// Correct: mutate through the root
ledger.RecordCredit(amount, description, occurredOn);

// Wrong: bypass the root
ledger.Entries.Add(new Entry(...));
```

### 3.1 AggregateRoot Base Class

All aggregate roots inherit from `AggregateRoot<TId>`. This base class owns the domain event collection — no aggregate manages its own list directly.

```csharp
public abstract class AggregateRoot<TId>
{
    private readonly List<IDomainEvent> domainEvents = [];

    public TId Id { get; protected init; } = default!;
    public IReadOnlyList<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) =>
        domainEvents.Add(domainEvent);

    public void ClearDomainEvents() =>
        domainEvents.Clear();
}
```

- Never expose a public `Raise` — only the aggregate itself raises events.
- `ClearDomainEvents` is called by the Application layer after dispatch, never inside the domain.

---

## 4. Entities

- An **Entity** has identity that persists through state changes. Two entities are equal if their IDs match, regardless of field values.
- Entity IDs are strongly-typed Value Objects, never raw `Guid` or `int`.
- Override `Equals` and `GetHashCode` based solely on the ID.
- Entities live inside their aggregate — they are never persisted or queried in isolation.
- Domain logic that mutates entity state belongs on the entity as a method, not in a service or handler.

```csharp
// Correct: behaviour on the entity
public sealed class LedgerLine : Entity<LedgerLineId>
{
    public void Reclassify(Category category) { ... }
}

// Wrong: logic outside the entity
ledgerLine.Category = newCategory; // naked setter
```

### 4.1 Entity Base Class

```csharp
public abstract class Entity<TId>
{
    protected Entity(TId id) => Id = id;

    public TId Id { get; }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id!.Equals(other.Id);
    }

    public override int GetHashCode() => Id!.GetHashCode();

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) =>
        !(left == right);
}
```

---

## 5. Value Objects

- A **Value Object** has no identity — two instances with the same data are interchangeable.
- Value Objects are **immutable**. All properties are `init`-only or set only in the constructor.
- Override `Equals`, `GetHashCode`, and optionally implement `IEquatable<T>` based on all fields.
- Model every domain concept that is not an entity as a Value Object: `Money`, `AccountId`, `DateRange`, `Description`, `Email`.
- Never use primitive types (`decimal`, `string`, `Guid`) directly in domain signatures when a richer VO makes the intent clearer.
- Validation belongs in the VO constructor or a static factory method. Return `Result<T>` from factories that can fail.

```csharp
// Correct: record VO with factory (equality is automatic via record)
public record Money(decimal Amount, Currency Currency)
{
    public static Result<Money> Create(decimal amount, Currency currency)
    {
        if (amount < 0) return Result.Failure<Money>(DomainError.InvalidAmount);
        return Result.Success(new Money(amount, currency));
    }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException(DomainError.CurrencyMismatch);

        return this with { Amount = Amount + other.Amount };
    }

    public Money Negate() => this with { Amount = -Amount };
}

// Correct: class VO with explicit equality (when record is not appropriate)
public sealed class AccountId : IEquatable<AccountId>
{
    public Guid Value { get; }

    private AccountId(Guid value) => Value = value;

    public static AccountId New() => new(Guid.NewGuid());
    public static AccountId From(Guid value) => new(value);

    public bool Equals(AccountId? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as AccountId);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value.ToString();
}

// Wrong: primitive obsession
public void Record(decimal amount, string currency) { ... }
```

---

## 6. Domain Events

- A **Domain Event** represents something that happened in the domain, expressed in past tense.
- Raise domain events inside aggregates via the inherited `Raise()` method — never call a handler directly from the domain.
- Events are dispatched by the Application layer **after** persistence succeeds — never before.
- Event names follow the pattern `{Noun}{PastVerb}`: `LedgerEntryRecorded`, `AccountClosed`, `TransferApproved`.
- Events carry only the data needed for downstream processing — typically IDs plus the relevant scalar values.
- Events are immutable records; they must never be mutated after creation.
- One aggregate operation may raise multiple events, but order them to match causality.

```csharp
// Event definition
public sealed record LedgerEntryRecorded(
    LedgerEntryId EntryId,
    AccountId AccountId,
    Money Amount,
    DateTimeOffset OccurredOn) : IDomainEvent;

// Raising inside the aggregate
public void RecordCredit(Money amount, Description description, DateTimeOffset occurredOn)
{
    if (amount.Amount <= 0)
        throw new DomainException(DomainError.AmountMustBePositive);

    lines.Add(LedgerLine.Credit(amount));
    Raise(new LedgerEntryRecorded(Id, AccountId, amount, occurredOn));
}
```

### 6.1 Event Dispatcher Interface

The dispatcher interface lives in `Denarius.Application` — the domain has no dependency on it.

```csharp
// Application layer
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct);
}
```

---

## 7. Domain Services

- A **Domain Service** encapsulates domain logic that does not naturally belong to a single aggregate or value object.
- Domain services live in `Denarius.Domain` and depend only on domain abstractions — no infrastructure, no EF Core, no HTTP.
- They are stateless. All state comes from arguments; results are returned, not stored internally.
- Name them after what they do in the domain language: `FundsTransferService`, `BalanceProjector`.
- Do not turn domain services into catch-all bags. If a method could live on an aggregate, put it there.

---

## 8. Repositories

- Define repository **interfaces** in `Denarius.Domain` — one per aggregate root, never per entity.
- Repository interface names follow `I{AggregateName}Repository`.
- Implementations live in `Denarius.Infrastructure` and depend on EF Core.
- Repositories load and save aggregates in full — partial loading is forbidden.
- Never expose `IQueryable` from a repository. Queries belong in dedicated read-model query objects.
- Add only the query methods the domain actually needs. Do not add speculative overloads.
- **Repositories never call `SaveChanges` directly.** Persistence is committed by the Application layer via `IUnitOfWork`, giving the handler control over transaction boundaries.

```csharp
// Domain layer
public interface ILedgerEntryRepository
{
    Task<LedgerEntry?> FindByIdAsync(LedgerEntryId id, CancellationToken ct);
    Task AddAsync(LedgerEntry entry, CancellationToken ct);
}

// Application layer
public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken ct);
}
```

---

## 9. Application Layer (Use Cases)

- Application services (command/query handlers) orchestrate the flow: load aggregate → invoke domain logic → persist → dispatch events.
- They contain **no domain logic** — if you are writing an `if` that enforces a business rule in a handler, move it to the aggregate.
- One handler per use case. Handlers are thin coordinators.
- Commands mutate state and return `Result` or `Result<T>` (no raw data objects from commands).
- Queries are read-only, bypass the domain model if needed, and never trigger side effects.
- Input validation (request shape) lives in FluentValidation validators, not inside handlers.

```
Handler responsibility: who, what, in what order — not why or how the domain works.
```

### 9.1 Command Handler — Full Example

```csharp
// Correct: handler as a thin coordinator
internal sealed class RecordCreditCommandHandler(
    ILedgerEntryRepository repository,
    IUnitOfWork unitOfWork,
    IDomainEventDispatcher dispatcher)
    : ICommandHandler<RecordCreditCommand>
{
    public async Task<Result> Handle(RecordCreditCommand cmd, CancellationToken ct)
    {
        // 1. Load aggregate
        var entry = await repository.FindByIdAsync(cmd.EntryId, ct);
        if (entry is null) return Result.Failure(DomainError.EntryNotFound);

        // 2. Build value objects (may fail for domain reasons)
        var moneyResult = Money.Create(cmd.Amount, cmd.Currency);
        if (moneyResult.IsFailure) return moneyResult.Error;

        // 3. Invoke domain logic — all business rules live here, inside the aggregate
        entry.RecordCredit(moneyResult.Value, cmd.Description, cmd.OccurredOn);

        // 4. Persist, then dispatch events
        await unitOfWork.CommitAsync(ct);
        await dispatcher.DispatchAsync(entry.DomainEvents, ct);
        entry.ClearDomainEvents();

        return Result.Success();
    }
}

// Wrong: business rule leaking into the handler
if (entry.IsClosed)
    return Result.Failure(DomainError.LedgerClosed); // belongs on the aggregate
```

### 9.2 Query Handler — Full Example

```csharp
// Queries bypass the domain model and hit the read side directly
internal sealed class GetAccountSummaryQueryHandler(IDbConnectionFactory db)
    : IQueryHandler<GetAccountSummaryQuery, AccountSummaryDto>
{
    public async Task<AccountSummaryDto> Handle(GetAccountSummaryQuery query, CancellationToken ct)
    {
        // Read-only: no aggregate loaded, no events raised, no UnitOfWork
        return await db.QuerySingleAsync<AccountSummaryDto>(
            Sql.AccountSummary,
            new { AccountId = query.AccountId.Value },
            ct);
    }
}
```

---

## 10. Factories

- Use a **Factory** when construction of an aggregate or complex value object is non-trivial.
- Factories live in the domain layer (static method on the root or a dedicated factory class).
- Factory methods return `Result<T>` when creation can fail for domain reasons.
- Do not use constructors with many parameters as a substitute for a factory — factories make intent explicit.

```csharp
public static Result<LedgerEntry> Open(AccountId accountId, Money initialBalance, DateTimeOffset openedOn)
{
    if (initialBalance.Amount < 0)
        return Result.Failure<LedgerEntry>(DomainError.NegativeBalance);

    var entry = new LedgerEntry(LedgerEntryId.New(), accountId, initialBalance, openedOn);
    entry.Raise(new LedgerEntryRecorded(entry.Id, accountId, initialBalance, openedOn));
    return Result.Success(entry);
}
```

---

## 11. Anti-Corruption Layer (ACL)

- When integrating with an external system or a different bounded context, always translate through an ACL.
- ACL translators live in `Denarius.Infrastructure` or in a dedicated integration project.
- The domain model is never polluted with external DTOs or foreign identifiers.
- Map external concepts to internal Value Objects at the boundary before passing data inward.

---

## 12. Invariant Enforcement

- Invariants are business rules that must **always** hold for an aggregate to be valid.
- Enforce invariants inside the aggregate method that changes state — throw a domain exception or return a `Result.Failure` immediately.
- Never defer invariant checks to the application layer or validators.
- Document each invariant with a `// Invariant:` comment only when it is non-obvious from the code.

```csharp
public void ApplyDebit(Money amount)
{
    if (amount.Amount <= 0)
        throw new DomainException(DomainError.AmountMustBePositive);

    // Invariant: a closed ledger period accepts no further mutations.
    if (status == LedgerStatus.Closed)
        throw new DomainException(DomainError.LedgerClosed);

    lines.Add(LedgerLine.Debit(amount));
    Raise(new DebitApplied(Id, amount, DateTimeOffset.UtcNow));
}
```

---

## 13. Ledger-Specific Rules (Denarius)

- **Balance is never stored.** It is always computed as `SUM` of ledger line amounts at query time.
- A `LedgerEntry` is the aggregate root. `LedgerLine` (debit/credit rows) are internal entities.
- Credit lines have positive amounts; debit lines have negative amounts — convention is enforced by factory methods on `LedgerLine`, never by callers.
- `Money` is a Value Object containing `Amount` (decimal) and `Currency`. Arithmetic between different currencies is a domain error.
- Closing a ledger period is an explicit domain operation — it raises an event and prevents further mutations.
- Account identifiers cross context boundaries as typed Value Objects (`AccountId`), never as raw `Guid`.

### 13.1 LedgerLine Sign Convention

The sign convention (credit = positive, debit = negative) is a domain invariant. It must be impossible for a caller to create a `LedgerLine` with the wrong sign. Internal factory methods are the only entry point.

```csharp
internal sealed class LedgerLine : Entity<LedgerLineId>
{
    public Money Amount { get; }

    private LedgerLine(LedgerLineId id, Money amount) : base(id) =>
        Amount = amount;

    // Factories encapsulate the sign convention — callers never set the sign directly
    internal static LedgerLine Credit(Money amount) =>
        new(LedgerLineId.New(), amount with { Amount = Math.Abs(amount.Amount) });

    internal static LedgerLine Debit(Money amount) =>
        new(LedgerLineId.New(), amount with { Amount = -Math.Abs(amount.Amount) });
}
```

---

## 14. Domain Testing

Domain logic is the highest-value code in the codebase — it must be the most thoroughly tested.

- Domain tests require **no infrastructure**. No EF Core, no mocks of repositories, no HTTP clients.
- Arrange aggregates through their factory methods. Do not use constructors or object initialisers to bypass domain rules in tests.
- Assert on two things only: **domain events raised** and **state observable through the aggregate root**.
- Never assert on internal collections directly — if a behaviour is not visible through the root's public API, it is not part of the contract.
- Test names follow `{Method}_When{Condition}_Should{Outcome}`.

```csharp
public sealed class LedgerEntryTests
{
    [Fact]
    public void RecordCredit_WhenAmountIsPositive_ShouldRaiseLedgerEntryRecorded()
    {
        // Arrange
        var entry = LedgerEntry
            .Open(AccountId.New(), Money.Create(0, Currency.BRL).Value, DateTimeOffset.UtcNow)
            .Value;

        entry.ClearDomainEvents(); // clear the Open event before the act

        var credit = Money.Create(100, Currency.BRL).Value;

        // Act
        entry.RecordCredit(credit, new Description("Salary"), DateTimeOffset.UtcNow);

        // Assert
        var evt = entry.DomainEvents.OfType<LedgerEntryRecorded>().Single();
        Assert.Equal(credit, evt.Amount);
    }

    [Fact]
    public void RecordCredit_WhenLedgerIsClosed_ShouldThrowDomainException()
    {
        // Arrange
        var entry = LedgerEntry
            .Open(AccountId.New(), Money.Create(0, Currency.BRL).Value, DateTimeOffset.UtcNow)
            .Value;

        entry.Close(DateTimeOffset.UtcNow);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            entry.RecordCredit(
                Money.Create(50, Currency.BRL).Value,
                new Description("Late entry"),
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Open_WhenInitialBalanceIsNegative_ShouldReturnFailure()
    {
        // Arrange & Act
        var result = LedgerEntry.Open(
            AccountId.New(),
            Money.Create(-1, Currency.BRL).Value,
            DateTimeOffset.UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(DomainError.NegativeBalance, result.Error);
    }
}
```

---

## 15. Layer Dependency Cheat-Sheet

```
Domain       → CrossCutting only
Application  → Domain, Infrastructure (interfaces), CrossCutting
Infrastructure → Domain, CrossCutting, EF Core, external SDKs
Api          → Application, CrossCutting
IoC          → Application, Infrastructure, Domain, CrossCutting (NOT Api)
```

Violations of this dependency graph are build errors — do not suppress them.

---

## 16. Naming Conventions Quick Reference

| Concept            | Suffix / Pattern             | Example                          |
|--------------------|------------------------------|----------------------------------|
| Aggregate root     | (none)                       | `LedgerEntry`                    |
| Entity (internal)  | (none)                       | `LedgerLine`                     |
| Value Object       | (none or descriptive noun)   | `Money`, `AccountId`             |
| Domain Event       | `{Noun}{PastVerb}`           | `LedgerEntryRecorded`            |
| Domain Service     | `{Noun}Service`              | `FundsTransferService`           |
| Repository iface   | `I{Root}Repository`          | `ILedgerEntryRepository`         |
| Command            | `{Verb}{Noun}Command`        | `RecordCreditCommand`            |
| Command Handler    | `{Verb}{Noun}CommandHandler` | `RecordCreditCommandHandler`     |
| Query              | `Get{Noun}Query`             | `GetAccountSummaryQuery`         |
| Query Handler      | `Get{Noun}QueryHandler`      | `GetAccountSummaryQueryHandler`  |
| Factory method     | `Create` / `Open` / `Issue`  | `LedgerEntry.Open(...)`          |
| ACL Translator     | `{Source}To{Target}Mapper`   | `PlaidTransactionToEntryMapper`  |
| Unit of Work       | `IUnitOfWork`                | `IUnitOfWork`                    |
| Event Dispatcher   | `IDomainEventDispatcher`     | `IDomainEventDispatcher`         |
