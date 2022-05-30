using System;

namespace BuildingBlocks.ApplicationUser;
public class ApplicationUser
{
    public string ClientId { get; set; }
    public string UserName { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string Sub { get; set; }
    public Guid Key { get; set; }
    public string InternalOrganizationId { get; set; }
    public string MainPositionId { get; set; }
    public string TemporaryPositionId { get; set; }
    public string PositionTypeId { get; set; }
    public string RoleType { get; set; }
    public string Token { get; set; }
    public string CustomerId { get; set; }
    public string IdentityNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; }
    public bool EmailConfirmed { get; set; }
}