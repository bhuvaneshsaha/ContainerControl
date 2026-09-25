using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ContainerControl.Modules.Delivery.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopeWorkerLeaseToApplicationEnvironment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "delivery",
                table: "worker_lease",
                keyColumn: "Id",
                keyColumnType: "integer",
                keyValue: 1);

            migrationBuilder.DropPrimaryKey(
                name: "PK_worker_lease",
                schema: "delivery",
                table: "worker_lease");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "delivery",
                table: "worker_lease");

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                schema: "delivery",
                table: "worker_lease",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "Environment",
                schema: "delivery",
                table: "worker_lease",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_worker_lease",
                schema: "delivery",
                table: "worker_lease",
                columns: new[] { "ApplicationId", "Environment" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_worker_lease",
                schema: "delivery",
                table: "worker_lease");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                schema: "delivery",
                table: "worker_lease");

            migrationBuilder.DropColumn(
                name: "Environment",
                schema: "delivery",
                table: "worker_lease");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                schema: "delivery",
                table: "worker_lease",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_worker_lease",
                schema: "delivery",
                table: "worker_lease",
                column: "Id");

            migrationBuilder.InsertData(
                schema: "delivery",
                table: "worker_lease",
                columns: new[] { "Id", "ExpiresAtUtc", "OwnerId" },
                values: new object[] { 1, null, null });
        }
    }
}
