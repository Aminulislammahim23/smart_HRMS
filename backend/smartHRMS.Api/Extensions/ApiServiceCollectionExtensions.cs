using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Application.Common.Models;

namespace smartHRMS.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers controllers and makes automatic model-validation failures (400) use the standard
    /// <see cref="ApiResponse{T}"/> envelope instead of the default ValidationProblemDetails.
    /// </summary>
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        // Enums travel as their names ("FullTime", "Female"), matching how responses already expose Status.
        // Integer values are refused so an undefined number can't slip in as a valid enum.
        var enumConverter = new JsonStringEnumConverter(allowIntegerValues: false);

        // The OpenAPI document reads these (minimal-API) options, so Swagger also shows the enums as strings.
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(enumConverter));

        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(enumConverter))
            .ConfigureApiBehaviorOptions(options =>
            {
                // Let body-less 4xx results (415, NotFound(), ...) reach UseStatusCodePages so they get the envelope too.
                options.SuppressMapClientErrors = true;

                options.InvalidModelStateResponseFactory = context =>
                {
                    // JSON errors are keyed by path ("$" or "$.gender"). Their raw messages name internal .NET types,
                    // and they come with a misleading "The dto field is required." for the unbound body, so when the
                    // body couldn't be read we report only a short message per JSON path.
                    var jsonErrorKeys = context.ModelState.Keys.Where(key => key.StartsWith('$')).ToList();

                    var errors = jsonErrorKeys.Count > 0
                        ? jsonErrorKeys.Select(DescribeJsonError).Distinct().ToList()
                        : context.ModelState.Values
                            .SelectMany(entry => entry.Errors)
                            .Select(error => error.ErrorMessage)
                            .Where(message => !string.IsNullOrWhiteSpace(message))
                            .Distinct()
                            .ToList();

                    return new BadRequestObjectResult(
                        ApiResponse.Fail("One or more validation errors occurred.", errors));
                };
            });

        return services;
    }

    private static string DescribeJsonError(string key)
    {
        var field = key.TrimStart('$', '.');
        return field.Length == 0
            ? "The request body is not valid JSON."
            : $"The value for '{field}' is invalid.";
    }
}
