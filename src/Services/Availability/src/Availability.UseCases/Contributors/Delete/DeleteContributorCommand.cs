using Availability.Core.ContributorAggregate;

namespace Availability.UseCases.Contributors.Delete;

public record DeleteContributorCommand(ContributorId ContributorId) : ICommand<Result>;
