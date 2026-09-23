using System.Security.Cryptography;
using System.Text;
using ContainerControl.Modules.Access.Application.Tokens;
using ContainerControl.Modules.Access.Domain.Tokens;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.SharedKernel.Auditing;
using ContainerControl.SharedKernel.CurrentUser;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Access.Infrastructure.Tokens;

public sealed class TokenAdminService : IApiTokenAuthenticator
{
    private readonly AccessDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditSink _audit;

    public TokenAdminService(AccessDbContext db, IClock clock, ICurrentUser currentUser, IAuditSink audit)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ApiToken>> ListAsync(Guid userId, CancellationToken cancellationToken) =>
        await _db.ApiTokens.AsNoTracking()
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .OrderBy(token => token.Name)
            .ToListAsync(cancellationToken);

    public async Task<(bool Ok, Guid? Id, string? Plaintext, string? Error)> IssueAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, null, null, "Enter a token name.");
        }

        var plaintext = "cc_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var token = new ApiToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name.Trim(),
            TokenHash = Hash(plaintext),
            CreatedAtUtc = _clock.UtcNow
        };
        _db.ApiTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditRecord("access.token.issued", "api-token", token.Id.ToString(), _currentUser.UserId), cancellationToken);
        return (true, token.Id, plaintext, null);
    }

    public async Task<Guid?> AuthenticateAsync(string plaintext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return null;
        }

        var hash = Hash(plaintext.Trim());
        var token = await _db.ApiTokens.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TokenHash == hash && item.RevokedAtUtc == null, cancellationToken);
        return token?.UserId;
    }

    private static string Hash(string plaintext)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
