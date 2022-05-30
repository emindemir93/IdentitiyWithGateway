// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Identity.Api.Models.Enum;
using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Models;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    public string CustomerId { get; set; }
    public string PositionTypeId { get; set; }
    public string InternalOrganizationId { get; set; }
    public string MainPositionId { get; set; }
    public string IdentityNumber { get; set; }
    public string Name { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; }
    public RoleType RoleType { get; set; }

}
