using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_NormalizedName_TenantId",
                table: "Roles");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Roles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_NormalizedName_TenantId",
                table: "Roles",
                columns: new[] { "NormalizedName", "TenantId" },
                unique: true,
                filter: "[NormalizedName] IS NOT NULL AND [DeletedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_NormalizedName_TenantId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Roles");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_NormalizedName_TenantId",
                table: "Roles",
                columns: new[] { "NormalizedName", "TenantId" },
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");
        }
    }
}
