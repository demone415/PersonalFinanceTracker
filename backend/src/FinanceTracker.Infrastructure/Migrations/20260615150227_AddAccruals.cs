using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccruals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "change_logs",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValuesBefore = table.Column<string>(type: "text", nullable: true),
                    ValuesAfter = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_change_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "receipts",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FD = table.Column<long>(type: "bigint", nullable: true),
                    FN = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    FPD = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    AmountInKopecks = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExternalNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ShiftNumber = table.Column<int>(type: "integer", nullable: true),
                    INN = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    Cashier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TaxationType = table.Column<int>(type: "integer", nullable: true),
                    FetchStatus = table.Column<int>(type: "integer", nullable: false),
                    FetchAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextFetchAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RawMetadata = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "accruals",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IncludeInStats = table.Column<bool>(type: "boolean", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accruals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accruals_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "public",
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_accruals_receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "public",
                        principalTable: "receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "receipt_items",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Sum = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_receipt_items_receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "public",
                        principalTable: "receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "accrual_tags",
                schema: "public",
                columns: table => new
                {
                    AccrualId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accrual_tags", x => new { x.AccrualId, x.Tag });
                    table.ForeignKey(
                        name: "FK_accrual_tags_accruals_AccrualId",
                        column: x => x.AccrualId,
                        principalSchema: "public",
                        principalTable: "accruals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accrual_tags_Tag",
                schema: "public",
                table: "accrual_tags",
                column: "Tag");

            migrationBuilder.CreateIndex(
                name: "IX_accruals_CategoryId",
                schema: "public",
                table: "accruals",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_accruals_GroupId",
                schema: "public",
                table: "accruals",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_accruals_ReceiptId",
                schema: "public",
                table: "accruals",
                column: "ReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accruals_Type",
                schema: "public",
                table: "accruals",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_accruals_UserId_Date",
                schema: "public",
                table: "accruals",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_change_logs_EntityType_EntityId",
                schema: "public",
                table: "change_logs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_change_logs_UserId_Timestamp",
                schema: "public",
                table: "change_logs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_items_ReceiptId",
                schema: "public",
                table: "receipt_items",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_receipts_FetchStatus",
                schema: "public",
                table: "receipts",
                column: "FetchStatus");

            migrationBuilder.CreateIndex(
                name: "IX_receipts_UserId",
                schema: "public",
                table: "receipts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accrual_tags",
                schema: "public");

            migrationBuilder.DropTable(
                name: "change_logs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "receipt_items",
                schema: "public");

            migrationBuilder.DropTable(
                name: "accruals",
                schema: "public");

            migrationBuilder.DropTable(
                name: "receipts",
                schema: "public");
        }
    }
}
