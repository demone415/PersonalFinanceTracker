using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptQrRawAndRetryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_receipts_FetchStatus",
                schema: "public",
                table: "receipts");

            migrationBuilder.AddColumn<string>(
                name: "QrRaw",
                schema: "public",
                table: "receipts",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipts_FetchStatus_NextFetchAt",
                schema: "public",
                table: "receipts",
                columns: new[] { "FetchStatus", "NextFetchAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_receipts_FetchStatus_NextFetchAt",
                schema: "public",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "QrRaw",
                schema: "public",
                table: "receipts");

            migrationBuilder.CreateIndex(
                name: "IX_receipts_FetchStatus",
                schema: "public",
                table: "receipts",
                column: "FetchStatus");
        }
    }
}
