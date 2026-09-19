using System.Reflection;
using Microsoft.OpenApi;

namespace CDC.Api.Infrastructure.Swagger;

/// <summary>
/// Swagger/OpenAPI registration. The generated document is OpenAPI v3, version <c>v1</c>, and
/// is served from <c>/swagger</c>.
/// </summary>
public static class SwaggerConfiguration
{
    /// <summary>The OpenAPI document name.</summary>
    public const string DocumentName = "v1";

    /// <summary>Adds the API explorer and Swagger generator.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = "Surveillance Profiles API",
                Version = DocumentName,
                Description =
                    "Species reference data, questionnaire metadata and answer data for the Surveillance " +
                    "Profiles (D2R2) service. Migrated from the legacy ISpeciesDataService WCF endpoint."
            });

            // Drives operation summaries, parameter descriptions and the sample payloads in the
            // <remarks> blocks; produced by <GenerateDocumentationFile> in the project file.
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }

            options.SupportNonNullableReferenceTypes();
        });

        return services;
    }

    /// <summary>Serves the OpenAPI document and the Swagger UI.</summary>
    /// <param name="app">The application pipeline builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint($"/swagger/{DocumentName}/swagger.json", $"Surveillance Profiles API {DocumentName}");
            options.DocumentTitle = "Surveillance Profiles API";
        });

        return app;
    }
}
