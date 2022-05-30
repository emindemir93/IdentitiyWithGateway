using System;

namespace BuildingBlocks.Infrastructure.IntegrationEvents.Events
{
    public class IdentityUserCreatedIntegrationEvent : IIntegrationMessage
    {
        public Guid PartyId { get; set; }
        public string UserName { get; set; }
        public string Name { get; set; }
        public string IdentificationNumber { get; set; }
        public string Email { get; set; }
        public string Role{ get; set; }
    }
}