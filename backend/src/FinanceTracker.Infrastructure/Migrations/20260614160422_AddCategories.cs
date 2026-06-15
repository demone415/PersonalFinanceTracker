using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FinanceTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "categories",
                columns: new[] { "Id", "Color", "Icon", "IsSystem", "Name", "UserId" },
                values: new object[,]
                {
                    { new Guid("a1c00000-0000-7000-8000-000000000001"), "#22c55e", "shopping-cart", true, "Продукты", null },
                    { new Guid("a1c00000-0000-7000-8000-000000000002"), "#f97316", "utensils", true, "Кафе и рестораны", null },
                    { new Guid("a1c00000-0000-7000-8000-000000000003"), "#3b82f6", "car", true, "Транспорт", null },
                    { new Guid("a1c00000-0000-7000-8000-000000000004"), "#8b5cf6", "house", true, "Жильё", null },
                    { new Guid("a1c00000-0000-7000-8000-000000000005"), "#ef4444", "heart-pulse", true, "Здоровье", null },
                    { new Guid("a1c00000-0000-7000-8000-000000000006"), "#ec4899", "gamepad-2", true, "Развлечения", null },
                    { new Guid("a1c00000-0000-7000-8000-000000000007"), "#14b8a6", "shirt", true, "Одежда", null },
                    { new Guid("a1c00000-0000-7000-8000-000000000008"), "#06b6d4", "wifi", true, "Связь и интернет", null },
                    { new Guid("a1c00000-0000-7000-8000-000000000009"), "#eab308", "graduation-cap", true, "Образование", null },
                    { new Guid("a1c00000-0000-7000-8000-00000000000a"), "#d946ef", "gift", true, "Подарки", null },
                    { new Guid("a1c00000-0000-7000-8000-00000000000b"), "#10b981", "wallet", true, "Зарплата", null },
                    { new Guid("a1c00000-0000-7000-8000-00000000000c"), "#64748b", "ellipsis", true, "Прочее", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_UserId",
                schema: "public",
                table: "categories",
                column: "UserId");

            // Nullable FK user_id → auth.users.id (system categories keep null).
            // auth.users is GoTrue-owned and not in the EF model, hence raw SQL.
            migrationBuilder.Sql(
                """
                ALTER TABLE public.categories
                    ADD CONSTRAINT "FK_categories_auth_users_UserId"
                    FOREIGN KEY ("UserId") REFERENCES auth.users (id) ON DELETE CASCADE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "categories",
                schema: "public");
        }
    }
}
