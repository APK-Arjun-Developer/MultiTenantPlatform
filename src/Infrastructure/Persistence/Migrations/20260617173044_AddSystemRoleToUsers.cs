using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemRoleToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SystemRole",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 3); // TenantUser

            // Backfill: platform system admin
            migrationBuilder.Sql(
                "UPDATE Users SET SystemRole = 1 " +
                "WHERE TenantId = '00000000-0000-0000-0000-000000000000'");

            // Backfill: tenant admins (assigned the TenantAdmin role)
            migrationBuilder.Sql(
                "UPDATE u SET u.SystemRole = 2 " +
                "FROM Users u " +
                "INNER JOIN AspNetUserRoles ur ON u.Id = ur.UserId " +
                "INNER JOIN Roles r ON ur.RoleId = r.Id " +
                "WHERE r.Name = 'TenantAdmin' " +
                "  AND u.TenantId != '00000000-0000-0000-0000-000000000000'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SystemRole",
                table: "Users");
        }
    }
}
