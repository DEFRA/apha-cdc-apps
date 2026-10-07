using System.Data.Common;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Shared helpers for reading nullable/positional columns from a forward-only <see cref="DbDataReader"/>.
/// Used by repositories (<see cref="SpeciesRepository"/>, <see cref="ProfileSectionRepository"/>)
/// that read stored-procedure result sets positionally because the result sets contain duplicate
/// or unnamed columns that Dapper cannot map by name.
/// </summary>
internal static class DbDataReaderExtensions
{
    public static byte[] ReadRowVersion(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? [] : (byte[])reader.GetValue(ordinal);

    public static string ReadString(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    public static string? ReadNullableString(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static Guid ReadGuid(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? Guid.Empty : reader.GetGuid(ordinal);

    public static Guid? ReadNullableGuid(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);

    public static bool ReadBoolean(this DbDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);

    public static bool? ReadNullableBoolean(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);

    public static int ReadInt32(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);

    public static decimal? ReadNullableDecimal(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);

    public static DateTime? ReadNullableDateTime(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
}
