using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantConfigAndEmailBroadcast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    WelcomeEmailEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MaintenanceMode = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaintenanceMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AllowPasswordReset = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantConfigs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfigs_TenantId",
                table: "TenantConfigs",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantConfigs");
        }
    }
}
