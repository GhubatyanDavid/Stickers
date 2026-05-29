using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SoundSticker.Configuration;

public sealed class UserIdHeaderOperationFilter : IOperationFilter
{
    private const string UserIdHeaderName = "X-User-Id";
    private const string SecuritySchemeName = "UserId";

    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "api/health",
        "api/stickers/all"
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var relativePath = context.ApiDescription.RelativePath?.Trim('/');
        if (string.IsNullOrWhiteSpace(relativePath) || PublicPaths.Contains(relativePath))
        {
            return;
        }

        operation.Parameters ??= [];
        if (operation.Parameters.Any(parameter =>
                string.Equals(parameter.Name, UserIdHeaderName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = UserIdHeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Stable MVP user id generated and stored by the frontend.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String
            }
        });

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SecuritySchemeName, null)] = []
        });
    }
}
