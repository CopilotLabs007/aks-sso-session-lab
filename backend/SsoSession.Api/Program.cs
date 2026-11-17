using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SsoSession.Api.Authentication;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = JwtSettings.Load(builder.Configuration, builder.Environment);
var idpSettings = DemoIdpSettings.Load(builder.Configuration);
var frontendBaseUrl = builder.Configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";

builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(idpSettings);
builder.Services.AddSingleton(new FrontendSettings(frontendBaseUrl));
builder.Services.AddSingleton<AuthFlowStore>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwtSettings.SigningKey),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(jwtSettings.CookieName, out var token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (jwtSettings.UsesEphemeralSigningKey)
{
    app.Logger.LogWarning("Jwt:SigningKey not configured; using an in-memory development key. Restarting the API invalidates existing sessions.");
}

if (!idpSettings.IsConfigured)
{
    app.Logger.LogWarning("Simulated IdP demo credentials are not configured. Set DemoIdp:Username and DemoIdp:Password with dotnet user-secrets.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public sealed record FrontendSettings(string BaseUrl);

public partial class Program;
