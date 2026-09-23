using System.Net.Http.Json;
using ContainerControl.Modules.Access.Domain.Roles;
using ContainerControl.Modules.Access.Domain.Users;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Host.IntegrationTests;

public static class ApiClient
{
    public const string SamplePassword = "Dev-User-Passw0rd!";

    public static async Task<HttpResponseMessage> SendAsync(
        this HttpClient client,
        HttpMethod method,
        string path,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (method != HttpMethod.Get && method != HttpMethod.Head)
        {
            var csrf = await client.GetAsync("/auth/csrf");
            csrf.EnsureSuccessStatusCode();
            var token = csrf.Headers.GetValues("X-XSRF-TOKEN").Single();
            request.Headers.Add("X-XSRF-TOKEN", token);
        }

        return await client.SendAsync(request);
    }

    public static async Task SignInAsync(this HttpClient client, string email, string password)
    {
        var response = await client.SendAsync(HttpMethod.Post, "/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
    }

    public static async Task<User> CreateUserAsync(
        ApiFactory factory,
        string email,
        params string[] permissionCodes)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = email,
            LockoutEnabled = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var result = await users.CreateAsync(user, SamplePassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        if (permissionCodes.Length > 0)
        {
            var role = new PermissionRole
            {
                Id = Guid.NewGuid(),
                Name = "test-" + Guid.NewGuid().ToString("n"),
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            foreach (var code in permissionCodes.Distinct(StringComparer.Ordinal))
            {
                role.Permissions.Add(new PermissionRolePermission
                {
                    RoleId = role.Id,
                    PermissionCode = code
                });
            }

            db.PermissionRoles.Add(role);
            db.UserPermissionRoles.Add(new UserPermissionRole { UserId = user.Id, RoleId = role.Id });
            await db.SaveChangesAsync();
        }

        return user;
    }

    public static async Task<IReadOnlyList<string>> ReadPermissionsAsync(this HttpClient client)
    {
        var response = await client.GetAsync("/me/permissions");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<PermissionsBody>();
        return body?.Permissions ?? [];
    }

    private sealed record PermissionsBody(IReadOnlyList<string> Permissions);
}
