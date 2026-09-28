using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kariyer.Recruiting.Api.Migrations
{
    /// <inheritdoc />
    public partial class LegacyStageFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION recruiting.kz_stage_from_legacy(value text)
                RETURNS text
                LANGUAGE sql
                IMMUTABLE
                PARALLEL SAFE
                AS $$
                    SELECT CASE lower(coalesce(value, ''))
                        WHEN 'pending'      THEN 'NEW'
                        WHEN 'under_review' THEN 'REVIEWING'
                        WHEN 'accepted'     THEN 'HIRED'
                        WHEN 'rejected'     THEN 'REJECTED'
                        WHEN 'withdrawn'    THEN 'WITHDRAWN'
                        ELSE 'NEW'
                    END;
                $$;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS recruiting.kz_stage_from_legacy(text);");

        }
    }
}
