using ContainerControl.Modules.Applications.Persistence;
using ContainerControl.Modules.Applications.Workloads;
using ContainerControl.SharedKernel.Auditing;
using ContainerControl.SharedKernel.CurrentUser;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ContainerControl.Modules.Applications.Templates;

public sealed class TemplateAdmin
{
    public const int MaxNameLength = 80;
    public const int MaxDescriptionLength = 400;

    private readonly ApplicationsDbContext _db;
    private readonly WorkloadAdmin _apps;
    private readonly IComposeDocumentGate _compose;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ILogger<TemplateAdmin> _logger;

    public TemplateAdmin(
        ApplicationsDbContext db,
        WorkloadAdmin apps,
        IComposeDocumentGate compose,
        ICurrentUser currentUser,
        IClock clock,
        IAuditSink audit,
        ILogger<TemplateAdmin> logger)
    {
        _db = db;
        _apps = apps;
        _compose = compose;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AppTemplate>> ListAsync(CancellationToken cancellationToken)
    {
        return await _db.Templates
            .AsNoTracking()
            .OrderBy(template => template.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool Ok, Guid? Id, IReadOnlyList<string> Errors)> PublishAsync(
        string? name,
        string? description,
        string? composeYaml,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var trimmedName = (name ?? string.Empty).Trim();
        var trimmedDescription = (description ?? string.Empty).Trim();
        if (trimmedName.Length is 0 or > MaxNameLength)
        {
            errors.Add($"Enter a template name of {MaxNameLength} characters or fewer.");
        }

        if (trimmedDescription.Length is 0 or > MaxDescriptionLength)
        {
            errors.Add($"Enter a short description of {MaxDescriptionLength} characters or fewer.");
        }

        errors.AddRange(TemplateComposeSecrets.Find(trimmedName));
        errors.AddRange(TemplateComposeSecrets.Find(trimmedDescription));
        if (errors.Count > 0)
        {
            _logger.LogInformation("Application template was not published.");
            return (false, null, errors);
        }

        var gate = _compose.Accept(composeYaml ?? string.Empty);
        if (!gate.Accepted)
        {
            _logger.LogInformation("Application template was not published.");
            return (false, null, gate.Errors);
        }

        var nameKey = trimmedName.ToLowerInvariant();
        var duplicate = await _db.Templates.AnyAsync(template => template.NameKey == nameKey, cancellationToken);
        if (duplicate)
        {
            _logger.LogInformation("Application template was not published.");
            return (false, null, ["A template with that name already exists."]);
        }

        var template = new AppTemplate
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            NameKey = nameKey,
            Description = trimmedDescription,
            ComposeYaml = composeYaml ?? string.Empty,
            CreatedAtUtc = _clock.UtcNow
        };
        _db.Templates.Add(template);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            _logger.LogInformation("Application template was not published.");
            return (false, null, ["A template with that name already exists."]);
        }

        await _audit.WriteAsync(
            new AuditRecord("apps.templates.published", "template", template.Id.ToString(), _currentUser.UserId),
            cancellationToken);
        _logger.LogInformation("Published application template {TemplateId}.", template.Id);
        return (true, template.Id, []);
    }

    public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var template = await _db.Templates.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (template is null)
        {
            return false;
        }

        _db.Templates.Remove(template);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(
            new AuditRecord("apps.templates.removed", "template", id.ToString(), _currentUser.UserId),
            cancellationToken);
        _logger.LogInformation("Removed application template {TemplateId}.", id);
        return true;
    }

    public async Task<(bool Ok, Guid? Id, string? Error)> CreateApplicationAsync(
        Guid templateId,
        Guid teamId,
        Guid hostId,
        string name,
        string environment,
        int? internalPort,
        string? hostname,
        bool exposed,
        bool requiresApproval,
        bool allowDatabaseImages,
        CancellationToken cancellationToken)
    {
        var template = await _db.Templates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == templateId, cancellationToken);
        if (template is null)
        {
            return (false, null, "not-found");
        }

        var gate = _compose.Accept(template.ComposeYaml);
        if (!gate.Accepted)
        {
            _logger.LogInformation(
                "Application was not created from template {TemplateId}.",
                template.Id);
            return (false, null, "This template cannot be used.");
        }

        var created = await _apps.CreateAsync(
            teamId,
            hostId,
            name,
            environment,
            image: null,
            command: null,
            template.ComposeYaml,
            internalPort,
            hostname,
            exposed,
            requiresApproval,
            allowDatabaseImages,
            cancellationToken);
        if (!created.Ok || created.Id is null)
        {
            return created;
        }

        _logger.LogInformation(
            "Created application {ApplicationId} from template {TemplateId}.",
            created.Id,
            template.Id);
        return created;
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return true;
            }
        }

        return false;
    }
}
