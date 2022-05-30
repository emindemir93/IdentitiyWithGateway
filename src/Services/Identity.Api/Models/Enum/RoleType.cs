using System.ComponentModel.DataAnnotations;

namespace Identity.Api.Models.Enum;
public enum RoleType
{
    [Display(Name = "Admin")]
    Admin = 1,
    [Display(Name = "Customer")]
    Customer = 2,
    [Display(Name = "Officer")]
    Officer = 3
}
