using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>What the Profile properties page is being used for.</summary>
public enum ProfilePropertiesMode
{
    /// <summary>Creating a brand new profile.</summary>
    Create,

    /// <summary>Creating a new profile cloned from an existing profile version.</summary>
    CloneProfile,

    /// <summary>Creating a new "what-if" scenario cloned from an existing profile version.</summary>
    CloneScenario,

    /// <summary>Editing the title and affected species of an existing profile.</summary>
    Edit
}

/// <summary>
/// Razor Page for creating, cloning and editing a profile's properties: its title (or scenario title)
/// and the species it affects. Replaces the legacy <c>ProfileProperties.aspx</c>, which served all four
/// uses from one page chosen by its query string: <c>ProfileId</c> to edit, <c>CloneProfileVersionId</c>
/// to clone a profile, plus <c>Scenario=1</c> to clone a "what-if" scenario, and neither to create.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.CanCreateProfile)]
public class ProfilePropertiesModel(
    IApiClient apiClient,
    IProfilePropertiesApiService profilePropertiesApiService,
    ISpeciesApiService speciesApiService,
    ILogger<ProfilePropertiesModel> logger) : PageModel
{
    public const string ProfiledSpeciesFieldName = nameof(ProfiledSpeciesIds);
    public const string OtherSpeciesFieldName = nameof(OtherSpeciesIds);

    private const int MaxTitleLength = 255;
    private const string SavedMessage = "Your changes were successfully saved";

    private ProfileAttributesDto? attributes;
    private NewProfileDefaultsDto? defaults;
    private IReadOnlyList<SpeciesDto> species = [];

    /// <summary>Gets or sets the profile being edited, bound from the page route. Absent when creating or cloning.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? ProfileId { get; set; }

    /// <summary>Gets or sets the profile version to clone, bound from the query string. Absent when editing or creating.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? CloneProfileVersionId { get; set; }

    /// <summary>Gets or sets whether the clone becomes a "what-if" scenario: <c>true</c>, or the legacy <c>1</c>.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Scenario { get; set; }

    /// <summary>Gets or sets the title, or scenario title, which may contain the editor's HTML formatting.</summary>
    [BindProperty]
    public string? ProfileTitle { get; set; }

    /// <summary>Gets or sets the base64-encoded row version read with the profile, so a concurrent edit can be detected.</summary>
    [BindProperty]
    public string? LastUpdated { get; set; }

    /// <summary>Gets or sets the species ticked as profiled species.</summary>
    [BindProperty]
    public List<Guid> ProfiledSpeciesIds { get; set; } = [];

    /// <summary>Gets or sets the species ticked as other affected species.</summary>
    [BindProperty]
    public List<Guid> OtherSpeciesIds { get; set; } = [];

    /// <summary>Gets or sets the message shown once after a successful update.</summary>
    [TempData]
    public string? StatusMessage { get; set; }

    /// <summary>Gets what the page is being used for.</summary>
    public ProfilePropertiesMode Mode { get; private set; }

    /// <summary>Gets a value indicating whether the form is shown. False when the page could not be loaded or there is nothing to edit.</summary>
    public bool ShowForm { get; private set; }

    /// <summary>Gets a value indicating whether the affected species are shown. False for a profile with no current draft.</summary>
    public bool ShowSpeciesSelector { get; private set; }

    /// <summary>Gets the page heading. May contain the profile title's HTML formatting.</summary>
    public string Heading { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the title being edited is a "what-if" scenario's rather than a profile's.</summary>
    public bool IsScenarioTitle { get; private set; }

    /// <summary>Gets the description of the version being cloned, or <see langword="null"/> when not cloning. May contain HTML.</summary>
    public string? ClonedFromDescription { get; private set; }

    /// <summary>Gets the species tree for the species the profile is about.</summary>
    public TreeViewViewModel ProfiledSpeciesTree { get; private set; } = EmptyTree("profiled-species", ProfiledSpeciesFieldName);

    /// <summary>Gets the species tree for the other species the profile affects.</summary>
    public TreeViewViewModel OtherSpeciesTree { get; private set; } = EmptyTree("other-species", OtherSpeciesFieldName);

    /// <summary>Gets the label of the title field.</summary>
    public string TitleLabel => IsScenarioTitle ? "Scenario title" : "Profile title";

    /// <summary>Gets the message shown while the title has no text, as the legacy required field validator did.</summary>
    public string TitleRequiredMessage => $"You must enter a {TitleLabel.ToLowerInvariant()}";

    /// <summary>Gets the label of the button that saves the form.</summary>
    public string SubmitButtonText => Mode == ProfilePropertiesMode.Edit ? "Update" : "Create profile";

    /// <summary>Gets a value indicating whether the introduction to creating a profile is shown. It is not when editing.</summary>
    public bool ShowInfoPanel => Mode != ProfilePropertiesMode.Edit;

    /// <summary>Gets a value indicating whether removing a profiled species warns that its data will be lost - only when editing.</summary>
    public bool ShowRemovalWarning => Mode == ProfilePropertiesMode.Edit && ShowSpeciesSelector;

    /// <summary>Gets the profile whose "Manage profile" page is linked, when editing.</summary>
    public Guid? ManageProfileId => Mode == ProfilePropertiesMode.Edit ? ProfileId : null;

    /// <summary>Gets the last breadcrumb, naming this page.</summary>
    public string BreadcrumbCurrent => Mode switch
    {
        ProfilePropertiesMode.Create => "Create profile",
        ProfilePropertiesMode.CloneProfile => "Clone profile",
        ProfilePropertiesMode.CloneScenario => "Clone scenario",
        _ => "Profile properties"
    };

    private bool IsScenarioRequested =>
        Scenario == "1" || string.Equals(Scenario, "true", StringComparison.OrdinalIgnoreCase);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        InitialiseMode();

        var loaded = await LoadAsync(cancellationToken);

        if (loaded == LoadOutcome.NotFound)
        {
            return NotFound();
        }

        if (loaded == LoadOutcome.Ok)
        {
            PopulateInputsFromSource();
        }

        return RenderPage();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        InitialiseMode();

        var loaded = await LoadAsync(cancellationToken);

        if (loaded == LoadOutcome.NotFound)
        {
            return NotFound();
        }

        if (loaded == LoadOutcome.Failed)
        {
            return Page();
        }

        var title = (ProfileTitle ?? string.Empty).Trim();
        ValidateTitle(title);
        var selection = ShowSpeciesSelector ? ValidateSpecies() : null;

        if (!ModelState.IsValid)
        {
            return RenderPage();
        }

        return Mode == ProfilePropertiesMode.Edit
            ? await UpdateAsync(title, selection, cancellationToken)
            : await CreateAsync(title, selection!, cancellationToken);
    }

    private static TreeViewViewModel EmptyTree(string idPrefix, string fieldName) => new()
    {
        IdPrefix = idPrefix,
        FieldName = fieldName,
        Nodes = [],
        ItemNameSingular = "species",
        ItemNamePlural = "species",
        AllowMultipleSelection = true
    };

    private void InitialiseMode()
    {
        // A malformed clone id or scenario flag is treated as absent, as the legacy page treated an
        // invalid GUID as "create a new profile", rather than failing model binding.
        ModelState.Remove(nameof(CloneProfileVersionId));
        ModelState.Remove(nameof(Scenario));

        if (CloneProfileVersionId == Guid.Empty)
        {
            CloneProfileVersionId = null;
        }

        Mode = DetermineMode();
        IsScenarioTitle = Mode == ProfilePropertiesMode.CloneScenario;
        Heading = Mode switch
        {
            ProfilePropertiesMode.Create => "Create new profile",
            ProfilePropertiesMode.CloneProfile => "Clone new profile",
            ProfilePropertiesMode.CloneScenario => "Clone new \"what-if\" scenario",
            _ => "Profile properties"
        };
    }

    private ProfilePropertiesMode DetermineMode()
    {
        if (ProfileId is not null)
        {
            return ProfilePropertiesMode.Edit;
        }

        if (CloneProfileVersionId is null)
        {
            return ProfilePropertiesMode.Create;
        }

        return IsScenarioRequested ? ProfilePropertiesMode.CloneScenario : ProfilePropertiesMode.CloneProfile;
    }

    private async Task<LoadOutcome> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var outcome = Mode switch
            {
                ProfilePropertiesMode.Edit => await LoadProfileAsync(cancellationToken),
                ProfilePropertiesMode.Create => LoadOutcome.Ok,
                _ => await LoadCloneSourceAsync(cancellationToken)
            };

            if (outcome != LoadOutcome.Ok)
            {
                return outcome;
            }

            ShowSpeciesSelector = Mode != ProfilePropertiesMode.Edit || attributes!.CurrentDraftProfileVersionId != Guid.Empty;

            if (ShowSpeciesSelector)
            {
                species = await speciesApiService.GetAllSpeciesAsync(cancellationToken);
            }

            ShowForm = true;

            return LoadOutcome.Ok;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LoadFailed(exception);
            ModelState.AddModelError(string.Empty, "We could not load this page. Try again later.");

            return LoadOutcome.Failed;
        }
    }

    private async Task<LoadOutcome> LoadProfileAsync(CancellationToken cancellationToken)
    {
        attributes = await apiClient.GetProfileAttributesAsync(ProfileId!.Value, cancellationToken);

        if (attributes is null)
        {
            logger.ProfileNotFound(ProfileId.Value);

            return LoadOutcome.NotFound;
        }

        IsScenarioTitle = attributes.IsWhatIfScenario;
        Heading = $"Profile properties for {(attributes.IsWhatIfScenario ? $"{attributes.ParentTitle} ({attributes.ScenarioTitle})" : attributes.Title)}";

        if (!attributes.IsWhatIfScenario && attributes.CurrentDraftProfileVersionId == Guid.Empty)
        {
            return Refuse("There are no editable properties for this profile.");
        }

        return LoadOutcome.Ok;
    }

    private async Task<LoadOutcome> LoadCloneSourceAsync(CancellationToken cancellationToken)
    {
        var cloneId = CloneProfileVersionId!.Value;
        defaults = await profilePropertiesApiService.GetNewProfileDefaultsAsync(
            cloneId, Mode == ProfilePropertiesMode.CloneScenario, cancellationToken);

        if (defaults is null)
        {
            logger.CloneSourceNotFound(cloneId);

            return LoadOutcome.NotFound;
        }

        ClonedFromDescription = defaults.ClonedVersionDescription;

        if (Mode == ProfilePropertiesMode.CloneScenario)
        {
            Heading = $"Clone new \"what-if\" scenario for {defaults.ParentTitle}";
        }
        else if (defaults.ClonedVersionIsWhatIfScenario)
        {
            return Refuse("You cannot clone a new \"current situation\" profile from a profile version belonging to a what-if scenario.");
        }

        return LoadOutcome.Ok;
    }

    private LoadOutcome Refuse(string reason)
    {
        logger.NotEditable(ProfileId, reason);
        ModelState.AddModelError(string.Empty, reason);

        return LoadOutcome.Failed;
    }

    // Fills the form the way the legacy Page_Load did when the page was first requested.
    private void PopulateInputsFromSource()
    {
        IReadOnlyList<AffectedSpeciesDto> source = [];

        if (Mode == ProfilePropertiesMode.Edit)
        {
            ProfileTitle = attributes!.IsWhatIfScenario ? attributes.ScenarioTitle : attributes.Title;
            LastUpdated = Convert.ToBase64String(attributes.LastUpdated);
            source = attributes.AffectedSpecies;
        }
        else if (defaults is not null)
        {
            ProfileTitle = Mode == ProfilePropertiesMode.CloneScenario ? defaults.ScenarioTitle : defaults.Title;
            source = defaults.AffectedSpecies;
        }

        ProfiledSpeciesIds = [.. AffectedSpeciesSelection.TopMost(species, AffectedSpeciesSelection.IdsOfType(source, AffectedSpeciesTypes.Profiled))];
        OtherSpeciesIds = [.. AffectedSpeciesSelection.TopMost(species, AffectedSpeciesSelection.IdsOfType(source, AffectedSpeciesTypes.Other))];
    }

    private void ValidateTitle(string title)
    {
        if (ProfileTitleHtmlFormatter.ToPlainText(title).Length == 0)
        {
            ModelState.AddModelError(nameof(ProfileTitle), TitleRequiredMessage);
        }
        else if (title.Length > MaxTitleLength)
        {
            ModelState.AddModelError(nameof(ProfileTitle), $"{TitleLabel} cannot exceed {MaxTitleLength} characters");
        }
    }

    private AffectedSpeciesSelectionResult ValidateSpecies()
    {
        IEnumerable<AffectedSpeciesDto> alreadySelected = Mode == ProfilePropertiesMode.Edit
            ? attributes!.AffectedSpecies
            : defaults?.AffectedSpecies ?? [];

        var result = AffectedSpeciesSelection.Normalise(
            species,
            ProfiledSpeciesIds,
            OtherSpeciesIds,
            alreadySelected.Select(item => item.SpeciesId).ToHashSet());

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(ProfiledSpeciesFieldName, error);
        }

        return result;
    }

    private async Task<IActionResult> CreateAsync(string title, AffectedSpeciesSelectionResult selection, CancellationToken cancellationToken)
    {
        var isScenario = Mode == ProfilePropertiesMode.CloneScenario;
        var cloneId = CloneProfileVersionId ?? Guid.Empty;
        var newProfileId = Guid.NewGuid();
        var draftVersionId = Guid.NewGuid();

        var request = new CreateProfileRequest
        {
            Id = newProfileId,
            CurrentDraftProfileVersionId = draftVersionId,
            CloneProfileVersionId = cloneId,
            Title = isScenario ? string.Empty : title,
            ScenarioTitle = isScenario ? title : string.Empty,
            ParentId = isScenario ? defaults!.ParentId : Guid.Empty,
            ParentTitle = isScenario ? defaults!.ParentTitle : string.Empty,
            ProfileStatusId = defaults?.ProfileStatusId ?? Guid.Empty,
            AffectedSpeciesInsertList = AffectedSpeciesSelection.PlanCreate(draftVersionId, cloneId, selection.Profiled, selection.Other)
        };

        CreateProfileResult result;

        try
        {
            result = await profilePropertiesApiService.CreateProfileAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.SaveFailed(exception, newProfileId);
            result = new CreateProfileResult(SaveProfileOutcome.Error, null, "The profile could not be created. Please try again.");
        }

        if (result.Outcome == SaveProfileOutcome.Success)
        {
            var createdProfileId = result.NewProfileId ?? newProfileId;
            logger.ProfileCreated(createdProfileId, cloneId, isScenario);

            // The legacy page meant to carry on to the new profile's questions (its redirect never fired,
            // because it compared the button text with "Create" while the text was "Create profile").
            return RedirectToPage("/SurveillanceProfiles/EditProfileQuestions", new { profileId = createdProfileId });
        }

        logger.CreateRefused(result.Outcome);
        ModelState.AddModelError(string.Empty, $"Profile creation failed: {result.ErrorMessage}");

        return RenderPage();
    }

    private async Task<IActionResult> UpdateAsync(string title, AffectedSpeciesSelectionResult? selection, CancellationToken cancellationToken)
    {
        byte[] rowVersion;

        try
        {
            rowVersion = Convert.FromBase64String(LastUpdated ?? string.Empty);
        }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "The page has expired. Reload the profile and try again.");

            return RenderPage();
        }

        (IReadOnlyList<AffectedSpeciesInsertDto> Inserts, IReadOnlyList<AffectedSpeciesDeleteDto> Deletes) changes = selection is null
            ? ([], [])
            : AffectedSpeciesSelection.PlanUpdate(
                attributes!.CurrentDraftProfileVersionId, attributes.AffectedSpecies, selection.Profiled, selection.Other);

        var request = new UpdateProfileAttributesRequest
        {
            Id = ProfileId!.Value,
            Title = attributes!.IsWhatIfScenario ? string.Empty : title,
            ScenarioTitle = attributes.IsWhatIfScenario ? title : string.Empty,
            LastUpdated = rowVersion,
            AffectedSpeciesDeleteList = changes.Deletes,
            AffectedSpeciesInsertList = changes.Inserts
        };

        UpdateProfileAttributesResult result;

        try
        {
            result = await profilePropertiesApiService.UpdateProfileAttributesAsync(ProfileId.Value, request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.SaveFailed(exception, ProfileId);
            result = new UpdateProfileAttributesResult(SaveProfileOutcome.Error, "The profile could not be saved. Please try again.");
        }

        if (result.Outcome == SaveProfileOutcome.Success)
        {
            logger.ProfileUpdated(ProfileId.Value);
            StatusMessage = SavedMessage;

            // Redirecting reloads the profile, so the page shows what was saved and carries its new row version.
            return RedirectToPage(new { profileId = ProfileId });
        }

        logger.UpdateRefused(ProfileId.Value, result.Outcome);
        ModelState.AddModelError(string.Empty, $"Profile update failed: {result.ErrorMessage}");

        // As the legacy page did after a failed update, show what is now saved when another user changed it first.
        if (result.Outcome == SaveProfileOutcome.Conflict && await LoadAsync(cancellationToken) == LoadOutcome.Ok)
        {
            PopulateInputsFromSource();
        }

        return RenderPage();
    }

    private PageResult RenderPage()
    {
        if (ShowSpeciesSelector)
        {
            var nodes = SpeciesTreeBuilder.Build(species, includeInactive: true, expandAll: true);

            ProfiledSpeciesTree = ProfiledSpeciesTree with { Nodes = nodes, SelectedValues = ToValues(ProfiledSpeciesIds) };
            OtherSpeciesTree = OtherSpeciesTree with { Nodes = nodes, SelectedValues = ToValues(OtherSpeciesIds) };
        }

        return Page();
    }

    private static HashSet<string> ToValues(IEnumerable<Guid> ids) => [.. ids.Select(id => id.ToString())];

    private enum LoadOutcome
    {
        Ok,
        NotFound,
        Failed
    }
}
