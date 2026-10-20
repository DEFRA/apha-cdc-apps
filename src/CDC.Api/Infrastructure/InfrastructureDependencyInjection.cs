using CDC.Api.Features.BespokeReportTemplates.Interfaces;
using CDC.Api.Features.DiseaseRanking.Interfaces;
using CDC.Api.Features.DiseaseRankingFilters.Interfaces;
using CDC.Api.Features.ExternalUsers.Interfaces;
using CDC.Api.Features.GlobalUsers.Interfaces;
using CDC.Api.Features.PrioritisationVariables.Interfaces;
using CDC.Api.Features.ProfileContributors.Interfaces;
using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Api.Features.ProfileNotes.Interfaces;
using CDC.Api.Features.ProfileQuestions.Interfaces;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Api.Features.ProfileSearch.Interfaces;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Api.Features.ReferenceData.Interfaces;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using CDC.Api.Features.Species.Interfaces;
using CDC.Api.Features.StaticReports.Interfaces;
using CDC.Api.Features.Users.Interfaces;
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
        services.AddScoped<IProfileContributorsRepository, ProfileContributorsRepository>();
        services.AddScoped<IProfileNoteRepository, ProfileNoteRepository>();
        services.AddScoped<IProfileQuestionRepository, ProfileQuestionRepository>();
        services.AddScoped<IProfileReportRepository, ProfileReportRepository>();
        services.AddScoped<IProfileReportContentRepository, ProfileReportContentRepository>();
        services.AddScoped<IBespokeReportTemplateRepository, BespokeReportTemplateRepository>();
        services.AddScoped<IStaticReportRepository, StaticReportRepository>();
        services.AddScoped<IDiseaseRankingRepository, DiseaseRankingRepository>();
        services.AddScoped<IPrioritisationVariablesRepository, PrioritisationVariablesRepository>();
        services.AddScoped<IDiseaseRankingFiltersRepository, DiseaseRankingFiltersRepository>();
        services.AddScoped<IProfileSectionRepository, ProfileSectionRepository>();
        services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IGlobalUsersRepository, GlobalUsersRepository>();
        services.AddScoped<IExternalUsersRepository, ExternalUsersRepository>();
        services.AddScoped<IReviewNotificationsRepository, ReviewNotificationsRepository>();

        return services;
    }
}
