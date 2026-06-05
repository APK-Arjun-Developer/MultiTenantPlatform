using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleScopeToRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default = 2 (Tenant) — applies to all existing tenant-scoped roles.
            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "Roles",
                type: "int",
                nullable: false,
                defaultValue: 2);

            // Backfill: system-scoped roles have TenantId = Guid.Empty → Scope = 1 (System).
            migrationBuilder.Sql(
                "UPDATE [Roles] SET [Scope] = 1 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Scope",
                table: "Roles");
        }
    }
}
