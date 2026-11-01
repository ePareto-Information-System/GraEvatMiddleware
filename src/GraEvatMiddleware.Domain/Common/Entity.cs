namespace GraEvatMiddleware.Domain.Common;

public abstract class Entity<TId>(TId id)
    where TId : notnull
{
    public TId Id { get; } = id;
}
