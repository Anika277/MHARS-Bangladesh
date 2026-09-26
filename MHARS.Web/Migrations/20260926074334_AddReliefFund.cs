using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MHARS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddReliefFund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DonationCampaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    HazardType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    District = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    GoalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonationCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Donations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    DonorName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DonorEmail = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DonorPhone = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TranId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ValId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    BankTranId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CardType = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceiptToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Donations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Donations_DonationCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "DonationCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Donations_CampaignId",
                table: "Donations",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_ReceiptToken",
                table: "Donations",
                column: "ReceiptToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Donations_Status_CampaignId",
                table: "Donations",
                columns: new[] { "Status", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_Donations_TranId",
                table: "Donations",
                column: "TranId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Donations");

            migrationBuilder.DropTable(
                name: "DonationCampaigns");
        }
    }
}
