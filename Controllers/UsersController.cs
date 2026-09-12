
using Freegram.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Freegram.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly FreegramDbContext _context;

    public UsersController(FreegramDbContext context)
    {
        _context = context;
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers(
        [FromQuery] string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return BadRequest("Nickname is required.");

        var users = await _context.Users
            .Where(u => EF.Functions.ILike(
                u.Nickname,
                $"%{nickname}%"))
            .Select(u => new
            {
                u.Id,
                u.Nickname
            })
            .Take(20)
            .ToListAsync();

        return Ok(users);
    }
}

