namespace Availability.Core.ContributorAggregate.Events;

public sealed class ContributorNameUpdatedEvent(Contributor contributor) : DomainEventBase
{
  public Contributor Contributor { get; init; } = contributor;
}
