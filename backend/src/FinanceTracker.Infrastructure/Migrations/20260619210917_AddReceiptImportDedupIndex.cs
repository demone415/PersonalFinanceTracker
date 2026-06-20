using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptImportDedupIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_receipts_UserId_ExternalNumber_INN_Date",
                schema: "public",
                table: "receipts",
                columns: new[] { "UserId", "ExternalNumber", "INN", "Date" },
                unique: true,
                filter: "\"ExternalNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_receipts_UserId_ExternalNumber_INN_Date",
                schema: "public",
                table: "receipts");
        }
    }
}
