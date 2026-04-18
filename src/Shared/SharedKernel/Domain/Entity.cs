namespace Denarius.Shared.SharedKernel.Domain
{
    public abstract class Entity
    {
        public virtual Guid Id { get; init; }
        public virtual DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public virtual DateTime UpdatedAt { get; protected set; }
        public virtual string? CreatedBy { get; init; }
        public virtual string? UpdatedBy { get; protected set; }
    }
}
