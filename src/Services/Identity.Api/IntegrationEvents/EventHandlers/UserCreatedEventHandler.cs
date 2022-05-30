using Identity.Api.Models;
using Identity.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Infrastructure.IntegrationEvents.Events;
using DotNetCore.CAP;
using Identity.Api.Models.Enum;

namespace Services.Identity.Background.IntegrationEvents.EventHandlers
{
    public class IdentityEmployeeCreatedIntegrationEventHandler : ICapSubscribe
    {
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher = new PasswordHasher<ApplicationUser>();
        private readonly ApplicationDbContext _context;

        public IdentityEmployeeCreatedIntegrationEventHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        [CapSubscribe(nameof(IdentityUserCreatedIntegrationEvent))]
        public async Task ProcessAsync(IdentityUserCreatedIntegrationEvent @event)
        {
            if (!await _context.Users.AnyAsync(i => i.Id == @event.PartyId.ToString()))
            {

                var user = new ApplicationUser
                {
                    Email = @event.Email,
                    Id = @event.PartyId.ToString(),
                    Name = @event.Name,
                    PhoneNumber = string.Empty,
                    UserName = @event.UserName,
                    NormalizedEmail = @event.Email,
                    NormalizedUserName = @event.UserName,
                    SecurityStamp = Guid.NewGuid().ToString("D"),
                    PasswordHash = @event.IdentificationNumber
                };


                var role = await _context.Roles.FirstOrDefaultAsync(i => i.Name == @event.Role);
                if (role is null)
                {
                    role = new IdentityRole
                    {
                        Name = @event.Role,
                        Id = Guid.NewGuid().ToString()
                    };
                    _context.Roles.Add(role);
                }
                Enum.TryParse<RoleType>(role?.Id, out RoleType roleType);
                user.RoleType = roleType;

                user.PasswordHash = _passwordHasher.HashPassword(user, user.PasswordHash);
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
        }
    }
}