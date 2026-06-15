using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.WebHost.UseUrls("http://0.0.0.0:8080");

    builder.Host.UseSerilog((ctx, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .Enrich.FromLogContext()
           .Enrich.WithProperty("Service", "gateway")
           .WriteTo.Console(new RenderedCompactJsonFormatter());
    });

    // JWT Bearer — reads from docker-compose env vars (double-underscore = nested IConfiguration key)
    // Falls back to flat env var names for local dev. If key is absent (dev bootstrap), uses a
    // placeholder to avoid startup crash; token validation is still enforced per route in ocelot.json.
    var issuer = builder.Configuration["Jwt__Issuer"]
                 ?? builder.Configuration["JWT_ISSUER"]
                 ?? "campusconnect";
    var audience = builder.Configuration["Jwt__Audience"]
                   ?? builder.Configuration["JWT_AUDIENCE"]
                   ?? "campusconnect-clients";
    var signingKeyRaw = builder.Configuration["Jwt__SigningKey"]
                        ?? builder.Configuration["JWT_SIGNING_KEY"]
                        ?? string.Empty;

    // SymmetricSecurityKey requires at least 1 byte. When JWT_SIGNING_KEY is not set
    // (dev bootstrap with no .env), use a 32-byte placeholder so the Gateway starts.
    // In production, JWT_SIGNING_KEY MUST be set via env var / secrets manager.
    const string DevPlaceholderKey = "campus-connect-dev-placeholder-key-32b";
    var signingKey = string.IsNullOrWhiteSpace(signingKeyRaw)
        ? Encoding.UTF8.GetBytes(DevPlaceholderKey)
        : Encoding.UTF8.GetBytes(signingKeyRaw);

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(signingKey)
            };
        });

    builder.Services.AddAuthorization();

    // Ocelot reads routes from ocelot.json
    builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: false);
    builder.Services.AddOcelot(builder.Configuration);

    var app = builder.Build();

    // IMPORTANT: Health endpoint MUST be registered as middleware (not MapGet) because
    // Ocelot.Middleware.UseOcelot() calls app.Run() internally which terminates
    // the minimal API endpoint routing pipeline. A middleware registered BEFORE UseOcelot()
    // intercepts the request before Ocelot processes it.
    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Method == "GET" && ctx.Request.Path == "/health")
        {
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync(
                JsonSerializer.Serialize(new { status = "ok", service = "gateway" }));
            return;
        }
        await next(ctx);
    });

    await app.UseOcelot();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Gateway terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
