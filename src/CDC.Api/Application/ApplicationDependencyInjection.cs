using CDC.Api.Application.Behaviours;
using CDC.Api.Features.ProfileManagement;
using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Api.Features.ProfileNotes;
using CDC.Api.Features.ProfileNotes.Interfaces;
using CDC.Api.Features.ProfileQuestions;
using CDC.Api.Features.ProfileQuestions.Interfaces;
using CDC.Api.Features.ProfileReports;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Api.Features.ProfileSearch;
using CDC.Api.Features.ProfileSearch.Interfaces;
using CDC.Api.Features.ProfileSections;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Api.Features.ReferenceData;
using CDC.Api.Features.ReferenceData.Interfaces;
using CDC.Api.Features.Species;
using CDC.Api.Features.Species.Interfaces;
using CDC.Api.Features.StaticReports;
using CDC.Api.Features.StaticReports.Interfaces;
using CDC.Api.Features.Users;
using CDC.Api.Features.Users.Interfaces;
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

        services.AddScoped<IUserContext, DefaultUserContext>();
        services.AddScoped<ISpeciesService, SpeciesService>();
        services.AddScoped<IProfileSearchService, ProfileSearchService>();
        services.AddScoped<IProfileManagementService, ProfileManagementService>();
        services.AddScoped<IProfileNoteService, ProfileNoteService>();
        services.AddScoped<IProfileQuestionService, ProfileQuestionService>();
        services.AddScoped<IProfileSectionService, ProfileSectionService>();
        services.AddScoped<IProfileReportService, ProfileReportService>();
        services.AddScoped<IStaticReportService, StaticReportService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
