using System.Security.Claims;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Safqa.Application;
using Safqa.Domain;
namespace Safqa.API;

public class JwtOptions
{
    public string Key { get; set; } = ""; public string Issuer { get; set; } = "safqa"; public string Audience { get; set; } = "safqa-web";
    public int AccessMinutes { get; set; } = 15; public int RefreshDays { get; set; } = 14;
}

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    ClaimsPrincipal U => accessor.HttpContext?.User ?? throw new UnauthorizedAccessException();
    public Guid UserId => Guid.Parse(U.FindFirstValue("sub")!);
    public Guid OrganizationId => Guid.Parse(U.FindFirstValue("org")!);
    public UserRole Role => Enum.Parse<UserRole>(U.FindFirstValue("role")!);
}

public class TokenService(IOptions<JwtOptions> opt)
{
    readonly JwtOptions o = opt.Value;
    public string CreateAccess(IdentityUser<Guid> u, UserProfile p)
    {
        var d = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new("sub", u.Id.ToString()), new("email", u.Email!), new("role", p.Role.ToString()), new("org", p.OrganizationId.ToString()), new("name", p.FullName)]),
            Expires = DateTime.UtcNow.AddMinutes(o.AccessMinutes), Issuer = o.Issuer, Audience = o.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(d);
    }
    public RefreshToken NewRefresh(Guid userId, out string raw)
    {
        raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        return new RefreshToken { UserId = userId, TokenHash = Hash(raw), ExpiresAt = DateTime.UtcNow.AddDays(o.RefreshDays) };
    }
    public static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));
}

public class ValidationFilter(IServiceProvider sp) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        foreach (var arg in ctx.ActionArguments.Values.Where(a => a is not null))
        {
            var vt = typeof(IValidator<>).MakeGenericType(arg!.GetType());
            if (sp.GetService(vt) is IValidator v)
            {
                var r = await v.ValidateAsync(new ValidationContext<object>(arg));
                if (!r.IsValid) throw new ValidationException(r.Errors);
            }
        }
        await next();
    }
}

public class SubUserIdProvider : IUserIdProvider { public string? GetUserId(HubConnectionContext c) => c.User?.FindFirst("sub")?.Value; }

public static class Extensions
{
    /// Tenant filter + rep scoping (reps see only their own records; managers/admins see the whole org).
    public static IQueryable<T> ScopedTo<T>(this IQueryable<T> q, ICurrentUser me) where T : OwnedEntity
    {
        var org = me.OrganizationId; var uid = me.UserId; var rep = me.Role == UserRole.Rep;
        return rep ? q.Where(x => x.OrganizationId == org && x.OwnerId == uid) : q.Where(x => x.OrganizationId == org);
    }
}
