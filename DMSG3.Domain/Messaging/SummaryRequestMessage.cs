using System;

namespace DMSG3.Domain.Messaging;

public record SummaryRequestMessage(Guid DocumentId);