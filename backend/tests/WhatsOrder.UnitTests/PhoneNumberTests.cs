using FluentAssertions;
using WhatsOrder.Application.Common;
using Xunit;

namespace WhatsOrder.UnitTests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("91234567", "+96891234567")]           // bare Omani mobile
    [InlineData("71234567", "+96871234567")]           // 7x mobile range
    [InlineData("24123456", "+96824123456")]           // landline
    [InlineData("091234567", "+96891234567")]          // trunk-style 0 prefix
    [InlineData("+968 9123 4567", "+96891234567")]     // spaces
    [InlineData("+968-9123-4567", "+96891234567")]     // dashes
    [InlineData("0096891234567", "+96891234567")]      // 00 international prefix
    [InlineData("96891234567", "+96891234567")]        // missing plus
    [InlineData("(968) 912-34567", "+96891234567")]    // decorated
    [InlineData("+971501234567", "+971501234567")]     // UAE — international allowed
    [InlineData("+14155552671", "+14155552671")]       // US
    public void Normalize_accepts_valid_numbers(string input, string expected) =>
        PhoneNumber.Normalize(input).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234")]              // too short
    [InlineData("abcdefgh")]          // letters
    [InlineData("12345678")]          // 8 digits but invalid Omani prefix (1)
    [InlineData("+96812345678")]      // Omani number starting with 1
    [InlineData("+9689123456")]       // Omani number with 7 national digits
    [InlineData("+968912345678")]     // Omani number with 9 national digits
    [InlineData("+0123456789")]       // leading zero after +
    [InlineData("91234567 ext 5")]    // trailing garbage
    public void Normalize_rejects_invalid_numbers(string? input) =>
        PhoneNumber.Normalize(input).Should().BeNull();

    [Fact]
    public void IsValid_matches_normalize_behaviour()
    {
        PhoneNumber.IsValid("91234567").Should().BeTrue();
        PhoneNumber.IsValid("garbage").Should().BeFalse();
    }
}
