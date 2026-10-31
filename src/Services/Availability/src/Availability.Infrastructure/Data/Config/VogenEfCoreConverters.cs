using Availability.Core.ContributorAggregate;
using Vogen;

namespace Availability.Infrastructure.Data.Config;

[EfCoreConverter<ContributorId>]
[EfCoreConverter<ContributorName>]
internal partial class VogenEfCoreConverters;
