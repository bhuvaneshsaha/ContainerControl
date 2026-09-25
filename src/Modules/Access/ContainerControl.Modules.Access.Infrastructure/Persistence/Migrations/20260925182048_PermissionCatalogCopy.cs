using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContainerControl.Modules.Access.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PermissionCatalogCopy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "access",
                table: "permissions",
                keyColumn: "Code",
                keyValue: "deploy.execute",
                column: "Description",
                value: "Deploy, redeploy, start a slot beside the live release, swap traffic, or set a canary percent.");

            migrationBuilder.UpdateData(
                schema: "access",
                table: "permissions",
                keyColumn: "Code",
                keyValue: "deploy.rollback",
                column: "Description",
                value: "Restore the last successful desired state, or send public traffic back to the previous slot.");

            migrationBuilder.UpdateData(
                schema: "access",
                table: "permissions",
                keyColumn: "Code",
                keyValue: "platform.settings.manage",
                column: "Description",
                value: "Change control-plane settings, including alert webhook and mail recipients.");

            migrationBuilder.UpdateData(
                schema: "access",
                table: "permissions",
                keyColumn: "Code",
                keyValue: "runtime.logs.read",
                column: "Description",
                value: "Read container logs, including lines kept after the Docker daemon rotates them.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "access",
                table: "permissions",
                keyColumn: "Code",
                keyValue: "deploy.execute",
                column: "Description",
                value: "Deploy and redeploy an application.");

            migrationBuilder.UpdateData(
                schema: "access",
                table: "permissions",
                keyColumn: "Code",
                keyValue: "deploy.rollback",
                column: "Description",
                value: "Restore the last successful desired state.");

            migrationBuilder.UpdateData(
                schema: "access",
                table: "permissions",
                keyColumn: "Code",
                keyValue: "platform.settings.manage",
                column: "Description",
                value: "Change control-plane settings.");

            migrationBuilder.UpdateData(
                schema: "access",
                table: "permissions",
                keyColumn: "Code",
                keyValue: "runtime.logs.read",
                column: "Description",
                value: "Read container logs.");
        }
    }
}
