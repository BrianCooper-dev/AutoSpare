using System.ComponentModel.DataAnnotations;

namespace AutoSpare.Application.Models;

public class LoginModel
{
    [Required(ErrorMessage = "نام کاربری الزامی است.")]
    [MinLength(3, ErrorMessage = "نام کاربری حداقل باید ۳ کاراکتر باشد.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور الزامی است.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
