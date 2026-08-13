using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WhatsOrder.Application.Auth;
using Xunit;

namespace WhatsOrder.IntegrationTests;

[Collection("api")]
public class AuthFlowTests(TestAppFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Register_returns_tokens_and_me_works()
    {
        var (client, auth) = await RegisterOwnerAsync();

        auth.AccessToken.Should().NotBeNullOrEmpty();
        auth.RefreshToken.Should().NotBeNullOrEmpty();
        auth.User.HasStore.Should().BeFalse();

        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me", Json);
        me!.Email.Should().Be(auth.User.Email);
    }

    [Fact]
    public async Task Register_with_duplicate_email_conflicts()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();

        var first = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Passw0rd!x", "One"), Json);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Passw0rd!x", "Two"), Json);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Weak_password_fails_validation()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(UniqueEmail(), "short", "Weak"), Json);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var (_, auth) = await RegisterOwnerAsync();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(auth.User.Email, "WrongPass1"), Json);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_succeeds_with_correct_credentials()
    {
        var (_, auth) = await RegisterOwnerAsync();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(auth.User.Email, "Passw0rd!x"), Json);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_rotates_and_detects_reuse()
    {
        var (_, auth) = await RegisterOwnerAsync();
        var client = Factory.CreateClient();

        // Rotate once: T1 → T2.
        var rotate = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken), Json);
        rotate.StatusCode.Should().Be(HttpStatusCode.OK);
        var newAuth = (await rotate.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        newAuth.RefreshToken.Should().NotBe(auth.RefreshToken);

        // Reusing revoked T1 must fail…
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken), Json);
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // …and revoke the whole family, so T2 is dead too.
        var afterReuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(newAuth.RefreshToken), Json);
        afterReuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var (_, auth) = await RegisterOwnerAsync();
        var client = Factory.CreateClient();

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(auth.RefreshToken), Json);
        logout.StatusCode.Should().Be(HttpStatusCode.OK);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken), Json);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_endpoints_require_a_token()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/store");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forgot_password_never_reveals_whether_the_account_exists()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest("nobody@test.om"), Json);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
