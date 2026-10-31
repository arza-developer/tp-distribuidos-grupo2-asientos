using Availability.Core.ContributorAggregate.Events;
using Availability.Core.Interfaces;

namespace Availability.Core.ContributorAggregate.Handlers;

public class ContributorNameUpdatedEmailNotificationHandler(
  ILogger<ContributorNameUpdatedEmailNotificationHandler> logger,
  IEmailSender emailSender) : INotificationHandler<ContributorNameUpdatedEvent>
{
  public async ValueTask Handle(ContributorNameUpdatedEvent domainEvent, CancellationToken cancellationToken)
  {
    logger.LogInformation("Handling Contributor Name Updated event for {contributorId}", domainEvent.Contributor.Id);

    await emailSender.SendEmailAsync("to@test.com",
                                     "from@test.com",
                                     $"Contributor {domainEvent.Contributor.Id} Name Updated",
$"Contributor with id {domainEvent.Contributor.Id} had their name updated to {domainEvent.Contributor.Name}.");
  }
}
