using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileFileId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProfileFileId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ProfileFileId",
                table: "Users",
                column: "ProfileFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Files_ProfileFileId",
                table: "Users",
                column: "ProfileFileId",
                principalTable: "Files",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Files_ProfileFileId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ProfileFileId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileFileId",
                table: "Users");
        }
    }
}
