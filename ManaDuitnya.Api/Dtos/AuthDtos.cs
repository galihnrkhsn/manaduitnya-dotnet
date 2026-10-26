using System.ComponentModel.DataAnnotations;

namespace ManaDuitnya.Api.Dtos;

public class RegisterRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, MinLength(8)] public string Password { get; set; } = "";
}

public class LoginRequest
{
    [Required] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public class RefreshRequest
{
    [Required] public string RefreshToken { get; set; } = "";
}

public class AuthResponse 
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";        
    public long UserId { get; set; }
    public string Name { get; set; } = "";        
    public string Email { get; set; } = "";        
}