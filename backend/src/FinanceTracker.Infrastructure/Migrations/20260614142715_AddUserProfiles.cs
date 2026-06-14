using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "user_profiles",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "RUB"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.Id);
                });

            // FK id → auth.users.id (ARCHITECTURE.md §1.2). auth.users is owned by
            // GoTrue and is not part of the EF model, so the constraint is added
            // here in raw SQL. Requires GoTrue to have migrated the auth schema
            // first (it does so on `docker compose up`, before `database update`).
            migrationBuilder.Sql(
                """
                ALTER TABLE public.user_profiles
                    ADD CONSTRAINT "FK_user_profiles_auth_users_Id"
                    FOREIGN KEY ("Id") REFERENCES auth.users (id) ON DELETE CASCADE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_profiles",
                schema: "public");
        }
    }
}
