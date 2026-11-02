using CDC.Api.Application;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.SpellCheck;
using CDC.Api.Features.SpellCheck.Dtos;
using CDC.Api.Features.SpellCheck.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.SpellCheck;

public sealed class SpellCheckServiceTests
{
    private readonly Mock<ICustomWordRepository> repository = new(MockBehavior.Strict);

    private SpellCheckService CreateService(
        bool isKnownUser = true,
        bool isProfileEditor = false,
        bool holdsAnyProfileRole = false,
        bool isUserManagementSystem = false) =>
        new(
            repository.Object,
            new FakeUserContext(isKnownUser, isProfileEditor, holdsAnyProfileRole, isUserManagementSystem),
            NullLogger<SpellCheckService>.Instance);

    private sealed class FakeUserContext(bool isKnownUser, bool isProfileEditor, bool holdsAnyProfileRole, bool isUserManagementSystem) : IUserContext
    {
        public Guid? UserId => isKnownUser ? Guid.Parse("88d1e0c6-7d10-427d-a82f-33fc2c1cef28") : null;

        public bool IsProfileEditor => isProfileEditor;

        public bool IsUserManagementSystem => isUserManagementSystem;

        public bool IsPolicyProfileUser => false;

        public UserKind Kind => isKnownUser ? UserKind.Internal : UserKind.Unclassified;

        public bool HoldsAnyProfileRole => holdsAnyProfileRole;

        public ProfileRoleAssignment? GetProfileRole(Guid profileId) => null;

        public bool IsAssignedToSection(Guid profileId, Guid profileSectionId) => false;

        public IEnumerable<Guid> GetAssignedSectionIds(Guid profileId) => [];
    }

    [Fact]
    public async Task GetCustomWordsAsync_ReturnsTheWordsInTheDictionary_ToAnyKnownUser()
    {
        IReadOnlyList<string> words = ["Defra", "APHA"];
        repository.Setup(repo => repo.GetCustomWordsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(words);

        var result = await CreateService().GetCustomWordsAsync(CancellationToken.None);

        result.Should().BeSameAs(words);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task GetCustomWordsAsync_IsRefused_WhenTheRequestNamesNoKnownUserOrAUserManagementSystem(bool isKnownUser, bool isUserManagementSystem)
    {
        var act = () => CreateService(isKnownUser, isUserManagementSystem: isUserManagementSystem).GetCustomWordsAsync(CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task AddCustomWordAsync_AddsTheTrimmedWordUnderANewId_ForAnEditorOrAContributorOrReviewer(bool isProfileEditor, bool holdsAnyProfileRole)
    {
        IReadOnlyList<string> existing = ["APHA"];
        repository.Setup(repo => repo.GetCustomWordsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        repository.Setup(repo => repo.AddCustomWordAsync(It.Is<Guid>(id => id != Guid.Empty), "Defra", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService(isProfileEditor: isProfileEditor, holdsAnyProfileRole: holdsAnyProfileRole)
            .AddCustomWordAsync("  Defra ", CancellationToken.None);

        result.Should().Be(new AddCustomWordResult(AddCustomWordOutcome.Success, null));
        repository.Verify(repo => repo.AddCustomWordAsync(It.IsAny<Guid>(), "Defra", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddCustomWordAsync_IsForbidden_ToAUserWhoIsNeitherAnEditorNorAContributorOrReviewer()
    {
        var result = await CreateService().AddCustomWordAsync("Defra", CancellationToken.None);

        result.Outcome.Should().Be(AddCustomWordOutcome.Forbidden);
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        repository.Verify(repo => repo.AddCustomWordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddCustomWordAsync_IsForbidden_ToAUserManagementSystem_EvenWithTheEditorFlag()
    {
        var result = await CreateService(isProfileEditor: true, holdsAnyProfileRole: true, isUserManagementSystem: true)
            .AddCustomWordAsync("Defra", CancellationToken.None);

        result.Outcome.Should().Be(AddCustomWordOutcome.Forbidden);
        repository.Verify(repo => repo.AddCustomWordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two words")]
    [InlineData("tab\tseparated")]
    [InlineData("line\nbreak")]
    public async Task AddCustomWordAsync_RefusesAWordThatIsEmptyOrNotASingleWord(string? word)
    {
        var result = await CreateService(isProfileEditor: true).AddCustomWordAsync(word, CancellationToken.None);

        result.Outcome.Should().Be(AddCustomWordOutcome.ValidationFailed);
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        repository.Verify(repo => repo.AddCustomWordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddCustomWordAsync_RefusesAWordLongerThanTheTableCanHold_ButAcceptsOneThatJustFits()
    {
        var longest = new string('a', SpellCheckService.MaxWordLength);
        repository.Setup(repo => repo.GetCustomWordsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repository.Setup(repo => repo.AddCustomWordAsync(It.IsAny<Guid>(), longest, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = CreateService(isProfileEditor: true);

        var tooLong = await service.AddCustomWordAsync(longest + "a", CancellationToken.None);
        var fits = await service.AddCustomWordAsync(longest, CancellationToken.None);

        tooLong.Outcome.Should().Be(AddCustomWordOutcome.ValidationFailed);
        fits.Outcome.Should().Be(AddCustomWordOutcome.Success);
    }

    [Fact]
    public async Task AddCustomWordAsync_DoesNotAddAWordThatIsThereAlready_WhateverItsCase()
    {
        IReadOnlyList<string> existing = ["Defra"];
        repository.Setup(repo => repo.GetCustomWordsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await CreateService(isProfileEditor: true).AddCustomWordAsync("DEFRA", CancellationToken.None);

        result.Outcome.Should().Be(AddCustomWordOutcome.Success);
        repository.Verify(repo => repo.AddCustomWordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
