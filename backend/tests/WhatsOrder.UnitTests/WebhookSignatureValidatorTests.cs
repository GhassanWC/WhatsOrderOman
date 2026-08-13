using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using WhatsOrder.Infrastructure.WhatsApp;
using Xunit;

namespace WhatsOrder.UnitTests;

public class WebhookSignatureValidatorTests
{
    private const string Secret = "my-app-secret";
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"object":"whatsapp_business_account"}""");

    private static string Sign(string secret, byte[] body) =>
        "sha256=" + Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body)).ToLowerInvariant();

    [Fact]
    public void Valid_signature_passes() =>
        WebhookSignatureValidator.IsValid(Secret, Body, Sign(Secret, Body)).Should().BeTrue();

    [Fact]
    public void Uppercase_hex_signature_passes() =>
        WebhookSignatureValidator.IsValid(Secret, Body, Sign(Secret, Body).ToUpperInvariant().Replace("SHA256=", "sha256="))
            .Should().BeTrue();

    [Fact]
    public void Wrong_secret_fails() =>
        WebhookSignatureValidator.IsValid(Secret, Body, Sign("other-secret", Body)).Should().BeFalse();

    [Fact]
    public void Tampered_body_fails()
    {
        var signature = Sign(Secret, Body);
        var tampered = Encoding.UTF8.GetBytes("""{"object":"tampered"}""");
        WebhookSignatureValidator.IsValid(Secret, tampered, signature).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha256=")]
    [InlineData("sha256=not-hex")]
    [InlineData("sha1=abcdef")]
    public void Missing_or_malformed_signature_fails(string? signature) =>
        WebhookSignatureValidator.IsValid(Secret, Body, signature).Should().BeFalse();

    [Fact]
    public void Empty_app_secret_always_fails() =>
        WebhookSignatureValidator.IsValid("", Body, Sign("", Body)).Should().BeFalse();
}
