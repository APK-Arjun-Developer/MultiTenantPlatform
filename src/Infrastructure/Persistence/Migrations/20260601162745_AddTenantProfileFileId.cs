using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantProfileFileId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProfileFileId",
                table: "Tenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ProfileFileId",
                table: "Tenants",
                column: "ProfileFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Files_ProfileFileId",
                table: "Tenants",
                column: "ProfileFileId",
                principalTable: "Files",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Files_ProfileFileId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_ProfileFileId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ProfileFileId",
                table: "Tenants");
        }
    }
}
