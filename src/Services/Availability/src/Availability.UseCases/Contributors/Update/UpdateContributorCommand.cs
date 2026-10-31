using Availability.Core.ContributorAggregate;

namespace Availability.UseCases.Contributors.Update;

public record UpdateContributorCommand(ContributorId ContributorId, ContributorName NewName) : ICommand<Result<ContributorDto>>;
