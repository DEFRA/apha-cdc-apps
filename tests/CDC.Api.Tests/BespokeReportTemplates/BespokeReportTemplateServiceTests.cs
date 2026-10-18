using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.BespokeReportTemplates;
using CDC.Api.Features.BespokeReportTemplates.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.BespokeReportTemplates;

public class BespokeReportTemplateServiceTests
{
    private static readonly Guid TemplateId = Guid.NewGuid();
    private static readonly string[] AB = ["a", "b"];
    private static readonly string[] XY = ["x", "y"];
    private static readonly string[] JustA = ["a"];

    private readonly Mock<IBespokeReportTemplateRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IUserContext> userContext = new();

    private BespokeReportTemplateService CreateService(bool isEditor = true, bool isPolicyUser = false, bool isUserManagement = false)
    {
        userContext.SetupGet(context => context.IsProfileEditor).Returns(isEditor);
        userContext.SetupGet(context => context.IsPolicyProfileUser).Returns(isPolicyUser);
        userContext.SetupGet(context => context.IsUserManagementSystem).Returns(isUserManagement);

        return new BespokeReportTemplateService(repository.Object, userContext.Object, NullLogger<BespokeReportTemplateService>.Instance);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GetTemplatesAsync_ListsTemplates_ForAnEditorOrPolicyUser(bool isEditor, bool isPolicyUser)
    {
        repository
            .Setup(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new BespokeReportTemplate { Id = TemplateId, Title = "Alpha" }, new BespokeReportTemplate { Id = Guid.NewGuid(), Title = "Beta" }]);

        var result = await CreateService(isEditor, isPolicyUser).GetTemplatesAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(template => template.Title).Should().Equal("Alpha", "Beta");
        result.Value[0].Id.Should().Be(TemplateId);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public async Task GetTemplatesAsync_IsForbidden_WhenTheUserMayNotViewTemplates(bool isEditor, bool isPolicyUser, bool isUserManagement)
    {
        var result = await CreateService(isEditor, isPolicyUser, isUserManagement).GetTemplatesAsync(CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetTemplateAsync_ReturnsTheTemplateWithItsTickedItems()
    {
        repository
            .Setup(repo => repo.GetAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BespokeReportTemplate { Id = TemplateId, Title = "Alpha", CheckedItems = ["a", "b"] });

        var result = await CreateService(isEditor: false, isPolicyUser: true).GetTemplateAsync(TemplateId, CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new { Id = TemplateId, Title = "Alpha", CheckedList = AB });
    }

    [Fact]
    public async Task GetTemplateAsync_IsNotFound_WhenTheTemplateDoesNotExist()
    {
        repository.Setup(repo => repo.GetAsync(TemplateId, It.IsAny<CancellationToken>())).ReturnsAsync((BespokeReportTemplate?)null);

        var result = await CreateService().GetTemplateAsync(TemplateId, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetTemplateAsync_IsForbidden_WhenTheUserMayNotViewTemplates()
    {
        var result = await CreateService(isEditor: false).GetTemplateAsync(TemplateId, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task SaveAsNewAsync_TrimsTheTitle_DropsDuplicateItems_AndReturnsTheNewTemplate()
    {
        repository.Setup(repo => repo.GetByTitleAsync("My template", It.IsAny<CancellationToken>())).ReturnsAsync((BespokeReportTemplate?)null);
        repository
            .Setup(repo => repo.InsertAsync("My template", It.Is<IReadOnlyList<string>>(items => items.SequenceEqual(AB)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TemplateId);

        var result = await CreateService().SaveAsNewAsync("  My template ", ["a", "b", "a"], CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new { Id = TemplateId, Title = "My template", CheckedList = AB });
        repository.VerifyAll();
    }

    [Fact]
    public async Task SaveAsNewAsync_IsAConflict_WhenAnotherTemplateHasTheTitle()
    {
        repository
            .Setup(repo => repo.GetByTitleAsync("Taken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BespokeReportTemplate { Id = Guid.NewGuid(), Title = "Taken" });

        var result = await CreateService().SaveAsNewAsync("Taken", ["a"], CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.Error.Should().Be(BespokeReportTemplateService.TitleInUseMessage);
        repository.Verify(repo => repo.InsertAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public async Task SaveAsNewAsync_IsForbidden_UnlessAnEditor(bool isEditor, bool isPolicyUser, bool isUserManagement)
    {
        var result = await CreateService(isEditor, isPolicyUser, isUserManagement).SaveAsNewAsync("Title", ["a"], CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateCheckedListAsync_ReplacesTheItems_AndKeepsTheTitle()
    {
        repository
            .Setup(repo => repo.GetAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BespokeReportTemplate { Id = TemplateId, Title = "Alpha", CheckedItems = ["old"] });
        repository
            .Setup(repo => repo.ReplaceCheckedItemsAsync(TemplateId, It.Is<IReadOnlyList<string>>(items => items.SequenceEqual(XY)), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().UpdateCheckedListAsync(TemplateId, ["x", "y", "x"], CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new { Id = TemplateId, Title = "Alpha", CheckedList = XY });
        repository.VerifyAll();
    }

    [Fact]
    public async Task UpdateCheckedListAsync_IsNotFound_WhenTheTemplateDoesNotExist()
    {
        repository.Setup(repo => repo.GetAsync(TemplateId, It.IsAny<CancellationToken>())).ReturnsAsync((BespokeReportTemplate?)null);

        var result = await CreateService().UpdateCheckedListAsync(TemplateId, ["x"], CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task UpdateCheckedListAsync_IsForbidden_ForAPolicyUser()
    {
        var result = await CreateService(isEditor: false, isPolicyUser: true).UpdateCheckedListAsync(TemplateId, ["x"], CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task RenameAsync_SavesTheTrimmedTitle()
    {
        repository
            .Setup(repo => repo.GetAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BespokeReportTemplate { Id = TemplateId, Title = "Alpha", CheckedItems = ["a"] });
        repository.Setup(repo => repo.GetByTitleAsync("Gamma", It.IsAny<CancellationToken>())).ReturnsAsync((BespokeReportTemplate?)null);
        repository.Setup(repo => repo.UpdateTitleAsync(TemplateId, "Gamma", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService().RenameAsync(TemplateId, " Gamma ", CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new { Id = TemplateId, Title = "Gamma", CheckedList = JustA });
        repository.VerifyAll();
    }

    [Fact]
    public async Task RenameAsync_AllowsTheTemplateToKeepItsOwnTitle()
    {
        repository
            .Setup(repo => repo.GetAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BespokeReportTemplate { Id = TemplateId, Title = "Alpha" });
        repository
            .Setup(repo => repo.GetByTitleAsync("Alpha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BespokeReportTemplate { Id = TemplateId, Title = "Alpha" });
        repository.Setup(repo => repo.UpdateTitleAsync(TemplateId, "Alpha", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService().RenameAsync(TemplateId, "Alpha", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RenameAsync_IsAConflict_WhenAnotherTemplateHasTheTitle()
    {
        repository
            .Setup(repo => repo.GetAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BespokeReportTemplate { Id = TemplateId, Title = "Alpha" });
        repository
            .Setup(repo => repo.GetByTitleAsync("Beta", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BespokeReportTemplate { Id = Guid.NewGuid(), Title = "Beta" });

        var result = await CreateService().RenameAsync(TemplateId, "Beta", CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        repository.Verify(repo => repo.UpdateTitleAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RenameAsync_IsNotFound_WhenTheTemplateDoesNotExist()
    {
        repository.Setup(repo => repo.GetAsync(TemplateId, It.IsAny<CancellationToken>())).ReturnsAsync((BespokeReportTemplate?)null);

        var result = await CreateService().RenameAsync(TemplateId, "Beta", CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task RenameAsync_IsForbidden_ForAPolicyUser()
    {
        var result = await CreateService(isEditor: false, isPolicyUser: true).RenameAsync(TemplateId, "Beta", CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task DeleteAsync_DeletesAnExistingTemplate()
    {
        repository
            .Setup(repo => repo.GetAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BespokeReportTemplate { Id = TemplateId, Title = "Alpha" });
        repository.Setup(repo => repo.DeleteAsync(TemplateId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService().DeleteAsync(TemplateId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.VerifyAll();
    }

    [Fact]
    public async Task DeleteAsync_IsNotFound_WhenTheTemplateDoesNotExist()
    {
        repository.Setup(repo => repo.GetAsync(TemplateId, It.IsAny<CancellationToken>())).ReturnsAsync((BespokeReportTemplate?)null);

        var result = await CreateService().DeleteAsync(TemplateId, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_IsForbidden_ForAPolicyUser()
    {
        var result = await CreateService(isEditor: false, isPolicyUser: true).DeleteAsync(TemplateId, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
    }
}
