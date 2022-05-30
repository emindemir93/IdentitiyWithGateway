using System;

namespace BuildingBlocks.Infrastructure.IntegrationEvents.Events
{
    public class IdentityUserTerminatedIntegrationEvent : IIntegrationMessage
    {
        public Guid PartyId { get; set; }
        public string UserName {get; set;}
    }

}