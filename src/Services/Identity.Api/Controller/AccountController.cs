using System.Linq;
using BuildingBlocks.ApplicationUser;
using Identity.Api.Data;
using Identity.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Controller;

[Route("api/v1/[controller]/[action]")]
[ApiController]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly ApplicationDbContext _applicationDbContext;
    private readonly UserManager<Models.ApplicationUser> _userManager;
    private readonly IApplicationUserAccessor _applicationUserAccessor;


    public AccountController(ApplicationDbContext applicationDbContext, UserManager<Models.ApplicationUser> userManager, IApplicationUserAccessor applicationUserAccessor)
    {
        _applicationDbContext = applicationDbContext;
        _userManager = userManager;
        _applicationUserAccessor = applicationUserAccessor;
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(PasswordChangeModel model)
    {
        var test = _applicationUserAccessor.CurrentUser;
        var id = model.Id.Trim();

        if (string.IsNullOrEmpty(model.OldPassword) || string.IsNullOrEmpty(model.NewPassword) || string.IsNullOrEmpty(model.Id))
        {
            var exception = new Exception("fillEmptyFields");
            return Ok(new
            {
                Result = exception,
                Success = false,
                ErrorMessage = "fillEmptyFields"
            });
        }

        Models.ApplicationUser user;

        var isGuid = Guid.TryParse(id, out _);

        if (isGuid)
        {
            user = await _applicationDbContext.Users.Where(u => u.Id == id).Select(u => u).FirstOrDefaultAsync();
        }
        else
        {
            user = await _applicationDbContext.Users.Where(u => u.UserName == id).Select(u => u).FirstOrDefaultAsync();
        }

        if (user == null)
        {
            return BadRequest(new { Success = false, ErrorMessage = $"User with {id} can not be found" });
        }

        if (user.UserName.Length > 9)
        {
            if (model.NewPassword.Length != 6 || !model.NewPassword.All(char.IsDigit))
            {
                return Ok(new { Result = new Exception("passwordDoesntMeetRequirements"), Success = false, ErrorMessage = "passwordDoesntMeetRequirements" });
            }

            _userManager.PasswordValidators.Clear();
        }

        var tempCheckPasswordAsync = await _userManager.CheckPasswordAsync(user, model.OldPassword);

        if (!tempCheckPasswordAsync)
        {
            var exception = new Exception("passwordDoesNotMatch");
            return Ok(new { Result = exception, Success = false, ErrorMessage = "passwordDoesntMeetRequirements" });
        }

        var currentValidators = _userManager.PasswordValidators.ToList();



        var ChangePasswordAsync = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
        var exceptionList = new List<Exception>();

        if (user.UserName.Length > 9)
        {
            currentValidators.ForEach(_userManager.PasswordValidators.Add);
        }

        if (!ChangePasswordAsync.Succeeded)
        {
            foreach (var i in ChangePasswordAsync.Errors)
            {
                exceptionList.Add(new Exception(i.Description));

            }
            // return exceptionList;
            return Ok(new { Result = exceptionList, Success = false, ErrorMessage = "passwordDoesntMeetRequirements" });
        }
        await _userManager.UpdateAsync(user);

        return Ok(new { Result = "", Success = true, ErrorMessage = "" });
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ForgotPassword()
    {
        return Ok();
    }
}