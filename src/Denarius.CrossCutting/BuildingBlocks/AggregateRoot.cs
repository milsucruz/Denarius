using Denarius.CrossCutting.Events;

namespace Denarius.CrossCutting.BuildingBlocks;

/// <summary>
/// Base class for all aggregate roots. Owns the domain event collection; subclasses raise events via <see cref="Raise"/>.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) : base(id) { }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() =>
        _domainEvents.Clear();
}
