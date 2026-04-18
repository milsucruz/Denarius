using Denarius.Shared.SharedKernel.Domain.Interfaces;

namespace Denarius.Shared.SharedKernel.Domain
{
    public class AggregateRoot : Entity
    {
        private readonly List<IDomainEvent> domainEvents = new();
        public IReadOnlyCollection<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();

        protected void AddDomainEvent(IDomainEvent domainEvent)
        {
            domainEvents.Add(domainEvent);
        }

        public void ClearDomainEvents() => domainEvents.Clear();
    }
}
