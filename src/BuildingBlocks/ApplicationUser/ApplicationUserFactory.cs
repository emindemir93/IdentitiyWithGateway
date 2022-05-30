using System.Security.Claims;

namespace BuildingBlocks.ApplicationUser;
public interface IApplicationUserFactory
{
    ApplicationUser Create(ClaimsPrincipal claims);

    ApplicationUser Create(ApplicationUser source);

    void Dispose();
}
public class ApplicationUserFactory : IApplicationUserFactory
{
    private readonly IApplicationUserAccessor _applicationUserAccessor;

    public ApplicationUserFactory()
       : this(null)
    { }

    public ApplicationUserFactory(IApplicationUserAccessor applicationUserAccessor)
    {
        _applicationUserAccessor = applicationUserAccessor;
    }

    public ApplicationUser Create(ClaimsPrincipal claims)
    {
        var applicationUser = claims.ToApplicationUser();

        if (_applicationUserAccessor != null)
        {
            _applicationUserAccessor.CurrentUser = applicationUser;
        }

        return applicationUser;
    }

    public ApplicationUser Create(ApplicationUser source)
    {
        if (_applicationUserAccessor != null)
        {
            _applicationUserAccessor.CurrentUser = source;
        }
        return source;
    }

    public void Dispose()
    {
        if (_applicationUserAccessor != null)
        {
            _applicationUserAccessor.CurrentUser = null;
        }
    }
}
