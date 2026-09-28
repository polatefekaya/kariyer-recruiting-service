namespace Kariyer.Recruiting.Domain.Abstractions;

public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _events = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _events;

    public void ClearDomainEvents() => _events.Clear();

    protected void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);
}

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
