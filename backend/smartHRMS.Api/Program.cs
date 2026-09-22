using smartHRMS.Api.Extensions;
using smartHRMS.Api.Middleware;
using smartHRMS.Api.OpenApi;
using smartHRMS.Application;
using smartHRMS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

//add services
builder.Services.AddApiControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(options => options.AddOperationTransformer<FormFileOperationTransformer>());

//error handling
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();

//application/infrastructure registration
builder.Services.AddApplication();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

// wwwroot is the only folder served as static content (below), so uploaded files never expose source,
// configuration, or any other part of the application.
var webRootPath = string.IsNullOrWhiteSpace(builder.Environment.WebRootPath)
    ? Path.Combine(builder.Environment.ContentRootPath, "wwwroot")
    : builder.Environment.WebRootPath;

builder.Services.AddInfrastructure(connectionString, webRootPath);

var app = builder.Build();

//http request pipeline
app.UseExceptionHandler();
app.UseStatusCodePages(StatusCodeResponseWriter.WriteAsync);
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "SmartHRMS API v1");
        options.DocumentTitle = "SmartHRMS API";
    });
}

if (!string.IsNullOrWhiteSpace(builder.Configuration["https_port"]) ||
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT")))
{
    app.UseHttpsRedirection();
}
app.UseAuthorization();
app.MapControllers();
app.Run();
