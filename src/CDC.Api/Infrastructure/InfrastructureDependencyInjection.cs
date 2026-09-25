using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Api.Features.ProfileNotes.Interfaces;
using CDC.Api.Features.ProfileQuestions.Interfaces;
using CDC.Api.Features.ProfileSearch.Interfaces;
using CDC.Api.Features.Species.Interfaces;
using CDC.Api.Infrastructure.Repositories;

namespace CDC.Api.Infrastructure;

/// <summary>
/// Registers the infrastructure layer: database connections and repositories.
/// </summary>
public static class InfrastructureDependencyInjection
{
    /// <summary>Adds the infrastructure layer to the container.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // The factory is stateless and builds a new connection per unit of work, so a single
        // instance is safe to share.
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<ISpeciesRepository, SpeciesRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IProfileManagementRepository, ProfileManagementRepository>();
        services.AddScoped<IProfileNoteRepository, ProfileNoteRepository>();
        services.AddScoped<IProfileQuestionRepository, ProfileQuestionRepository>();

        return services;
    }
}
