using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MOT.Application.DTOs;
using MOT.Application.Validators;
using MOT.Domain.Interfaces;
using MOT.Infrastructure.Authentication;
using MOT.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") 
                       ?? builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? throw new InvalidOperationException("Connection string not found.");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddScoped<MOT.Application.Common.Interfaces.IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IJwtProvider, JwtProvider>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(RegisterRequest).Assembly));
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "SuperSecretKeyForMindOnTrackMVP123!@#";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "MOT.Api",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "MOT.Frontend",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

var app = builder.Build();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://*:{port}");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
}

app.UseHttpsRedirection();
app.UseRouting();

// Bulletproof Custom CORS Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
    context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
    context.Response.Headers.Append("Access-Control-Allow-Headers", "*");

    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 204;
        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
