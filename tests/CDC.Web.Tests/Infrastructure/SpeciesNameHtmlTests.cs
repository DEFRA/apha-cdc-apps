using CDC.Web.Infrastructure;


namespace CDC.Web.Tests.Infrastructure;

public sealed class SpeciesNameHtmlTests
{
    [Theory]
    [InlineData("<em>Bos taurus</em> cattle", "<em>Bos taurus</em> cattle")]
    [InlineData("<i>Ovis</i> aries", "<i>Ovis</i> aries")]
    [InlineData("<script>alert(1)</script>Sheep", "alert(1)Sheep")]
    [InlineData("<p>Goat</p>", "Goat")]
    [InlineData("Line\r\nbreak", "Linebreak")]
    [InlineData("  Horse  ", "Horse")]
    [InlineData(null, "")]
    public void Clean_KeepsOnlyItalicTags(string? input, string expected) =>
        Assert.Equal(expected, SpeciesNameHtml.Clean(input));

    [Fact]
    public void ToPlainText_RemovesMarkup() =>
        Assert.Equal("Bos taurus", SpeciesNameHtml.ToPlainText("<em>Bos</em> taurus"));
}
