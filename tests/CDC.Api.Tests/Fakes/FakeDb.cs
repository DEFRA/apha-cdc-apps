using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace CDC.Api.Tests.Fakes;

/// <summary>
/// Minimal in-memory ADO.NET provider. Dapper runs against <see cref="DbConnection"/> rather
/// than an interface, so the only way to unit test a Dapper repository without a live SQL
/// Server is to give it a real - but fake - provider. Every command is recorded, so tests can
/// assert on stored procedure names, parameters and transaction use.
/// </summary>
internal sealed class FakeDbConnection : DbConnection
{
    private ConnectionState state = ConnectionState.Closed;

    /// <summary>Scripted behaviour, keyed by stored procedure name.</summary>
    public Dictionary<string, FakeCommandScript> Scripts { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every command executed, in order.</summary>
    public List<RecordedCommand> Executed { get; } = [];

    /// <summary>Transactions started on this connection.</summary>
    public List<FakeDbTransaction> Transactions { get; } = [];

    [AllowNull]
    public override string ConnectionString { get; set; } = "Fake";

    public override string Database => "FakeDatabase";

    public override string DataSource => "FakeDataSource";

    public override string ServerVersion => "0.0";

    public override ConnectionState State => state;

    public void Script(string commandText, FakeCommandScript script) => Scripts[commandText] = script;

    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

    public override void Close() => state = ConnectionState.Closed;

    public override void Open() => state = ConnectionState.Open;

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        var transaction = new FakeDbTransaction(this, isolationLevel);
        Transactions.Add(transaction);

        return transaction;
    }

    protected override DbCommand CreateDbCommand() => new FakeDbCommand(this);
}

/// <summary>Scripted response for a single stored procedure.</summary>
internal sealed class FakeCommandScript
{
    /// <summary>Result sets returned by <c>ExecuteReader</c>, in order.</summary>
    public IReadOnlyList<FakeResultSet> ResultSets { get; init; } = [];

    /// <summary>Thrown instead of executing, to simulate a database failure.</summary>
    public Exception? Throws { get; init; }

    /// <summary>Values assigned to output parameters after execution.</summary>
    public IReadOnlyDictionary<string, object?> OutputValues { get; init; } = new Dictionary<string, object?>();
}

/// <summary>One scripted result set.</summary>
/// <param name="Columns">Column names, in ordinal order.</param>
/// <param name="Rows">Row values, matching <paramref name="Columns"/>.</param>
internal sealed record FakeResultSet(string[] Columns, IReadOnlyList<object?[]> Rows)
{
    public static FakeResultSet Empty(params string[] columns) => new(columns, []);
}

/// <summary>A command as executed, captured for assertions.</summary>
/// <param name="CommandText">The stored procedure name.</param>
/// <param name="CommandType">The command type used.</param>
/// <param name="Parameters">Parameter names and values at execution time.</param>
/// <param name="HadTransaction">Whether the command was enlisted in a transaction.</param>
internal sealed record RecordedCommand(
    string CommandText,
    CommandType CommandType,
    IReadOnlyDictionary<string, object?> Parameters,
    bool HadTransaction);

internal sealed class FakeDbTransaction(FakeDbConnection connection, IsolationLevel isolationLevel) : DbTransaction
{
    public bool Committed { get; private set; }

    public bool RolledBack { get; private set; }

    public override IsolationLevel IsolationLevel => isolationLevel;

    protected override DbConnection DbConnection => connection;

    public override void Commit() => Committed = true;

    public override void Rollback() => RolledBack = true;
}

internal sealed class FakeDbCommand(FakeDbConnection connection) : DbCommand
{
    private readonly FakeDbParameterCollection parameters = [];

    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;

    public override int CommandTimeout { get; set; }

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection { get; set; } = connection;

    protected override DbParameterCollection DbParameterCollection => parameters;

    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery()
    {
        var script = Run();
        ApplyOutputValues(script);

        return 1;
    }

    public override object? ExecuteScalar()
    {
        var resultSets = Run().ResultSets;

        return resultSets.Count > 0 && resultSets[0].Rows.Count > 0 ? resultSets[0].Rows[0][0] : null;
    }

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter() => new FakeDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        new FakeDbDataReader(Run().ResultSets);

    private FakeCommandScript Run()
    {
        connection.Executed.Add(new RecordedCommand(
            CommandText,
            CommandType,
            parameters.Cast<DbParameter>().ToDictionary(
                parameter => parameter.ParameterName,
                parameter => parameter.Value == DBNull.Value ? null : parameter.Value,
                StringComparer.OrdinalIgnoreCase),
            DbTransaction is not null));

        if (!connection.Scripts.TryGetValue(CommandText, out var script))
        {
            return new FakeCommandScript();
        }

        return script.Throws is null ? script : throw script.Throws;
    }

