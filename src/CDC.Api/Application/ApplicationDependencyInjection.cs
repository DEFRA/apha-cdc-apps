using CDC.Api.Application.Behaviours;
using CDC.Api.Features.ProfileSearch;
using CDC.Api.Features.ProfileSearch.Interfaces;
using CDC.Api.Features.Species;
using CDC.Api.Features.Species.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Application;

/// <summary>
/// Registers the application layer: MediatR handlers, the validation pipeline, FluentValidation
/// validators, and feature services.
/// </summary>
public static class ApplicationDependencyInjection
{
    /// <summary>Adds the application layer to the container.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationDependencyInjection).Assembly;

        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Registered after MediatR so that validation runs before any handler.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

        services.AddScoped<ISpeciesService, SpeciesService>();
        services.AddScoped<IProfileSearchService, ProfileSearchService>();

        return services;
    }
}
