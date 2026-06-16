using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "monthly_budgets",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    LimitAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monthly_budgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_monthly_budgets_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "public",
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_monthly_budgets_CategoryId",
                schema: "public",
                table: "monthly_budgets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_monthly_budgets_UserId_CategoryId_Year_Month",
                schema: "public",
                table: "monthly_budgets",
                columns: new[] { "UserId", "CategoryId", "Year", "Month" },
                unique: true);

            // Owner FK user_id → auth.users.id. auth.users is GoTrue-owned and not
            // in the EF model, hence raw SQL (mirrors the categories migration).
            migrationBuilder.Sql(
                """
                ALTER TABLE public.monthly_budgets
                    ADD CONSTRAINT "FK_monthly_budgets_auth_users_UserId"
                    FOREIGN KEY ("UserId") REFERENCES auth.users (id) ON DELETE CASCADE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "monthly_budgets",
                schema: "public");
        }
    }
}
