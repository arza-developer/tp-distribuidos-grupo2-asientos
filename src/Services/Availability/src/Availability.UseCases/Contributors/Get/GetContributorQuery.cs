using Availability.Core.ContributorAggregate;

namespace Availability.UseCases.Contributors.Get;

public record GetContributorQuery(ContributorId ContributorId) : IQuery<Result<ContributorDto>>;
