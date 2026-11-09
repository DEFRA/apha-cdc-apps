using System.Data;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Api.Tests.ReferenceData;

public sealed class ReferenceDataMaintenanceRepositoryTests : IDisposable
{
    private static readonly Guid TableId = Guid.Parse("D9889426-96A2-4C69-A3DB-C26F256C5FC1");
    private static readonly Guid ValueId = Guid.Parse("533262C1-454C-45E3-876B-2B47841AB5C2");
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly byte[] RowVersion = [0, 0, 0, 0, 0, 0, 7, 209];

    private readonly FakeDbConnection connection = new();

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ReferenceDataMaintenanceRepository CreateRepository() =>
        new(new StubConnectionFactory(connection), NullLogger<ReferenceDataMaintenanceRepository>.Instance);

    [Fact]
    public async Task GetMaintainableTablesAsync_MapsRows()
    {
        connection.Script(ReferenceDataStoredProcedures.GetMaintainableTables, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Id", "Name"], [[TableId, "Control mechanism"]])]
        });

        var tables = await CreateRepository().GetMaintainableTablesAsync(CancellationToken.None);

        tables.Should().ContainSingle().Which.Name.Should().Be("Control mechanism");
        connection.Executed.Should().ContainSingle().Which.CommandType.Should().Be(CommandType.StoredProcedure);
    }

    [Fact]
    public async Task GetMaintainableValuesAsync_MapsDatesAndFlags()
    {
        var from = new DateTime(2007, 1, 1);
        connection.Script(ReferenceDataStoredProcedures.GetMaintainableValuesByTable, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "LookupValue", "EffectiveDateFrom", "EffectiveDateTo", "IsInUse", "IsActive"],
                    [[ValueId, "Import controls", from, null, true, true]])
                {
                    ColumnTypes = [typeof(Guid), typeof(string), typeof(DateTime), typeof(DateTime), typeof(bool), typeof(bool)]
                }
            ]
        });

        var values = await CreateRepository().GetMaintainableValuesAsync(TableId, CancellationToken.None);

        var value = values.Should().ContainSingle().Subject;
        value.LookupValue.Should().Be("Import controls");
        value.EffectiveDateFrom.Should().Be(from);
        value.EffectiveDateTo.Should().BeNull();
        value.IsInUse.Should().BeTrue();
        value.IsActive.Should().BeTrue();
        connection.Executed.Single().Parameters["ReferenceTableId"].Should().Be(TableId);
    }

    [Fact]
    public async Task GetMaintainableValuesAsync_ReturnsNull_WhenTheTableIsNotMaintainable()
    {
        connection.Script(ReferenceDataStoredProcedures.GetMaintainableValuesByTable, new FakeCommandScript
        {
            Throws = new FakeDbException("The specified reference table does not exist or is not maintainable")
        });

        var values = await CreateRepository().GetMaintainableValuesAsync(TableId, CancellationToken.None);

        values.Should().BeNull();
    }

    [Fact]
    public async Task GetMaintainableValuesAsync_Rethrows_AnyOtherFailure()
    {
        connection.Script(ReferenceDataStoredProcedures.GetMaintainableValuesByTable, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetMaintainableValuesAsync(TableId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetValueRowVersionAsync_ReturnsTheRowVersion_OrNull()
    {
        connection.Script(ReferenceDataStoredProcedures.GetMaintainableValue, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(["LookupValue", "IsInUse", "EffectiveDateFrom", "EffectiveDateTo", "LastUpdated"], [["Import controls", true, null, null, RowVersion]])
                {
                    ColumnTypes = [typeof(string), typeof(bool), typeof(DateTime), typeof(DateTime), typeof(byte[])]
                }
            ]
        });

        (await CreateRepository().GetValueRowVersionAsync(ValueId, CancellationToken.None)).Should().Equal(RowVersion);

        connection.Script(ReferenceDataStoredProcedures.GetMaintainableValue, new FakeCommandScript
        {
            Throws = new FakeDbException("The specified reference value does not belong to a maintainable reference table")
        });

        (await CreateRepository().GetValueRowVersionAsync(ValueId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task GetValueRowVersionAsync_Rethrows_AnyOtherFailure()
    {
        connection.Script(ReferenceDataStoredProcedures.GetMaintainableValue, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetValueRowVersionAsync(ValueId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetAuditTrailAsync_MapsRows()
    {
        var auditId = Guid.NewGuid();
        var logDate = new DateTime(2026, 11, 9, 10, 30, 0);
        connection.Script(ReferenceDataStoredProcedures.GetAuditTrail, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "OldLookupValue", "NewLookupValue", "FullName", "EffectiveDate", "LogDate", "Reason"],
                    [[auditId, "- new entry -", "Zoning", "Profile Editor", logDate, logDate, "New"]])
            ]
        });

        var entries = await CreateRepository().GetAuditTrailAsync(TableId, CancellationToken.None);

        var entry = entries.Should().ContainSingle().Subject;
        entry.Id.Should().Be(auditId);
        entry.ReferenceTableId.Should().Be(TableId);
        entry.UserFullName.Should().Be("Profile Editor");
        entry.LogDate.Should().Be(logDate);
    }

    [Fact]
    public async Task GetAuditTrailAsync_Rethrows_OnFailure()
    {
        connection.Script(ReferenceDataStoredProcedures.GetAuditTrail, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetAuditTrailAsync(TableId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task ChangeMethods_CallTheirProceduresWithTheLegacyParameters()
    {
        var repository = CreateRepository();

        await repository.UpdateValueAsync(ValueId, "Imports", UserId, "Reason", RowVersion, CancellationToken.None);
        await repository.InsertValueAsync(TableId, ValueId, "Zoning", UserId, "Reason", CancellationToken.None);
        await repository.InactivateValueAsync(ValueId, UserId, "Reason", RowVersion, CancellationToken.None);
        await repository.DeleteValueAsync(ValueId, "Zoning", UserId, "Reason", RowVersion, CancellationToken.None);

        connection.Executed.Select(command => command.CommandText).Should().Equal(
            ReferenceDataStoredProcedures.UpdateValue,
            ReferenceDataStoredProcedures.InsertValue,
            ReferenceDataStoredProcedures.InactivateValue,
            ReferenceDataStoredProcedures.DeleteValue);

        ParameterNames(0).Should().BeEquivalentTo(["ValueId", "NewLookupValue", "UserId", "Reason", "LastUpdated"]);
        ParameterNames(1).Should().BeEquivalentTo(["ReferenceTableId", "ValueId", "NewLookupValue", "UserId", "Reason"]);
        ParameterNames(2).Should().BeEquivalentTo(["ValueId", "UserId", "Reason", "LastUpdated"]);
        ParameterNames(3).Should().BeEquivalentTo(["ValueId", "OldLookupValue", "UserId", "Reason", "LastUpdated"]);

        IEnumerable<string> ParameterNames(int index) => connection.Executed[index].Parameters.Keys.Select(name => name.TrimStart('@'));
    }

    [Fact]
    public async Task ChangeMethods_TranslateTheProceduresErrors()
    {
        var repository = CreateRepository();

        connection.Script(ReferenceDataStoredProcedures.UpdateValue, new FakeCommandScript
        {
            Throws = new FakeDbException("The reference value cannot be updated because it has been edited by another user")
        });
        await repository.Invoking(r => r.UpdateValueAsync(ValueId, "Imports", UserId, "Reason", RowVersion, CancellationToken.None))
            .Should().ThrowAsync<ConcurrencyException>();

        connection.Script(ReferenceDataStoredProcedures.InsertValue, new FakeCommandScript
        {
            Throws = new FakeDbException("There is already a reference value with this name")
        });
        await repository.Invoking(r => r.InsertValueAsync(TableId, ValueId, "Zoning", UserId, "Reason", CancellationToken.None))
            .Should().ThrowAsync<DuplicateReferenceValueException>();

        connection.Script(ReferenceDataStoredProcedures.DeleteValue, new FakeCommandScript { Throws = new FakeDbException("boom") });
        await repository.Invoking(r => r.DeleteValueAsync(ValueId, "Zoning", UserId, "Reason", RowVersion, CancellationToken.None))
            .Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task Throws_WhenTheFactoryDoesNotReturnADbConnection()
    {
        var repository = new ReferenceDataMaintenanceRepository(new NonDbConnectionFactory(), NullLogger<ReferenceDataMaintenanceRepository>.Instance);

        var act = () => repository.GetMaintainableTablesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    private sealed class NonDbConnectionFactory : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => new NonDbConnection();
    }

    private sealed class NonDbConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;

        public int ConnectionTimeout => 0;

        public string Database => string.Empty;

        public ConnectionState State => ConnectionState.Closed;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();

        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();

        public void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public void Close()
        {
        }

        public IDbCommand CreateCommand() => throw new NotSupportedException();

        public void Dispose()
        {
        }

        public void Open()
        {
        }
    }
}
