using System.Threading;

namespace BuildingBlocks.ApplicationUser;
public class ApplicationUserAccessor : IApplicationUserAccessor
{
    private static readonly AsyncLocal<ApplicationUser> _currentUserContext = new AsyncLocal<ApplicationUser>();

    public ApplicationUser CurrentUser
    {
        get => _currentUserContext.Value;
        set => _currentUserContext.Value = value;
    }
}

public interface IApplicationUserAccessor
{
    ApplicationUser CurrentUser { get; set; }
}
