using MediatR;

namespace Denarius.Shared.SharedKernel.Domain.Interfaces
{
    public interface IDomainEvent : INotification
    {
        Guid Id { get; }
        DateTime OccurredOn { get; }
    }
}
