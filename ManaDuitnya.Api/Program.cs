using Dapper;
using ManaDuitnya.Api.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ManaDuitnya.Api.Services;
using ManaDuitnya.Api.Converters;

DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);
var jwt     = builder.Configuration.GetSection("Jwt");


builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new WibDateTimeConverter()));
builder.Services.AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Default")!);
builder.Services.AddScoped<TransactionRepository>();
builder.Services.AddScoped<WalletRepository>();
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
        ClockSkew                   = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<RefreshTokenRepository>();
builder.Services.AddSingleton<TokenService>();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
