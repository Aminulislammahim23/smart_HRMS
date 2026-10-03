using smartHRMS.Api.Auth;
using smartHRMS.Api.Extensions;
using smartHRMS.Api.Middleware;
using smartHRMS.Api.OpenApi;
using smartHRMS.Application;
using smartHRMS.Application.Common.Calendar;
using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Features.Auth;
using smartHRMS.Application.Features.Payroll;
using smartHRMS.Application.Features.EmployeeDocuments;
using smartHRMS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

//add services
builder.Services.AddApiControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(options => options.AddOperationTransformer<FormFileOperationTransformer>());

// CORS: only the browser origins listed in "Cors:AllowedOrigins" may call the API (Development lists the React
// dev server). With no origins configured, cross-origin browser calls are refused.
const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition")));

//error handling
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();

//application/infrastructure registration
var documentsSection = builder.Configuration.GetSection(EmployeeDocumentOptions.SectionName);
var documentOptions = documentsSection.Get<EmployeeDocumentOptions>() ?? new EmployeeDocumentOptions();
var attendanceOptions = builder.Configuration.GetSection(AttendanceOptions.SectionName).Get<AttendanceOptions>() ?? new AttendanceOptions();
var workCalendarOptions = builder.Configuration.GetSection(WorkCalendarOptions.SectionName).Get<WorkCalendarOptions>() ?? new WorkCalendarOptions();
var payrollOptions = builder.Configuration.GetSection(PayrollOptions.SectionName).Get<PayrollOptions>() ?? new PayrollOptions();
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddApplication(documentOptions, attendanceOptions, workCalendarOptions, payrollOptions, authOptions);

// Sign-in (JWT bearer). Every endpoint requires a signed-in user unless it is marked [AllowAnonymous].
using (var startupLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole()))
{
    builder.Services.AddAppAuthentication(builder.Configuration, builder.Environment, startupLoggerFactory.CreateLogger("SmartHRMS.Startup"));
}

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
// First, so CORS headers are also on error responses (4xx/5xx) and the browser lets the frontend read them.
app.UseCors(FrontendCorsPolicy);
app.UseExceptionHandler();
app.UseStatusCodePages(StatusCodeResponseWriter.WriteAsync);
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.EnsureBootstrapAdminAsync();
app.Run();
