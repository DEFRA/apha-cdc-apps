using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;

namespace CDC.Api.Tests.Infrastructure;

public sealed class DbDataReaderExtensionsTests
{
    private static FakeDbDataReader CreateReader(object? value)
    {
        var reader = new FakeDbDataReader([new FakeResultSet(["Column"], [[value]])]);
        reader.Read();

        return reader;
    }

    [Fact]
    public void ReadRowVersion_ReturnsEmptyArray_WhenColumnIsNull()
    {
        CreateReader(null).ReadRowVersion(0).Should().BeEmpty();
    }

    [Fact]
    public void ReadRowVersion_ReturnsValue_WhenColumnIsNotNull()
    {
        var bytes = new byte[] { 1, 2, 3 };
        CreateReader(bytes).ReadRowVersion(0).Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public void ReadString_ReturnsEmptyString_WhenColumnIsNull()
    {
        CreateReader(null).ReadString(0).Should().Be(string.Empty);
    }

    [Fact]
    public void ReadString_ReturnsValue_WhenColumnIsNotNull()
    {
        CreateReader("Value").ReadString(0).Should().Be("Value");
    }

    [Fact]
    public void ReadNullableString_ReturnsNull_WhenColumnIsNull()
    {
        CreateReader(null).ReadNullableString(0).Should().BeNull();
    }

    [Fact]
    public void ReadNullableString_ReturnsValue_WhenColumnIsNotNull()
    {
        CreateReader("Value").ReadNullableString(0).Should().Be("Value");
    }

    [Fact]
    public void ReadGuid_ReturnsEmptyGuid_WhenColumnIsNull()
    {
        CreateReader(null).ReadGuid(0).Should().Be(Guid.Empty);
    }

    [Fact]
    public void ReadGuid_ReturnsValue_WhenColumnIsNotNull()
    {
        var guid = Guid.NewGuid();
        CreateReader(guid).ReadGuid(0).Should().Be(guid);
    }

    [Fact]
    public void ReadNullableGuid_ReturnsNull_WhenColumnIsNull()
    {
        CreateReader(null).ReadNullableGuid(0).Should().BeNull();
    }

    [Fact]
    public void ReadNullableGuid_ReturnsValue_WhenColumnIsNotNull()
    {
        var guid = Guid.NewGuid();
        CreateReader(guid).ReadNullableGuid(0).Should().Be(guid);
    }

    [Fact]
    public void ReadBoolean_ReturnsFalse_WhenColumnIsNull()
    {
        CreateReader(null).ReadBoolean(0).Should().BeFalse();
    }

    [Fact]
    public void ReadBoolean_ReturnsValue_WhenColumnIsNotNull()
    {
        CreateReader(true).ReadBoolean(0).Should().BeTrue();
    }

    [Fact]
    public void ReadNullableBoolean_ReturnsNull_WhenColumnIsNull()
    {
        CreateReader(null).ReadNullableBoolean(0).Should().BeNull();
    }

    [Fact]
    public void ReadNullableBoolean_ReturnsValue_WhenColumnIsNotNull()
    {
        CreateReader(true).ReadNullableBoolean(0).Should().BeTrue();
    }

    [Fact]
    public void ReadInt32_ReturnsZero_WhenColumnIsNull()
    {
        CreateReader(null).ReadInt32(0).Should().Be(0);
    }

    [Fact]
    public void ReadInt32_ReturnsValue_WhenColumnIsNotNull()
    {
        CreateReader(42).ReadInt32(0).Should().Be(42);
    }

    [Fact]
    public void ReadNullableDecimal_ReturnsNull_WhenColumnIsNull()
    {
        CreateReader(null).ReadNullableDecimal(0).Should().BeNull();
    }

    [Fact]
    public void ReadNullableDecimal_ReturnsValue_WhenColumnIsNotNull()
    {
        CreateReader(12.5m).ReadNullableDecimal(0).Should().Be(12.5m);
    }

    [Fact]
    public void ReadNullableDateTime_ReturnsNull_WhenColumnIsNull()
    {
        CreateReader(null).ReadNullableDateTime(0).Should().BeNull();
    }

    [Fact]
    public void ReadNullableDateTime_ReturnsValue_WhenColumnIsNotNull()
    {
        var dateTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        CreateReader(dateTime).ReadNullableDateTime(0).Should().Be(dateTime);
    }
}
