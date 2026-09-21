using Microsoft.EntityFrameworkCore;
using smartHRMS.Infrastructure.Data;


var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

//add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();


//database connection
builder.Services.AddDbContext<SmartHRMSDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

//http request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!string.IsNullOrWhiteSpace(builder.Configuration["https_port"]) ||
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT")))
{
    app.UseHttpsRedirection();
}
app.UseAuthorization();
app.MapControllers();
app.Run();
