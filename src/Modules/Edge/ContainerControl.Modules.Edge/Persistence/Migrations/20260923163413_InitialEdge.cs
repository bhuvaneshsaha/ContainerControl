using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ContainerControl.Modules.Edge.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialEdge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "edge");

            migrationBuilder.CreateTable(
                name: "module_boundary",
                schema: "edge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModuleName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_module_boundary", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "edge",
                table: "module_boundary",
                columns: new[] { "Id", "ModuleName" },
                values: new object[] { 1, "Edge" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "module_boundary",
                schema: "edge");
        }
    }
}
