using CDC.Common.Contracts;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

/// <summary>Builders shared by the tests of the profile section tab pages.</summary>
internal static class ProfileTabTestData
{
    public static readonly Guid ProfileId = Guid.NewGuid();
    public static readonly Guid ProfileVersionId = Guid.NewGuid();
    public static readonly Guid SummarySectionId = Guid.NewGuid();
    public static readonly Guid EpidemiologySectionId = Guid.NewGuid();
    public static readonly Guid QuestionId = Guid.NewGuid();

    /// <summary>A profile with a current version and no tab permissions.</summary>
    public static ManageProfileViewModel Profile(Guid? currentProfileVersionId = null) => new()
    {
        ProfileId = ProfileId,
        ProfileTitle = "Bovine tuberculosis",
        CurrentProfileVersionId = currentProfileVersionId ?? ProfileVersionId
    };

    /// <summary>A current draft a profile editor may edit, with every tab visible.</summary>
    public static ManageProfileViewModel DraftProfile() =>
        Profile() with
        {
            CanEditProfileSection = true,
            CanEditAllSections = true,
            CanEditAuthorReviewDates = true,
            CanEditVaReviewDates = true,
            CanEditPolicyReviewDates = true,
            CanAddReviewComments = true,
            CanClearProfileSection = true,
            CanPrintProfileSection = true,
            CanViewNotes = true,
            CanViewReviewComments = true,
            CanViewContributions = true,
            CanViewAllRevisionDates = true
        };

    /// <summary>A current draft read-only to the user, who can still see every tab.</summary>
    public static ManageProfileViewModel ReadOnlyDraftProfile() =>
        DraftProfile() with
        {
            CanEditProfileSection = false,
            CanEditAllSections = false,
            CanEditAuthorReviewDates = false,
            CanEditVaReviewDates = false,
            CanEditPolicyReviewDates = false,
            CanAddReviewComments = false,
            CanClearProfileSection = false
        };

    /// <summary>Two sections, the first holding one per-species question.</summary>
    public static ProfileQuestionnaireMetadataDto TwoSectionMetadata() => new()
    {
        Sections =
        [
            new ProfileSectionMetadataDto
            {
                Id = SummarySectionId,
                Name = "Summary",
                ShortName = "SUMMARY",
                SectionNumber = 1,
                Questions =
                [
                    new ProfileQuestionMetadataDto
                    {
                        Id = QuestionId,
                        SectionId = SummarySectionId,
                        ShortName = "Overview",
                        QuestionNumber = 1,
                        IsPerSpecies = true,
                        IsRepeating = true,
                        Fields =
                        [
                            new ProfileFieldMetadataDto
                            {
                                Id = Guid.NewGuid(),
                                QuestionId = QuestionId,
                                Name = "Is it endemic?",
                                FieldNumber = 1,
                                DataTypeName = "Boolean"
                            }
                        ]
                    }
                ]
            },
            new ProfileSectionMetadataDto
            {
                Id = EpidemiologySectionId,
                Name = "Epidemiology",
                ShortName = "Epi",
                SectionNumber = 2,
                Questions = []
            }
        ]
    };

    /// <summary>Gives a page model the route values, form, URL helper and metadata provider a request would.</summary>
    public static TPage Configure<TPage>(TPage pageModel, IDictionary<string, string>? form = null, Guid? section = null)
        where TPage : ProfileSectionTabPageModel
    {
        var request = new DefaultHttpContext();

        if (form is not null)
        {
            request.Request.Form = new FormCollection(form.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Value)));
        }

        pageModel.ProfileId = ProfileId;
        pageModel.Section = section;
        pageModel.PageContext = new PageContext
        {
            HttpContext = request,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
        pageModel.MetadataProvider = new EmptyModelMetadataProvider();
        pageModel.Url = new PageRouteUrlHelper();

        return pageModel;
    }

    /// <summary>Resolves <c>Url.Page(...)</c> to <c>{page}?{handler=...&amp;}key=value&amp;...</c> so tests can assert on links.</summary>
    private sealed class PageRouteUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new(new DefaultHttpContext(), new RouteData(), new PageActionDescriptor());

        public string? Action(UrlActionContext actionContext) => throw new NotSupportedException();

        public string? Content(string? contentPath) => throw new NotSupportedException();

        public bool IsLocalUrl(string? url) => throw new NotSupportedException();

        public string? Link(string? routeName, object? values) => throw new NotSupportedException();

        public string? RouteUrl(UrlRouteContext routeContext)
        {
            var values = new RouteValueDictionary(routeContext.Values);
            var page = values["page"];
            values.Remove("page");

            return $"{page}?{string.Join("&", values.Select(pair => $"{pair.Key}={pair.Value}"))}";
        }
    }
}
