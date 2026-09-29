using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kariyer.Recruiting.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixLegacyStageFunctionEnum : Migration
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

                CREATE OR REPLACE FUNCTION recruiting.kz_stage_from_legacy(value anyelement)
                RETURNS text
                LANGUAGE sql
                IMMUTABLE
                PARALLEL SAFE
                AS $$
                    SELECT recruiting.kz_stage_from_legacy(value::text);
                $$;

                DO $$
                DECLARE
                    v_schema text;
                BEGIN
                    SELECT n.nspname INTO v_schema
                    FROM pg_type t
                    JOIN pg_namespace n ON n.oid = t.typnamespace
                    WHERE t.typname = 'enum_job_application_application_status'
                    LIMIT 1;

                    IF v_schema IS NOT NULL THEN
                        EXECUTE format('
                            CREATE OR REPLACE FUNCTION recruiting.kz_stage_from_legacy(value %I.enum_job_application_application_status)
                            RETURNS text
                            LANGUAGE sql
                            IMMUTABLE
                            PARALLEL SAFE
                            AS $func$
                                SELECT recruiting.kz_stage_from_legacy(value::text);
                            $func$;
                        ', v_schema);
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS recruiting.kz_stage_from_legacy(anyelement);
                DO $$
                DECLARE
                    v_schema text;
                BEGIN
                    SELECT n.nspname INTO v_schema
                    FROM pg_type t
                    JOIN pg_namespace n ON n.oid = t.typnamespace
                    WHERE t.typname = 'enum_job_application_application_status'
                    LIMIT 1;

                    IF v_schema IS NOT NULL THEN
                        EXECUTE format('DROP FUNCTION IF EXISTS recruiting.kz_stage_from_legacy(%I.enum_job_application_application_status);', v_schema);
                    END IF;
                END $$;
                """);
        }
    }
}
