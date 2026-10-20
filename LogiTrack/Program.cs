using System.Text;
using System.Threading.RateLimiting;
using LogiTrack.Models;
using LogiTrack.Util;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);


    builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
    {
        // Declare the bearer scheme so OpenAPI clients can send the JWT from /api/Auth/login
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            }] = []
        });
        return Task.CompletedTask;
    }));
    builder.Services.AddControllers();
    // Pooling reuses context instances across requests instead of building one per request
    builder.Services.AddDbContextPool<LogiTrackContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("LogiTrack")));
    builder.Services.AddProblemDetails();
    builder.Services.AddHealthChecks().AddDbContextCheck<LogiTrackContext>();
    // AddIdentityCore skips the cookie handlers AddIdentity registers; this API authenticates with JWT only
    builder.Services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<LogiTrackContext>()
        .AddSignInManager();

    // Per-IP limit on register/login to slow down scripted sign-ups and password guessing
    var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1) }));
    });
    // Behind a reverse proxy, take the client IP (used by the rate limiter) and scheme from X-Forwarded-*.
    // Only loopback proxies are trusted by default; add the real proxy to KnownProxies/KnownNetworks.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

    var jwt = builder.Configuration.GetSection("Jwt");
    var jwtKey = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwt["Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });
    builder.Services.AddAuthorization();
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<InventoryCacheSignal>();
    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await RoleSeeder.SeedRolesAsync(roleManager);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await RoleSeeder.SeedAdminAsync(userManager, app.Configuration);
    }
    app.UseForwardedHeaders();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }
    else
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();
    app.MapHealthChecks("/health");
    app.MapControllers();


    app.Run();

// Exposes the entry point to WebApplicationFactory in LogiTrack.Tests
public partial class Program;
