using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace smartHRMS.Api.OpenApi;

/// <summary>
/// The built-in OpenAPI document generator does not recognize an MVC action's <see cref="IFormFile"/>
/// parameter as a file upload — it reflects over IFormFile's own properties (FileName, Length, ...) and
/// describes the request body as <c>application/x-www-form-urlencoded</c>. That schema breaks the
/// "Try it out" file picker in Swagger UI. This transformer rewrites such operations to the correct
/// <c>multipart/form-data</c> shape (<c>type: string, format: binary</c> per file parameter).
/// </summary>
public sealed class FormFileOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        // ApiExplorer flattens an IFormFile parameter into its own properties (FileName, Length, ...)
        // rather than describing it as one file, so detect it from the action's real C# parameters instead.
        var fileParameterNames = context.Description.ActionDescriptor.Parameters
            .Where(parameter => typeof(IFormFile).IsAssignableFrom(parameter.ParameterType))
            .Select(parameter => parameter.Name)
            .ToList();

        if (fileParameterNames.Count == 0)
        {
            return Task.CompletedTask;
        }

        var properties = fileParameterNames.ToDictionary(
            name => name,
            IOpenApiSchema (_) => new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" });

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = properties,
                        Required = new HashSet<string>(fileParameterNames),
                    },
                },
            },
        };

        if (operation.Parameters is not null)
        {
            foreach (var name in fileParameterNames)
            {
                var stray = operation.Parameters.FirstOrDefault(p => p.Name == name);
                if (stray is not null)
                {
                    operation.Parameters.Remove(stray);
                }
            }
        }

        return Task.CompletedTask;
    }
}
