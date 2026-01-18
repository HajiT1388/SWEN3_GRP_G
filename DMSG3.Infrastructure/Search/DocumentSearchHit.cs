using System;

namespace DMSG3.Infrastructure.Search;

public record DocumentSearchHit(Guid Id, double? Score);
