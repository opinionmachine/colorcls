using Xunit;

public class PosixTests
{
    // Winter, summer, before 1970, and the hours around both European clock changes.
    [Theory]
    [InlineData("2024-01-15T10:30:00")]
    [InlineData("2024-07-15T10:30:00")]
    [InlineData("1965-06-01T00:00:00")]
    [InlineData("2024-03-31T00:59:59")]
    [InlineData("2024-03-31T01:00:00")]
    [InlineData("2024-10-27T00:59:59")]
    [InlineData("2024-10-27T01:00:00")]
    public void LocalTimeAgreesWithTheBcl(string iso)
    {
        var utc = DateTime.SpecifyKind(DateTime.Parse(iso, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
        Assert.Equal(utc.ToLocalTime().Ticks, Posix.LocalTime(utc).Ticks);
    }
}
