using System;

namespace BuildingBlocks.Infrastructure;

public class IntegrationMessage
{
    public Guid CorrelationId { get; set; }
    public ApplicationUser.ApplicationUser User { get; set; }
    public string MessageName { get; set; }
    public string Inner { get; set; }

}
