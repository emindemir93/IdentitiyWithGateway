using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.ApplicationUser;
public static class ApplicationUserExtensions
{
    internal static ApplicationUser ToApplicationUser(this ClaimsPrincipal claims)
    {
        if (claims == null)
        {
            throw new ArgumentNullException(nameof(claims));
        }

        return new ApplicationUser
        {
            ClientId = claims.FindFirstValue("client_id"),
            UserName = claims.FindFirstValue("UserName"),
            Name = claims.FindFirstValue("name"),
            InternalOrganizationId = claims.FindFirstValue("internal_organization_id"),
            Email = claims.FindFirstValue("email"),
            PhoneNumber = claims.FindFirstValue("phone_number"),
            Sub = claims.FindFirstValue("s"),
            PositionTypeId = claims.FindFirstValue("position_type_id"),
            Key = string.IsNullOrWhiteSpace(claims.FindFirstValue("key")) ? Guid.Empty : Guid.Parse(claims.FindFirstValue("key")),
            MainPositionId = claims.FindFirstValue("PositionId"),
            TemporaryPositionId = claims.FindFirstValue("temporary_position_id"),
            RoleType = claims.FindFirstValue("r"),
            Token = claims.FindFirstValue("t"),
            CustomerId = claims.FindFirstValue("customer_id")
        };
    }

    public static IServiceCollection AddApplicationUser(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton<IApplicationUserAccessor, ApplicationUserAccessor>();
        serviceCollection.TryAddTransient<IApplicationUserFactory, ApplicationUserFactory>();

        return serviceCollection;
    }

    public static IApplicationBuilder UseApplicationUser(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }

        if (app.ApplicationServices.GetService(typeof(IApplicationUserFactory)) == null)
        {
            throw new InvalidOperationException("Unable to find the required services. You must call the AddApplicationUser method in ConfigureServices in the application startup code.");
        }

        return app.UseMiddleware<ApplicationUserMiddleware>();
    }
}
