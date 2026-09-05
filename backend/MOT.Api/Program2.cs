using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Cors;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddControllers();

var app = builder.Build();
app.UseRouting();
app.UseCors("AllowAll");
app.MapControllers().RequireCors("AllowAll");
app.Run("http://localhost:5002");
