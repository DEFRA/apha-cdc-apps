using CDC.Api.Application;
using CDC.Api.Domain.Entities;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.Application;

/// <summary>
/// Locks the role matrix: the profile editor (admin) and policy profile user are internal, global users; the technical,
/// policy and VA authors and reviewers hold roles on individual profiles, as external users or as internal users with no
/// global role.
/// </summary>
public class ProfileAccessTests
{
    private const string Editor = "Profile editor";
    private const string PolicyUser = "Policy profile user";
    private const string TechnicalAuthor = "Technical author";
    private const string TechnicalReviewer = "Technical reviewer";
    private const string PolicyAuthor = "Policy author";
    private const string PolicyReviewer = "Policy reviewer";
    private const string VaAuthor = "VA author";
    private const string VaReviewer = "VA reviewer";
    private const string InternalWithNoRole = "Internal user with no role";
    private const string InternalAuthor = "Internal user who is a technical author";
    private const string InternalReviewer = "Internal user who is a technical reviewer";
    private const string ExternalOnAnotherProfile = "External user on another profile";

    private static readonly Guid ProfileId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid OtherProfileId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid SectionId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid OtherSectionId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static readonly ProfileVersionFacts CurrentDraft = new(ProfileId, IsPublished: false, IsCurrent: true);
    private static readonly ProfileVersionFacts SupersededDraft = new(ProfileId, IsPublished: false, IsCurrent: false);
    private static readonly ProfileVersionFacts CurrentPublished = new(ProfileId, IsPublished: true, IsCurrent: true);
    private static readonly ProfileVersionFacts SupersededPublished = new(ProfileId, IsPublished: true, IsCurrent: false);

    public static TheoryData<string> EveryRole =>
    [
        Editor, PolicyUser, TechnicalAuthor, TechnicalReviewer, PolicyAuthor, PolicyReviewer, VaAuthor, VaReviewer,
        InternalAuthor, InternalReviewer, InternalWithNoRole, ExternalOnAnotherProfile
    ];