    private void ApplyOutputValues(FakeCommandScript script)
    {
        foreach (var (name, value) in script.OutputValues)
        {
            foreach (DbParameter parameter in parameters)
            {
                // Dapper strips the "@" prefix when it creates parameters, and SQL Server
                // accepts either form, so compare without it.
                if (string.Equals(parameter.ParameterName.TrimStart('@'), name.TrimStart('@'), StringComparison.OrdinalIgnoreCase))
                {
                    parameter.Value = value;
                }
            }
        }
    }
}

internal sealed class FakeDbParameter : DbParameter
{
    public override DbType DbType { get; set; }

    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

    public override bool IsNullable { get; set; }

    [AllowNull]
    public override string ParameterName { get; set; } = string.Empty;

    public override int Size { get; set; }

    [AllowNull]
    public override string SourceColumn { get; set; } = string.Empty;

    public override bool SourceColumnNullMapping { get; set; }

    public override object? Value { get; set; }

    public override void ResetDbType() => DbType = DbType.Object;
}

internal sealed class FakeDbParameterCollection : DbParameterCollection, IEnumerable<DbParameter>
{
    private readonly List<DbParameter> items = [];

    public override int Count => items.Count;

    public override object SyncRoot { get; } = new();

    public override int Add(object value)
    {
        items.Add((DbParameter)value);
        return items.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
        {
            Add(value);
        }
    }

    public override void Clear() => items.Clear();

    public override bool Contains(object value) => items.Contains((DbParameter)value);

    public override bool Contains(string value) => IndexOf(value) >= 0;

    public override void CopyTo(Array array, int index) => ((ICollection)items).CopyTo(array, index);

    public override IEnumerator GetEnumerator() => items.GetEnumerator();

    public override int IndexOf(object value) => items.IndexOf((DbParameter)value);

    public override int IndexOf(string parameterName) =>
        items.FindIndex(item => string.Equals(item.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));

    public override void Insert(int index, object value) => items.Insert(index, (DbParameter)value);

    public override void Remove(object value) => items.Remove((DbParameter)value);

    public override void RemoveAt(int index) => items.RemoveAt(index);

    public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));

    protected override DbParameter GetParameter(int index) => items[index];

    protected override DbParameter GetParameter(string parameterName) => items[IndexOf(parameterName)];

    protected override void SetParameter(int index, DbParameter value) => items[index] = value;

    protected override void SetParameter(string parameterName, DbParameter value) => items[IndexOf(parameterName)] = value;

    IEnumerator<DbParameter> IEnumerable<DbParameter>.GetEnumerator() => items.GetEnumerator();
}

internal sealed class FakeDbDataReader(IReadOnlyList<FakeResultSet> resultSets) : DbDataReader
{
    private int resultSetIndex;
    private int rowIndex = -1;
    private bool closed;

    private FakeResultSet Current => resultSets[resultSetIndex];

    private object?[] CurrentRow => Current.Rows[rowIndex];

    public override int Depth => 0;

    public override int FieldCount => resultSetIndex < resultSets.Count ? Current.Columns.Length : 0;

    public override bool HasRows => resultSetIndex < resultSets.Count && Current.Rows.Count > 0;

    public override bool IsClosed => closed;

    public override int RecordsAffected => 0;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal), Culture);

    public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal), Culture);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        var value = (byte[])GetValue(ordinal);

        if (buffer is null)
        {
            return value.Length;
        }

        var count = Math.Min(length, value.Length - (int)dataOffset);
        Array.Copy(value, dataOffset, buffer, bufferOffset, count);

        return count;
    }

    public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal), Culture);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
        throw new NotSupportedException();

    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal), Culture);

    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal), Culture);

    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal), Culture);

    public override Type GetFieldType(int ordinal)
    {
        foreach (var row in Current.Rows)
        {
            if (row[ordinal] is not null)
            {
                return row[ordinal]!.GetType();
            }
        }

        return typeof(object);
    }

    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal), Culture);

    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);

    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal), Culture);

    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal), Culture);

    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal), Culture);

    public override string GetName(int ordinal) => Current.Columns[ordinal];

    public override int GetOrdinal(string name) =>
        Array.FindIndex(Current.Columns, column => string.Equals(column, name, StringComparison.OrdinalIgnoreCase));

    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override object GetValue(int ordinal) => CurrentRow[ordinal] ?? DBNull.Value;

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);

        for (var index = 0; index < count; index++)
        {
            values[index] = GetValue(index);
        }

        return count;
    }

    public override bool IsDBNull(int ordinal) => CurrentRow[ordinal] is null;

    public override bool NextResult()
    {
        resultSetIndex++;
        rowIndex = -1;

        return resultSetIndex < resultSets.Count;
    }

    public override bool Read()
    {
        if (resultSetIndex >= resultSets.Count)
        {
            return false;
        }

        rowIndex++;

        return rowIndex < Current.Rows.Count;
    }

    public override IEnumerator GetEnumerator() => Current.Rows.GetEnumerator();

    public override void Close() => closed = true;

    private static IFormatProvider Culture => System.Globalization.CultureInfo.InvariantCulture;
}

/// <summary>A <see cref="DbException"/> that tests can construct and throw.</summary>
internal sealed class FakeDbException(string message) : DbException(message);
