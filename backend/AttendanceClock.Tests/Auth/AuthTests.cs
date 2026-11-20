using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AttendanceClock.Api.Auth;
using AttendanceClock.Api.Data;
using AttendanceClock.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AttendanceClock.Tests.Auth;

/// <summary>Starts the app once for all login tests: clean test database + demo accounts + one deactivated user.</summary>
public sealed class AuthFixture : IDisposable
{
    public ApiFactory Factory { get; }

    public AuthFixture()
    {
        ApiFactory.ResetDatabase();
        Factory = new ApiFactory();
        Factory.CreateClient().Dispose(); // starts the app → migrations + demo accounts

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Employee>>();
        var former = new Employee { UserName = "former", DisplayName = "Former Employee", IsActive = false };
        former.PasswordHash = hasher.HashPassword(former, "Employee123!");
        db.Employees.Add(former);
        db.SaveChanges();
    }

    public void Dispose() => Factory.Dispose();
}

/// <summary>Plan 06 test table.</summary>
public class AuthTests(AuthFixture fixture) : IClassFixture<AuthFixture>
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    private Task<HttpResponseMessage> Login(string userName, string password, HttpClient? client = null) =>
        (client ?? _client).PostAsJsonAsync("/api/auth/login", new { userName, password });

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<string> ErrorTitle(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("title").GetString()!;
    }

    [Fact] // 1
    public async Task Correct_password_returns_a_token_and_the_role()
    {
        var response = await Login("manager", "Manager123!");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("Manager", body.Employee.Role);
        Assert.Equal("Maya Manager", body.Employee.DisplayName);
    }

    [Fact] // 2
    public async Task Wrong_password_is_refused()
    {
        var response = await Login("dana", "WrongPassword!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", await ErrorCode(response));
    }

    [Fact] // 3
    public async Task Unknown_user_gets_exactly_the_same_answer_as_a_wrong_password()
    {
        var wrongPassword = await Login("dana", "WrongPassword!");
        var unknownUser = await Login("nobody", "WrongPassword!");

        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal(await ErrorCode(wrongPassword), await ErrorCode(unknownUser));
        Assert.Equal(await ErrorTitle(wrongPassword), await ErrorTitle(unknownUser));
    }

    [Fact] // 4
    public async Task Deactivated_employee_cannot_log_in_even_with_the_right_password()
    {
        var response = await Login("former", "Employee123!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", await ErrorCode(response));
    }

    [Fact] // 5
    public async Task Me_without_a_token_is_refused()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact] // 6
    public async Task Me_with_a_valid_token_returns_the_right_employee()
    {
        var login = await (await Login("dana", "Employee123!")).Content.ReadFromJsonAsync<LoginResponse>();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<EmployeeDto>();
        Assert.Equal("dana", me!.UserName);
        Assert.Equal("Employee", me.Role);
    }

    [Fact] // 7
    public async Task Me_with_a_tampered_token_is_refused()
    {
        var login = await (await Login("dana", "Employee123!")).Content.ReadFromJsonAsync<LoginResponse>();
        // Change one character of the signature, so the token no longer matches what the server signed.
        var token = login!.Token;
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact] // 8
    public async Task Sixth_login_attempt_within_a_minute_is_blocked()
    {
        // A separate app instance with the real limit (5 per minute).
        using var strictFactory = new ApiFactory { LoginAttemptsPerMinute = 5 };
        using var client = strictFactory.CreateClient();

        for (var attempt = 1; attempt <= 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login("dana", "guess" + attempt, client)).StatusCode);

        var sixth = await Login("dana", "Employee123!", client); // even the RIGHT password is blocked now

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.Equal("TOO_MANY_ATTEMPTS", await ErrorCode(sixth));
    }

    [Fact] // 9
    public async Task Passwords_are_stored_as_hashes_never_as_plain_text()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dana = await db.Employees.SingleAsync(e => e.UserName == "dana");

        Assert.NotEqual("Employee123!", dana.PasswordHash);
        Assert.DoesNotContain("Employee123", dana.PasswordHash);
        Assert.True(dana.PasswordHash.Length > 50); // salted PBKDF2 hash, base64-encoded
    }
}
