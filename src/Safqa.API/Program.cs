using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Safqa.API;
using Safqa.Application;
using Safqa.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var jwt = cfg.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrEmpty(jwt.Key) || jwt.Key.Length < 32)
{
    jwt.Key = "SafqaSecretKeyMustBe32CharactersLong2026!";
}

builder.Services.AddDbContext<SafqaDbContext>(o => o.UseNpgsql(cfg.GetConnectionString("Default")));
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<SafqaDbContext>());
builder.Services.AddIdentityCore<IdentityUser<Guid>>(o =>
{
    o.Password.RequiredLength = 8; o.User.RequireUniqueEmail = true;
    o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
}).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<SafqaDbContext>();

builder.Services.Configure<JwtOptions>(cfg.GetSection("Jwt"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidateAudience = jwt.Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ClockSkew = TimeSpan.FromSeconds(30), RoleClaimType = "role", NameClaimType = "sub"
    };
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var t = ctx.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(t) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs")) ctx.Token = t;
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<LeadRoutingService>();
builder.Services.AddScoped<QuotePdfService>();
builder.Services.AddSingleton<IUserIdProvider, SubUserIdProvider>();

builder.Services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<ConvertLeadCommand>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddValidatorsFromAssemblyContaining<ConvertLeadCommand>();
builder.Services.AddValidatorsFromAssemblyContaining<LoginValidator>();

builder.Services.AddControllers(o => o.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR();
builder.Services.Configure<ForwardedHeadersOptions>(o => { o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto; o.KnownNetworks.Clear(); o.KnownProxies.Clear(); });

var origins = cfg.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    static RateLimitPartition<string> PerIp(HttpContext c, int permits) => RateLimitPartition.GetFixedWindowLimiter(
        c.Connection.RemoteIpAddress?.ToString() ?? "anon", _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1) });
    o.AddPolicy("public-capture", c => PerIp(c, 10));
    o.AddPolicy("auth", c => PerIp(c, 20));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Safqa API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header, Description = "Paste the access token" });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } });
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<ExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");

// بيرد فوراً بـ 200 OK سواء Back4App فحص / أو /health
app.MapGet("/", () => Results.Ok(new { name = "Safqa CRM API", status = "healthy", version = "1.0.0" }));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// الاتصال بقاعدة البيانات والديمو يشتغل في الخلفية فور تشغيل السيرفر بدون تعطيل الـ Health Check
_ = Task.Run(async () =>
{
    try
    {
        await Task.Delay(1500);
        using var scope = app.Services.CreateScope();
        var demo = true;
        await DbInitializer.SeedAsync(scope.ServiceProvider, demo, "Safqa@Admin2026!");
        Console.WriteLine("[Safqa] Database seeded successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Safqa Seed Error] {ex.Message}");
    }
});

app.Run();
