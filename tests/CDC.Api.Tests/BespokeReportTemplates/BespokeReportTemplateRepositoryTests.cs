using System.Data;
using CDC.Api.Features.BespokeReportTemplates;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Api.Tests.BespokeReportTemplates;

public class BespokeReportTemplateRepositoryTests : IDisposable
{
    private static readonly Guid TemplateId = Guid.NewGuid();
    private static readonly string[] TemplateColumns = ["Id", "Title"];
    private static readonly Type[] TemplateColumnTypes = [typeof(Guid), typeof(string)];

    private readonly FakeDbConnection connection = new();

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private BespokeReportTemplateRepository CreateRepository() =>
        new(new StubConnectionFactory(connection), NullLogger<BespokeReportTemplateRepository>.Instance);

    [Fact]
    public async Task GetAllAsync_ReturnsTheTemplatesInTheOrderTheDatabaseGives()
    {
        connection.Script(BespokeReportTemplateSql.GetAll, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(TemplateColumns, [[TemplateId, "Alpha"], [Guid.NewGuid(), "Beta"]])]
        });

        var templates = await CreateRepository().GetAllAsync(CancellationToken.None);

        templates.Select(template => template.Title).Should().Equal("Alpha", "Beta");
        templates[0].Id.Should().Be(TemplateId);
        templates[0].CheckedItems.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_ReadsTheTemplateAndItsTickedItems()
    {
        connection.Script(BespokeReportTemplateSql.GetById, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(TemplateColumns, [[TemplateId, "Alpha"]])]
        });
        connection.Script(BespokeReportTemplateSql.GetCheckedItems, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["CheckedId"], [["aSection"], ["bQuestion"]])]
        });

        var template = await CreateRepository().GetAsync(TemplateId, CancellationToken.None);

        template.Should().NotBeNull();
        template!.Title.Should().Be("Alpha");
        template.CheckedItems.Should().Equal("aSection", "bQuestion");
        connection.Executed.Should().OnlyContain(command => (Guid)command.Parameters["Id"]! == TemplateId);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenTheTemplateDoesNotExist()
    {
        connection.Script(BespokeReportTemplateSql.GetById, new FakeCommandScript { ResultSets = [FakeResultSet.Empty(TemplateColumns, TemplateColumnTypes)] });

        var template = await CreateRepository().GetAsync(TemplateId, CancellationToken.None);

        template.Should().BeNull();
        connection.Executed.Should().ContainSingle();
    }

    [Fact]
    public async Task GetByTitleAsync_FindsATemplateByTitle()
    {
        connection.Script(BespokeReportTemplateSql.GetByTitle, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(TemplateColumns, [[TemplateId, "Alpha"]])]
        });

        var template = await CreateRepository().GetByTitleAsync("Alpha", CancellationToken.None);

        template!.Id.Should().Be(TemplateId);
        connection.Executed.Single().Parameters["Title"].Should().Be("Alpha");
    }

    [Fact]
    public async Task GetByTitleAsync_ReturnsNull_WhenNoTemplateHasTheTitle()
    {
        connection.Script(BespokeReportTemplateSql.GetByTitle, new FakeCommandScript { ResultSets = [FakeResultSet.Empty(TemplateColumns, TemplateColumnTypes)] });

        (await CreateRepository().GetByTitleAsync("Missing", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task InsertAsync_InsertsTheTemplateAndEachTickedItem_InOneTransaction()
    {
        var id = await CreateRepository().InsertAsync("Alpha", ["aSection", "bQuestion"], CancellationToken.None);

        id.Should().NotBeEmpty();
        var transaction = connection.Transactions.Should().ContainSingle().Subject;
        transaction.Committed.Should().BeTrue();
        transaction.RolledBack.Should().BeFalse();
        connection.Executed.Select(command => command.CommandText).Should().Equal(
            BespokeReportTemplateSql.InsertTemplate,
            BespokeReportTemplateSql.InsertCheckedItem,
            BespokeReportTemplateSql.InsertCheckedItem);
        connection.Executed.Should().OnlyContain(command => command.HadTransaction && command.CommandType == CommandType.Text);
        connection.Executed[0].Parameters["Id"].Should().Be(id);
        connection.Executed[0].Parameters["Title"].Should().Be("Alpha");
        connection.Executed.Skip(1).Select(command => command.Parameters["CheckedId"]).Should().Equal("aSection", "bQuestion");
        connection.Executed.Skip(1).Should().OnlyContain(command => (Guid)command.Parameters["TemplateId"]! == id);
    }

    [Fact]
    public async Task InsertAsync_InsertsNoItemRows_WhenNothingIsTicked()
    {
        await CreateRepository().InsertAsync("Alpha", [], CancellationToken.None);

        connection.Executed.Select(command => command.CommandText).Should().Equal(BespokeReportTemplateSql.InsertTemplate);
    }

    [Fact]
    public async Task InsertAsync_RollsBackAndRethrows_WhenAnItemCannotBeInserted()
    {
        connection.Script(BespokeReportTemplateSql.InsertCheckedItem, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().InsertAsync("Alpha", ["a"], CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
        var transaction = connection.Transactions.Should().ContainSingle().Subject;
        transaction.RolledBack.Should().BeTrue();
        transaction.Committed.Should().BeFalse();
    }

    [Fact]
    public async Task ReplaceCheckedItemsAsync_DeletesTheOldItemsAndInsertsTheNew_InOneTransaction()
    {
        await CreateRepository().ReplaceCheckedItemsAsync(TemplateId, ["x", "y"], CancellationToken.None);

        connection.Executed.Select(command => command.CommandText).Should().Equal(
            BespokeReportTemplateSql.DeleteCheckedItems,
            BespokeReportTemplateSql.InsertCheckedItem,
            BespokeReportTemplateSql.InsertCheckedItem);
        connection.Executed[0].Parameters["Id"].Should().Be(TemplateId);
        connection.Transactions.Should().ContainSingle().Which.Committed.Should().BeTrue();
    }

    [Fact]
    public async Task ReplaceCheckedItemsAsync_RollsBackAndRethrows_OnFailure()
    {
        connection.Script(BespokeReportTemplateSql.DeleteCheckedItems, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().ReplaceCheckedItemsAsync(TemplateId, ["x"], CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
        connection.Transactions.Should().ContainSingle().Which.RolledBack.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTitleAsync_UpdatesTheTitle()
    {
        await CreateRepository().UpdateTitleAsync(TemplateId, "Gamma", CancellationToken.None);

        var command = connection.Executed.Should().ContainSingle().Subject;
        command.CommandText.Should().Be(BespokeReportTemplateSql.UpdateTitle);
        command.Parameters["Id"].Should().Be(TemplateId);
        command.Parameters["Title"].Should().Be("Gamma");
    }

    [Fact]
    public async Task UpdateTitleAsync_Rethrows_OnFailure()
    {
        connection.Script(BespokeReportTemplateSql.UpdateTitle, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().UpdateTitleAsync(TemplateId, "Gamma", CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task DeleteAsync_DeletesTheItemsThenTheTemplate_InOneTransaction()
    {
        await CreateRepository().DeleteAsync(TemplateId, CancellationToken.None);

        connection.Executed.Select(command => command.CommandText).Should().Equal(
            BespokeReportTemplateSql.DeleteCheckedItems,
            BespokeReportTemplateSql.DeleteTemplate);
        connection.Executed.Should().OnlyContain(command => command.HadTransaction);
        connection.Transactions.Should().ContainSingle().Which.Committed.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_RollsBackAndRethrows_OnFailure()
    {
        connection.Script(BespokeReportTemplateSql.DeleteTemplate, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().DeleteAsync(TemplateId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
        connection.Transactions.Should().ContainSingle().Which.RolledBack.Should().BeTrue();
    }

    [Theory]
    [InlineData(nameof(BespokeReportTemplateRepository.GetAllAsync))]
    [InlineData(nameof(BespokeReportTemplateRepository.GetAsync))]
    [InlineData(nameof(BespokeReportTemplateRepository.GetByTitleAsync))]
    public async Task Reads_Rethrow_WhenTheDatabaseFails(string operation)
    {
        connection.Script(BespokeReportTemplateSql.GetAll, new FakeCommandScript { Throws = new FakeDbException("boom") });
        connection.Script(BespokeReportTemplateSql.GetById, new FakeCommandScript { Throws = new FakeDbException("boom") });
        connection.Script(BespokeReportTemplateSql.GetByTitle, new FakeCommandScript { Throws = new FakeDbException("boom") });
        var repository = CreateRepository();

        Func<Task> act = operation switch
        {
            nameof(BespokeReportTemplateRepository.GetAllAsync) => () => repository.GetAllAsync(CancellationToken.None),
            nameof(BespokeReportTemplateRepository.GetAsync) => () => repository.GetAsync(TemplateId, CancellationToken.None),
            _ => () => repository.GetByTitleAsync("Alpha", CancellationToken.None)
        };

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }
}
