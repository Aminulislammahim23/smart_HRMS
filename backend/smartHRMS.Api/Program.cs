using smartHRMS.Api.Extensions;
using smartHRMS.Api.Middleware;
using smartHRMS.Api.OpenApi;
using smartHRMS.Application;
using smartHRMS.Application.Features.EmployeeDocuments;
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
var documentsSection = builder.Configuration.GetSection(EmployeeDocumentOptions.SectionName);
var documentOptions = documentsSection.Get<EmployeeDocumentOptions>() ?? new EmployeeDocumentOptions();
builder.Services.AddApplication(documentOptions);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

// wwwroot is the only folder served as static content (below), so uploaded files never expose source,
// configuration, or any other part of the application.
var webRootPath = string.IsNullOrWhiteSpace(builder.Environment.WebRootPath)
    ? Path.Combine(builder.Environment.ContentRootPath, "wwwroot")
    : builder.Environment.WebRootPath;

// Employee documents (NID, passport, ...) are private: they live outside wwwroot and are only streamed through the API.
var documentStoragePath = Path.GetFullPath(Path.Combine(
    builder.Environment.ContentRootPath,
    documentsSection["StoragePath"] ?? Path.Combine("App_Data", "employee-documents")));

if (documentStoragePath.StartsWith(Path.GetFullPath(webRootPath), StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"{EmployeeDocumentOptions.SectionName}:StoragePath must not be inside wwwroot, or documents would be publicly downloadable.");
}

builder.Services.AddInfrastructure(connectionString, webRootPath, documentStoragePath);

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
