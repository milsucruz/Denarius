namespace Denarius.Shared.SharedKernel.Messaging
{
    public interface IIntegrationEvent
    {
        Guid Id { get; init; }
        DateTime CreatedAt { get; init; }
    }
}
