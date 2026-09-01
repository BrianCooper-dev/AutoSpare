namespace AutoSpare.Domain.Common;

public abstract class BaseEntity<TId>
{
    public TId Id { get; protected set; } = default!;

    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

    public DateTime? LastModifiedAt { get; protected set; }

    public void UpdateModificationTime()
    {
        LastModifiedAt = DateTime.UtcNow;
    }
}

public abstract class BaseEntity : BaseEntity<Guid>
{
    protected BaseEntity()
    {
        Id = Guid.NewGuid();
    }
}