    [Theory]
    [InlineData(Editor, true)]
    [InlineData(PolicyUser, true)]
    [InlineData(TechnicalAuthor, true)]
    [InlineData(TechnicalReviewer, true)]
    [InlineData(PolicyAuthor, true)]
    [InlineData(PolicyReviewer, true)]
    [InlineData(VaAuthor, true)]
    [InlineData(VaReviewer, true)]
    [InlineData(InternalAuthor, true)]
    [InlineData(InternalReviewer, true)]
    [InlineData(InternalWithNoRole, false)]
    [InlineData(ExternalOnAnotherProfile, false)]
    public void CanViewDraft_IsGrantedToGlobalUsersAndToUsersWhoHoldARoleOnTheProfile(string role, bool expected)
    {
        ProfileAccess.CanViewDraft(User(role), ProfileId).Should().Be(expected);
        ProfileAccess.CanView(User(role), CurrentDraft).Should().Be(expected);
        ProfileAccess.CanView(User(role), SupersededDraft).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(EveryRole))]
    public void CanView_AllowsAPublishedVersionToEveryone(string role)
    {
        ProfileAccess.CanView(User(role), CurrentPublished).Should().BeTrue();
        ProfileAccess.CanView(User(role), SupersededPublished).Should().BeTrue();
    }

    [Theory]
    [InlineData(Editor, true)]
    [InlineData(PolicyUser, true)]
    [InlineData(TechnicalAuthor, false)]
    [InlineData(VaReviewer, false)]
    [InlineData(InternalAuthor, false)]
    [InlineData(InternalWithNoRole, false)]
    public void IsGlobalUser_IsTrueOnlyForAnInternalProfileEditorOrPolicyProfileUser(string role, bool expected) =>
        ProfileAccess.IsGlobalUser(User(role)).Should().Be(expected);

    [Theory]
    [InlineData(Editor, true)]
    [InlineData(PolicyUser, true)]
    [InlineData(TechnicalAuthor, true)]
    [InlineData(TechnicalReviewer, true)]
    [InlineData(PolicyAuthor, true)]
    [InlineData(PolicyReviewer, true)]
    [InlineData(VaAuthor, true)]
    [InlineData(VaReviewer, true)]
    [InlineData(InternalAuthor, true)]
    [InlineData(InternalReviewer, true)]
    [InlineData(InternalWithNoRole, false)]
    [InlineData(ExternalOnAnotherProfile, false)]
    public void PrintingAndRunningReportsOnADraft_FollowTheRightToViewTheDraft(string role, bool expected)
    {
        ProfileAccess.CanPrintDraft(User(role), CurrentDraft).Should().Be(expected);
        ProfileAccess.CanPrintDraft(User(role), SupersededDraft).Should().Be(expected);
        ProfileAccess.CanRunDraftReports(User(role), CurrentDraft).Should().Be(expected);
        ProfileAccess.CanViewDraftTabs(User(role), SupersededDraft).Should().Be(expected);
        ProfileAccess.CanPrintDraft(User(role), CurrentPublished).Should().BeFalse();
        ProfileAccess.CanRunDraftReports(User(role), SupersededDraft).Should().BeFalse();
        ProfileAccess.CanViewDraftTabs(User(role), CurrentPublished).Should().BeFalse();
    }

    [Theory]
    [InlineData(Editor, true, true)]
    [InlineData(Editor, false, true)]
    [InlineData(PolicyUser, true, false)]
    [InlineData(TechnicalAuthor, true, true)]
    [InlineData(TechnicalAuthor, false, false)]
    [InlineData(PolicyAuthor, true, true)]
    [InlineData(VaAuthor, true, true)]
    [InlineData(VaAuthor, false, false)]
    [InlineData(TechnicalReviewer, true, false)]
    [InlineData(PolicyReviewer, true, false)]
    [InlineData(VaReviewer, true, false)]
    [InlineData(InternalAuthor, true, true)]
    [InlineData(InternalAuthor, false, false)]
    [InlineData(InternalReviewer, true, false)]
    [InlineData(InternalWithNoRole, true, false)]
    [InlineData(ExternalOnAnotherProfile, true, false)]
    public void CanEditSection_IsGrantedToTheEditorAndToAnAuthorAssignedToTheSection(string role, bool isAssignedSection, bool expected)
    {
        var sectionId = isAssignedSection ? SectionId : OtherSectionId;

        ProfileAccess.CanEditSection(User(role), CurrentDraft, sectionId).Should().Be(expected);
        ProfileAccess.CanEditAnySection(User(role), CurrentDraft).Should().Be(expected || IsAuthorOnProfile(role) || role == Editor);
    }

    [Theory]
    [MemberData(nameof(EveryRole))]
    public void CanEditSection_IsRefusedForEveryoneOnAVersionThatIsNotTheCurrentDraft(string role)
    {
        foreach (var version in new[] { SupersededDraft, CurrentPublished, SupersededPublished })
        {
            ProfileAccess.CanEditSection(User(role), version, SectionId).Should().BeFalse();
            ProfileAccess.CanEditAnySection(User(role), version).Should().BeFalse();
            ProfileAccess.CanClearSection(User(role), version).Should().BeFalse();
            ProfileAccess.CanEditReviewDates(User(role), version).Should().BeFalse();
            ProfileAccess.CanChangeNotes(User(role), version).Should().BeFalse();
            ProfileAccess.CanWriteReviewComments(User(role), version).Should().BeFalse();
        }
    }

    [Theory]
    [MemberData(nameof(EveryRole))]
    public void CanEditReviewDates_IsForTheProfileEditorOnly(string role) =>
        ProfileAccess.CanEditReviewDates(User(role), CurrentDraft).Should().Be(role == Editor);

    [Theory]
    [InlineData(Editor, true)]
    [InlineData(PolicyUser, false)]
    [InlineData(TechnicalAuthor, true)]
    [InlineData(PolicyAuthor, true)]
    [InlineData(VaAuthor, true)]
    [InlineData(TechnicalReviewer, false)]
    [InlineData(PolicyReviewer, false)]
    [InlineData(VaReviewer, false)]
    [InlineData(InternalAuthor, true)]
    [InlineData(InternalReviewer, false)]
    [InlineData(InternalWithNoRole, false)]
    [InlineData(ExternalOnAnotherProfile, false)]
    public void CanChangeNotes_IsGrantedToTheEditorAndToAnAuthorOfTheProfile(string role, bool expected) =>
        ProfileAccess.CanChangeNotes(User(role), CurrentDraft).Should().Be(expected);

    [Theory]
    [MemberData(nameof(EveryRole))]
    public void ClearingASection_IsForTheProfileEditorOnly(string role) =>
        ProfileAccess.CanClearSection(User(role), CurrentDraft).Should().Be(role == Editor);

    [Theory]
    [InlineData(Editor, true)]
    [InlineData(PolicyUser, false)]
    [InlineData(TechnicalAuthor, true)]
    [InlineData(PolicyAuthor, true)]
    [InlineData(VaAuthor, true)]
    [InlineData(TechnicalReviewer, true)]
    [InlineData(PolicyReviewer, true)]
    [InlineData(VaReviewer, true)]
    [InlineData(InternalAuthor, true)]
    [InlineData(InternalReviewer, true)]
    [InlineData(InternalWithNoRole, false)]
    [InlineData(ExternalOnAnotherProfile, false)]
    public void WritingReviewComments_IsForTheEditorAndForAnyContributorOrReviewerOfTheProfile(string role, bool expected) =>
        ProfileAccess.CanWriteReviewComments(User(role), CurrentDraft).Should().Be(expected);

    [Theory]
    [InlineData(Editor, true, true, true)]
    [InlineData(Editor, false, true, true)]
    [InlineData(PolicyUser, true, true, false)]
    [InlineData(PolicyUser, false, true, true)]
    [InlineData(TechnicalAuthor, true, true, false)]
    [InlineData(TechnicalAuthor, false, true, true)]
    [InlineData(InternalAuthor, true, true, false)]
    [InlineData(InternalAuthor, false, true, true)]
    [InlineData(VaReviewer, true, true, false)]
    [InlineData(VaReviewer, false, true, true)]
    [InlineData(InternalWithNoRole, false, true, false)]
    [InlineData(ExternalOnAnotherProfile, false, true, false)]
    [InlineData(Editor, true, false, false)]
    [InlineData(Editor, false, false, false)]
    public void CanCreateReports_IsForTheEditorOnAPublishedVersionAndForAnyoneWhoCanViewADraft(
        string role,
        bool isPublished,
        bool isCurrent,
        bool expected) =>
        ProfileAccess.CanCreateReports(User(role), new ProfileVersionFacts(ProfileId, isPublished, isCurrent)).Should().Be(expected);

    [Theory]
    [InlineData(Editor, ReviewType.Author, true)]
    [InlineData(Editor, ReviewType.Va, true)]
    [InlineData(Editor, ReviewType.Policy, true)]
    [InlineData(PolicyUser, ReviewType.Policy, false)]
    [InlineData(TechnicalAuthor, ReviewType.Author, true)]
    [InlineData(TechnicalAuthor, ReviewType.Va, false)]
    [InlineData(InternalAuthor, ReviewType.Author, true)]
    [InlineData(InternalAuthor, ReviewType.Va, false)]
    [InlineData(TechnicalReviewer, ReviewType.Author, true)]
    [InlineData(TechnicalReviewer, ReviewType.Policy, false)]
    [InlineData(PolicyAuthor, ReviewType.Policy, true)]
    [InlineData(PolicyReviewer, ReviewType.Policy, true)]
    [InlineData(PolicyReviewer, ReviewType.Va, false)]
    [InlineData(VaAuthor, ReviewType.Va, true)]
    [InlineData(VaReviewer, ReviewType.Va, true)]
    [InlineData(VaReviewer, ReviewType.Author, false)]
    [InlineData(InternalWithNoRole, ReviewType.Author, false)]
    [InlineData(ExternalOnAnotherProfile, ReviewType.Author, false)]
    public void ReceivesReviewEmails_IsForTheEditorAndForUsersWithARoleOnTheProfileOwningThatReview(
        string role,
        ReviewType reviewType,
        bool expected) =>
        ProfileAccess.ReceivesReviewEmails(User(role), ProfileId, reviewType).Should().Be(expected);

    [Theory]
    [MemberData(nameof(EveryRole))]
    public void CanViewCustomWords_IsGrantedToEveryKnownUser(string role)
    {
        ProfileAccess.CanViewCustomWords(User(role)).Should().BeTrue();
        ProfileAccess.CanViewCustomWords(new RequestUserContext()).Should().BeFalse();
    }

    [Theory]
    [InlineData(Editor, true)]
    [InlineData(PolicyUser, false)]
    [InlineData(TechnicalAuthor, true)]
    [InlineData(TechnicalReviewer, true)]
    [InlineData(PolicyAuthor, true)]
    [InlineData(PolicyReviewer, true)]
    [InlineData(VaAuthor, true)]
    [InlineData(VaReviewer, true)]
    [InlineData(InternalAuthor, true)]
    [InlineData(InternalReviewer, true)]
    [InlineData(InternalWithNoRole, false)]
    [InlineData(ExternalOnAnotherProfile, true)]
    public void CanEditCustomWords_IsForTheEditorAndForAContributorOrReviewerOfAnyProfile(string role, bool expected)
    {
        ProfileAccess.CanEditCustomWords(User(role)).Should().Be(expected);
        ProfileAccess.CanEditCustomWords(new RequestUserContext()).Should().BeFalse();
    }

    [Fact]
    public void AUserManagementSystemAccount_IsRefusedEverything_EvenWithTheEditorFlag()
    {
        var user = new Mock<IUserContext>();
        user.SetupGet(context => context.UserId).Returns(Guid.NewGuid());
        user.SetupGet(context => context.IsProfileEditor).Returns(true);
        user.SetupGet(context => context.IsPolicyProfileUser).Returns(true);
        user.SetupGet(context => context.IsUserManagementSystem).Returns(true);

        ProfileAccess.CanViewCustomWords(user.Object).Should().BeFalse();
        ProfileAccess.CanEditCustomWords(user.Object).Should().BeFalse();
        ProfileAccess.IsGlobalUser(user.Object).Should().BeFalse();
        ProfileAccess.CanViewDraft(user.Object, ProfileId).Should().BeFalse();
        ProfileAccess.CanEditSection(user.Object, CurrentDraft, SectionId).Should().BeFalse();
        ProfileAccess.CanEditAnySection(user.Object, CurrentDraft).Should().BeFalse();
        ProfileAccess.CanEditReviewDates(user.Object, CurrentDraft).Should().BeFalse();
        ProfileAccess.CanWriteReviewComments(user.Object, CurrentDraft).Should().BeFalse();
        ProfileAccess.CanChangeNotes(user.Object, CurrentDraft).Should().BeFalse();
        ProfileAccess.CanCreateReports(user.Object, CurrentDraft).Should().BeFalse();
        ProfileAccess.ReceivesReviewEmails(user.Object, ProfileId, ReviewType.Author).Should().BeFalse();
    }

    [Fact]
    public void ARoleTheCatalogueDoesNotKnow_OwnsNoReview()
    {
        var user = External(new ProfileRoleAssignment(ProfileId, Guid.NewGuid(), "Some other role", true));

        ProfileAccess.ReceivesReviewEmails(user, ProfileId, ReviewType.Author).Should().BeFalse();
    }

    [Fact]
    public void TheRules_RejectANullUser()
    {
        Action[] calls =
        [
            () => ProfileAccess.IsEditor(null!),
            () => ProfileAccess.IsPolicyUser(null!),
            () => ProfileAccess.IsAssigned(null!, ProfileId),
            () => ProfileAccess.CanViewCustomWords(null!),
            () => ProfileAccess.CanEditCustomWords(null!),
            () => ProfileAccess.CanEditSection(null!, CurrentDraft, SectionId),
            () => ProfileAccess.CanEditAnySection(null!, CurrentDraft),
            () => ProfileAccess.CanEditReviewDates(null!, CurrentDraft),
            () => ProfileAccess.CanWriteReviewComments(null!, CurrentDraft),
            () => ProfileAccess.CanChangeNotes(null!, CurrentDraft),
            () => ProfileAccess.CanCreateReports(null!, CurrentDraft),
            () => ProfileAccess.ReceivesReviewEmails(null!, ProfileId, ReviewType.Author)
        ];

        foreach (var call in calls)
        {
            call.Should().Throw<ArgumentNullException>();
        }
    }

    private static bool IsAuthorOnProfile(string role) => role is TechnicalAuthor or PolicyAuthor or VaAuthor or InternalAuthor;

    private static RequestUserContext User(string role) => role switch
    {
        Editor => Context(Internal(isProfileEditor: true, isPolicyProfileUser: false)),
        PolicyUser => Context(Internal(isProfileEditor: false, isPolicyProfileUser: true)),
        InternalWithNoRole => Context(Internal(isProfileEditor: false, isPolicyProfileUser: false)),
        InternalAuthor => Context(InternalWithProfileRole(new ProfileRoleAssignment(ProfileId, Guid.NewGuid(), TechnicalAuthor, true))),
        InternalReviewer => Context(InternalWithProfileRole(new ProfileRoleAssignment(ProfileId, Guid.NewGuid(), TechnicalReviewer, false))),
        ExternalOnAnotherProfile => Context(ExternalUser(new ProfileRoleAssignment(OtherProfileId, Guid.NewGuid(), TechnicalAuthor, true))),
        TechnicalAuthor or PolicyAuthor or VaAuthor => Context(ExternalUser(new ProfileRoleAssignment(ProfileId, Guid.NewGuid(), role, true))),
        TechnicalReviewer or PolicyReviewer or VaReviewer => Context(ExternalUser(new ProfileRoleAssignment(ProfileId, Guid.NewGuid(), role, false))),
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown test role.")
    };

    private static RequestUserContext External(ProfileRoleAssignment role) => Context(ExternalUser(role));

    private static RequestUserContext Context(ActingUser user)
    {
        var context = new RequestUserContext();
        context.SetActingUser(user);

        return context;
    }

    private static ActingUser Internal(bool isProfileEditor, bool isPolicyProfileUser) => new()
    {
        Id = Guid.NewGuid(),
        IsProfileEditor = isProfileEditor,
        IsPolicyProfileUser = isPolicyProfileUser,
        SsoUserIdInt = Guid.NewGuid()
    };

    /// <summary>An internal user with no global role who holds the given role on the profile; an author is also assigned to <see cref="SectionId"/>.</summary>
    private static ActingUser InternalWithProfileRole(ProfileRoleAssignment role) => Internal(isProfileEditor: false, isPolicyProfileUser: false) with
    {
        ProfileRoles = [role],
        SectionAssignments = role.IsContributor ? [new ProfileSectionAssignment(role.ProfileId, SectionId)] : []
    };

    /// <summary>An external user holding the given role; an author is also assigned to <see cref="SectionId"/> on that profile.</summary>
    private static ActingUser ExternalUser(ProfileRoleAssignment role) => new()
    {
        Id = Guid.NewGuid(),
        IsProfileEditor = false,
        IsPolicyProfileUser = false,
        SsoUserIdExt = Guid.NewGuid(),
        ProfileRoles = [role],
        SectionAssignments = role.IsContributor ? [new ProfileSectionAssignment(role.ProfileId, SectionId)] : []
    };
}
