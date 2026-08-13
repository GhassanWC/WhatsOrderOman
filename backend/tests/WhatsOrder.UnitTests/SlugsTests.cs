using FluentAssertions;
using WhatsOrder.Application.Common;
using Xunit;

namespace WhatsOrder.UnitTests;

public class SlugsTests
{
    [Theory]
    [InlineData("alreem")]
    [InlineData("al-reem")]
    [InlineData("shop123")]
    [InlineData("123shop")]
    [InlineData("abc")]
    public void Valid_slugs_pass(string slug) => Slugs.IsValid(slug).Should().BeTrue();

    [Theory]
    [InlineData("ab")]               // too short
    [InlineData("-alreem")]          // leading hyphen
    [InlineData("alreem-")]          // trailing hyphen
    [InlineData("al--reem")]         // double hyphen
    [InlineData("Al Reem")]          // spaces / uppercase raw — normalized first
    [InlineData("متجر")]             // non-latin
    [InlineData("api")]              // reserved
    [InlineData("dashboard")]        // reserved
    [InlineData("uploads")]          // reserved
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_or_reserved_slugs_fail(string? slug) => Slugs.IsValid(slug).Should().BeFalse();

    [Fact]
    public void Normalize_lowercases_and_trims() =>
        Slugs.Normalize("  AlReem ").Should().Be("alreem");
}
