using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Maps UserProfile's optimistic-concurrency token to the PostgreSQL xmin
            // system column (CLAUDE.md, ARCHITECTURE.md §4). xmin exists on every row,
            // so no column is created — EF's scaffolded AddColumn is intentionally
            // removed (mirrors AddIdempotencyRecordsAndXminConcurrency).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // xmin is a system column — nothing to drop.
        }
    }
}
