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
        services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                // Let body-less 4xx results (415, NotFound(), ...) reach UseStatusCodePages so they get the envelope too.
                options.SuppressMapClientErrors = true;

                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState.Values
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
}
