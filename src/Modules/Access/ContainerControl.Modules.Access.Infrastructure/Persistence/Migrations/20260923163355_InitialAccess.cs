using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ContainerControl.Modules.Access.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "access");

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_entries",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "break_glass_grants",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_break_glass_grants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permission_roles",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "access",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Module = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "api_tokens",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_api_tokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "access",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "access",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "access",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "access",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "access",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "access",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_permission_roles",
                schema: "access",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permission_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_user_permission_roles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "access",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_permission_roles_permission_roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "access",
                        principalTable: "permission_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "permission_role_permissions",
                schema: "access",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_role_permissions", x => new { x.RoleId, x.PermissionCode });
                    table.ForeignKey(
                        name: "FK_permission_role_permissions_permission_roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "access",
                        principalTable: "permission_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_permission_role_permissions_permissions_PermissionCode",
                        column: x => x.PermissionCode,
                        principalSchema: "access",
                        principalTable: "permissions",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "team_memberships",
                schema: "access",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_memberships", x => new { x.TeamId, x.UserId });
                    table.ForeignKey(
                        name: "FK_team_memberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "access",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_memberships_teams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "access",
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "permissions",
                columns: new[] { "Code", "Description", "DisplayName", "Module" },
                values: new object[,]
                {
                    { "access.audit.read", "Read the append-only audit log.", "Read audit", "Access" },
                    { "access.breakglass.grant", "Grant a short-lived permission.", "Grant break-glass", "Access" },
                    { "access.roles.manage", "Compose roles from the permission catalog.", "Manage roles", "Access" },
                    { "access.teams.manage", "Create teams and memberships.", "Manage teams", "Access" },
                    { "access.tokens.manage", "Issue and revoke CI API tokens.", "Manage API tokens", "Access" },
                    { "access.users.manage", "Create and disable user accounts.", "Manage users", "Access" },
                    { "access.users.read", "View provisioned user accounts.", "Read users", "Access" },
                    { "apps.read", "View applications the caller may access.", "Read applications", "Applications" },
                    { "apps.write", "Create and update applications.", "Write applications", "Applications" },
                    { "deploy.approve", "Accept a deploy that is waiting for approval.", "Approve deploys", "Delivery" },
                    { "deploy.execute", "Deploy and redeploy an application.", "Execute deploys", "Delivery" },
                    { "deploy.rollback", "Restore the last successful desired state.", "Roll back deploys", "Delivery" },
                    { "edge.certs.manage", "Upload and replace certificates.", "Manage certificates", "Edge" },
                    { "edge.dns.manage", "Save domains that applications may claim.", "Manage allowed domains", "Edge" },
                    { "platform.capacity.read", "Read host capacity.", "Read capacity", "Platform" },
                    { "platform.hosts.manage", "Register and update Docker hosts.", "Manage Docker hosts", "Platform" },
                    { "platform.quotas.manage", "Set team CPU, memory, and storage quotas.", "Manage quotas", "Platform" },
                    { "platform.settings.manage", "Change control-plane settings.", "Manage platform settings", "Platform" },
                    { "registries.manage", "Create and update registry connections.", "Manage registries", "Registries" },
                    { "registries.read", "View registry connections.", "Read registries", "Registries" },
                    { "runtime.control", "Start, stop, and restart services.", "Control runtime", "Runtime" },
                    { "runtime.logs.read", "Read container logs.", "Read logs", "Runtime" },
                    { "runtime.stats.read", "Read CPU and memory stats.", "Read stats", "Runtime" },
                    { "secrets.manage", "Write secret values for non-production environments.", "Manage secrets", "Secrets" },
                    { "secrets.manage.prod", "Write secret values for production.", "Manage production secrets", "Secrets" },
                    { "secrets.read", "View secret names and injection mode.", "Read secrets", "Secrets" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_tokens_UserId",
                schema: "access",
                table: "api_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "access",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "access",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "access",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "access",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_OccurredAtUtc",
                schema: "access",
                table: "audit_entries",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_break_glass_grants_UserId",
                schema: "access",
                table: "break_glass_grants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_permission_role_permissions_PermissionCode",
                schema: "access",
                table: "permission_role_permissions",
                column: "PermissionCode");

            migrationBuilder.CreateIndex(
                name: "IX_permission_roles_Name",
                schema: "access",
                table: "permission_roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_memberships_UserId",
                schema: "access",
                table: "team_memberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_teams_Name",
                schema: "access",
                table: "teams",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_roles_RoleId",
                schema: "access",
                table: "user_permission_roles",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_tokens",
                schema: "access");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "access");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "access");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "access");

            migrationBuilder.DropTable(
                name: "audit_entries",
                schema: "access");

            migrationBuilder.DropTable(
                name: "break_glass_grants",
                schema: "access");

            migrationBuilder.DropTable(
                name: "permission_role_permissions",
                schema: "access");

            migrationBuilder.DropTable(
                name: "team_memberships",
                schema: "access");

            migrationBuilder.DropTable(
                name: "user_permission_roles",
                schema: "access");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "access");

            migrationBuilder.DropTable(
                name: "teams",
                schema: "access");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "access");

            migrationBuilder.DropTable(
                name: "permission_roles",
                schema: "access");
        }
    }
}
