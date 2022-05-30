using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity.UI.V4.Pages.Account.Manage.Internal;

namespace Identity.Api.Models;

public class PasswordChangeModel
{
    public string Id { get; set; }
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    [Required]
    public string OldPassword { get; set; }
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    [Required]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
    public string NewPassword { get; set; }
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; }
}