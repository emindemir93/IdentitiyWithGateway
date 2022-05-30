using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace BuildingBlocks.ApplicationUser;
public class ApplicationUserMiddleware
{
    private readonly RequestDelegate _next;

    public ApplicationUserMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }
    public async Task Invoke(HttpContext context, IApplicationUserFactory applicationUserFactory)
    {
        applicationUserFactory.Create(context.User);

        await _next(context);

        applicationUserFactory.Dispose();
    }
}
