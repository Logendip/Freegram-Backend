using Freegram.Data;
using Freegram.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Freegram.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly FreegramDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(
        FreegramDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        UserRegisterDto request)
    {
        var nickname =
            request.Nickname?.Trim() ?? string.Empty;

        var password =
            request.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(nickname))
        {
            return BadRequest(
                "Nickname is required."
            );
        }

        if (nickname.Length < 3)
        {
            return BadRequest(
                "Nickname must contain at least 3 characters."
            );
        }

        if (nickname.Length > 30)
        {
            return BadRequest(
                "Nickname must contain at most 30 characters."
            );
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(
                "Password is required."
            );
        }

        if (password.Length < 6)
        {
            return BadRequest(
                "Password must contain at least 6 characters."
            );
        }

        var existingUser =
            await _context.Users
                .FirstOrDefaultAsync(
                    u => u.Nickname == nickname
                );

        if (existingUser != null)
        {
            return BadRequest(
                "This nickname is already taken."
            );
        }

        var passwordHash =
            HashPassword(password);

        var user = new User
        {
            Nickname = nickname,
            PasswordHash = passwordHash
        };

        _context.Users.Add(user);

        await _context.SaveChangesAsync();

        return Ok(
            new
            {
                message =
                    "User registered successfully.",
                user.Id,
                user.Nickname
            }
        );
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        UserLoginDto request)
    {
        var nickname =
            request.Nickname?.Trim() ?? string.Empty;

        var password =
            request.Password ?? string.Empty;

        if (
            string.IsNullOrWhiteSpace(
                nickname
            ) ||
            string.IsNullOrWhiteSpace(
                password
            )
        )
        {
            return Unauthorized(
                "Invalid nickname or password."
            );
        }

        var user =
            await _context.Users
                .FirstOrDefaultAsync(
                    u => u.Nickname == nickname
                );

        if (user == null)
        {
            return Unauthorized(
                "Invalid nickname or password."
            );
        }

        var passwordHash =
            HashPassword(password);

        if (
            user.PasswordHash !=
            passwordHash
        )
        {
            return Unauthorized(
                "Invalid nickname or password."
            );
        }

        var token =
            CreateToken(user);

        return Ok(
            new
            {
                message =
                    "Login successful.",
                token,
                user.Id,
                user.Nickname
            }
        );
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (userId == null)
        {
            return Unauthorized();
        }

        var user =
            await _context.Users
                .FirstOrDefaultAsync(
                    u =>
                        u.Id ==
                        int.Parse(userId)
                );

        if (user == null)
        {
            return NotFound();
        }

        return Ok(
            new
            {
                user.Id,
                user.Nickname
            }
        );
    }

    private string CreateToken(
        User user)
    {
        var claims =
            new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString()
                ),

                new Claim(
                    ClaimTypes.Name,
                    user.Nickname
                )
            };

        var key =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _configuration[
                        "Jwt:Key"
                    ]!
                )
            );

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

        var expirationMinutes =
            int.Parse(
                _configuration[
                    "Jwt:ExpirationMinutes"
                ] ?? "60"
            );

        var token =
            new JwtSecurityToken(
                issuer:
                    _configuration[
                        "Jwt:Issuer"
                    ],

                audience:
                    _configuration[
                        "Jwt:Audience"
                    ],

                claims: claims,

                expires:
                    DateTime.UtcNow.AddMinutes(
                        expirationMinutes
                    ),

                signingCredentials:
                    credentials
            );

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    private static string HashPassword(
        string password)
    {
        using var sha256 =
            SHA256.Create();

        var bytes =
            Encoding.UTF8.GetBytes(
                password
            );

        var hash =
            sha256.ComputeHash(bytes);

        return Convert.ToBase64String(
            hash
        );
    }
}

public class UserRegisterDto
{
    public string Nickname { get; set; }
        = string.Empty;

    public string Password { get; set; }
        = string.Empty;
}

public class UserLoginDto
{
    public string Nickname { get; set; }
        = string.Empty;

    public string Password { get; set; }
        = string.Empty;
}