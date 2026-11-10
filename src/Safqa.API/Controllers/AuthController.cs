using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Safqa.Application;
using Safqa.Infrastructure;
namespace Safqa.API.Controllers;

[ApiController, Route("api/v1/auth")]
public class AuthController(
    UserManager<IdentityUser<Guid>> um, 
    SafqaDbContext db, 
    TokenService ts, 
    ICurrentUser me,
    IHubContext<NotificationsHub> hub) : ControllerBase
{
    IActionResult Bad() => Unauthorized(new ProblemDetails { Status = 401, Title = "INVALID_CREDENTIALS", Detail = "Invalid e-mail or password" });

    async Task<AuthResponse> Issue(IdentityUser<Guid> u, Domain.UserProfile p, CancellationToken ct, Domain.RefreshToken? replace = null)
    {
        var rt = ts.NewRefresh(u.Id, out var raw);
        if (replace is not null) { replace.RevokedAt = DateTime.UtcNow; replace.ReplacedByHash = rt.TokenHash; }
        db.RefreshTokens.Add(rt);
        await db.SaveChangesAsync(ct);
        return new AuthResponse(ts.CreateAccess(u, p), raw, new UserDto(u.Id, u.Email!, p.FullName, p.Role, p.OrganizationId));
    }

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest r, CancellationToken ct)
    {
        var u = await um.FindByEmailAsync(r.Email);
        if (u is null || await um.IsLockedOutAsync(u)) return Bad();
        if (!await um.CheckPasswordAsync(u, r.Password)) { await um.AccessFailedAsync(u); return Bad(); }
        await um.ResetAccessFailedCountAsync(u);
        var p = await db.Profiles.FirstOrDefaultAsync(x => x.Id == u.Id, ct);
        if (p is null || !p.IsActive) return StatusCode(403, new ProblemDetails { Status = 403, Title = "ACCOUNT_DISABLED", Detail = "Account is disabled" });
        return Ok(await Issue(u, p, ct));
    }

    [HttpPost("reset-password"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrWhiteSpace(r.NewPassword) || r.NewPassword.Length < 8)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "INVALID_INPUT", Detail = "Password must be at least 8 characters." });
        }

        var u = await um.FindByEmailAsync(r.Email);
        if (u is null)
        {
            return NotFound(new ProblemDetails { Status = 404, Title = "USER_NOT_FOUND", Detail = "User with this email was not found." });
        }

        await um.RemovePasswordAsync(u);
        var result = await um.AddPasswordAsync(u, r.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new ProblemDetails { Status = 400, Title = "RESET_FAILED", Detail = errors });
        }

        var p = await db.Profiles.FirstOrDefaultAsync(x => x.Id == u.Id, ct);
        var userName = p?.FullName ?? u.Email!;

        // 1. تسجيل النشاط
        db.ActivityLogs.Add(new Domain.ActivityLog
        {
            ActorId = u.Id,
            EntityType = "User",
            EntityId = u.Id,
            Action = "PASSWORD_RESET",
            Description = $"قام المستخدم {userName} ({u.Email}) بتعيين كلمة مرور جديدة",
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        // 2. إرسال إشعار فوري للآدمن عبر SignalR
        await hub.Clients.All.SendAsync("passwordReset", new 
        { 
            email = u.Email, 
            name = userName,
            message = $"قام {userName} بتغيير كلمة المرور الخاصة به" 
        }, ct);

        return Ok(new { message = "Password reset successfully" });
    }

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh(RefreshRequest r, CancellationToken ct)
    {
        var hash = TokenService.Hash(r.RefreshToken);
        var t = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (t is null) return Bad();
        if (t.RevokedAt is not null)
        {
            await db.RefreshTokens.Where(x => x.UserId == t.UserId && x.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
            return Bad();
        }
        if (t.ExpiresAt < DateTime.UtcNow) return Bad();
        var u = await um.FindByIdAsync(t.UserId.ToString());
        var p = await db.Profiles.FirstOrDefaultAsync(x => x.Id == t.UserId, ct);
        if (u is null || p is null || !p.IsActive) return Bad();
        return Ok(await Issue(u, p, ct, t));
    }

    [HttpGet("me"), Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var u = await um.FindByIdAsync(me.UserId.ToString());
        var p = await db.Profiles.FirstAsync(x => x.Id == me.UserId, ct);
        return new UserDto(me.UserId, u!.Email!, p.FullName, p.Role, p.OrganizationId);
    }
}
