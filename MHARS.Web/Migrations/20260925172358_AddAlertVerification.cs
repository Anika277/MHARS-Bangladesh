using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MHARS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "Alerts",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationNote",
                table: "Alerts",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerificationStatus",
                table: "Alerts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "Alerts",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "VerificationNote",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "Alerts");
        }
    }
}
