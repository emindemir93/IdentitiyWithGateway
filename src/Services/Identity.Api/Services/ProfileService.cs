using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Identity.Api.Models;
using Identity.Api.Models.Enum;
using IdentityModel;
using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Services;

public class ProfileService : IProfileService
{
    private readonly UserManager<ApplicationUser> _userManager;
    public ProfileService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var subject = context.Subject ?? throw new ArgumentNullException(nameof(context.Subject));

        var subjectId = subject.Claims.Where(x => x.Type == "sub").FirstOrDefault().Value;
        var user = await _userManager.FindByIdAsync(subjectId);
        var claims = await GetClaimsFromUser(user);
        context.IssuedClaims = claims.ToList();
    }

    public Task IsActiveAsync(IsActiveContext context)
    {
        context.IsActive = true;
        return Task.CompletedTask;
    }

    private async Task<IEnumerable<Claim>> GetClaimsFromUser(ApplicationUser user)
    {

        var claims = new List<Claim>
            {
                new Claim(JwtClaimTypes.Subject, user.Id),
                new Claim(JwtClaimTypes.Role, Enum.GetName<RoleType>(user.RoleType)),
                new Claim(JwtClaimTypes.PreferredUserName, user.UserName),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),

            };

        claims.AddRange(new[]{
                new Claim("t", user.SecurityStamp),
                new Claim("s", user.Id),
                new Claim("r", Enum.GetName<RoleType>(user.RoleType)),
                new Claim("u", user.UserName),
                new Claim("date_of_birth", user.DateOfBirth.ToString()),
                new Claim("identity_number", user.IdentityNumber)
                });
        if (!string.IsNullOrWhiteSpace(user.Id))
            claims.Add(new Claim("key", user.Id));

        if (!string.IsNullOrWhiteSpace(user.Name))
            claims.Add(new Claim("name", user.Name));

        if (!string.IsNullOrWhiteSpace(user.InternalOrganizationId))
            claims.Add(new Claim("internal_organization_id", user.InternalOrganizationId));

        if (!string.IsNullOrWhiteSpace(user.MainPositionId))
            claims.Add(new Claim("main_position_id", user.MainPositionId));

        if (!string.IsNullOrWhiteSpace(user.PositionTypeId))
            claims.Add(new Claim("position_type_id", user.PositionTypeId));

        if (!string.IsNullOrWhiteSpace(user.CustomerId))
            claims.Add(new Claim("customer_id", user.CustomerId));

        if (_userManager.SupportsUserEmail)
        {
            claims.AddRange(new[]
            {
                    new Claim(JwtClaimTypes.Email, user.Email==null?"":user.Email),
                    new Claim(JwtClaimTypes.EmailVerified, user.EmailConfirmed ? "true" : "false", ClaimValueTypes.Boolean)
                });
        }

        if (_userManager.SupportsUserPhoneNumber && !string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            claims.AddRange(new[]
            {
                    new Claim(JwtClaimTypes.PhoneNumber, user.PhoneNumber==null?"":user.PhoneNumber),
                    new Claim(JwtClaimTypes.PhoneNumberVerified, user.PhoneNumberConfirmed ? "true" : "false", ClaimValueTypes.Boolean)
                });
        }
        var audiences = new List<string> { "Identity" };
        audiences.ForEach(au => claims.Add(new Claim(JwtClaimTypes.Audience, au)));

        return claims;
    }
}
