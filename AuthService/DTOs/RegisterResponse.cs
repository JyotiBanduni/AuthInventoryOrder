namespace AuthService.DTOs;

public class RegisterResponse
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}