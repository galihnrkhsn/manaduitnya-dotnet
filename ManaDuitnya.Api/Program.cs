using Dapper;
using ManaDuitnya.Api.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ManaDuitnya.Api.Services;
using ManaDuitnya.Api.Converters;
using ManaDuitnya.Api.Extensions;

DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);
var jwt     = builder.Configuration.GetSection("Jwt");

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new WibDateTimeConverter()));
builder.Services.AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Default")!);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer      = true,
        ValidIssuer         = jwt["Issuer"],
        ValidateAudience    = true,
        ValidAudience       = jwt["Audience"],
        ValidateLifetime    = true,
        ValidateIssuerSigningKey    = true,
        IssuerSigningKey            = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
        ClockSkew                   = TimeSpan.Zero
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<RefreshTokenRepository>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => "ManaDuitnya API is running!");
app.Run();
