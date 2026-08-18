using AuthService.Data;
using AuthService.DTOs;
using AuthService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthDbContext _context;
    private readonly IConfiguration _configuration;
    public AuthController(
        AuthDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Password is required.");
        }

        if (request.Role != "ADMIN" && request.Role != "USER")
        {
            return BadRequest("Role must be ADMIN or USER.");
        }

        var existingUser = await _context.Users
            .FirstOrDefaultAsync(x => x.Username == request.Username);

        if (existingUser != null)
        {
            return Conflict("Username already exists.");
        }

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);

        await _context.SaveChangesAsync();

        var response = new RegisterResponse
        {
            UserId = user.UserId,
            Username = user.Username,
            Role = user.Role
        };

        return CreatedAtAction(
            nameof(Register),
            new { id = user.UserId },
            response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Username == request.Username);

        if (user == null)
        {
            return Unauthorized("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            return Unauthorized("User is inactive.");
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(
            request.Password,
            user.PasswordHash);

        if (!passwordValid)
        {
            return Unauthorized("Invalid username or password.");
        }

        var jwtKey = _configuration["Jwt:Key"]
                     ?? throw new InvalidOperationException("JWT Key is missing.");

        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        var expiryMinutes = int.Parse(
            _configuration["Jwt:ExpiryMinutes"] ?? "60");

        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
    {
        new Claim(
            ClaimTypes.NameIdentifier,
            user.UserId.ToString()),

        new Claim(
            ClaimTypes.Name,
            user.Username),

        new Claim(
            ClaimTypes.Role,
            user.Role)
    };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler()
            .WriteToken(token);

        return Ok(new LoginResponse
        {
            Token = tokenString,
            ExpiresAt = expiresAt,
            Username = user.Username,
            Role = user.Role
        });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.UserId == Guid.Parse(userId));

        if (user == null)
        {
            return NotFound("User not found.");
        }

        return Ok(new
        {
            user.UserId,
            user.Username,
            user.Role,
            user.IsActive,
            user.CreatedAt
        });
    }
}