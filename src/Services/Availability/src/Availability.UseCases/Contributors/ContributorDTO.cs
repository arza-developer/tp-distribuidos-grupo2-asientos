using Availability.Core.ContributorAggregate;

namespace Availability.UseCases.Contributors;
public record ContributorDto(ContributorId Id, ContributorName Name, PhoneNumber PhoneNumber);
