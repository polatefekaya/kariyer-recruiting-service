using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kariyer.Recruiting.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialRecruitingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "recruiting");

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION recruiting.kz_fold(value text)
                RETURNS text
                LANGUAGE sql
                IMMUTABLE
                PARALLEL SAFE
                RETURNS NULL ON NULL INPUT
                AS $$
                    SELECT lower(translate(value,
                        'İIıŞşĞğÜüÖöÇçÂâÎîÛû',
                        'iiissgguuooccaaiiuu'));
                $$;
                """);


            migrationBuilder.CreateTable(
                name: "activity_log",
                schema: "recruiting",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    application_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    job_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    company_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    actor_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    actor_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", maxLength: 512, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "application_note",
                schema: "recruiting",
                columns: table => new
                {
                    application_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    job_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    company_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    candidate_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    author_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_note", x => x.application_uid);
                });

            migrationBuilder.CreateTable(
                name: "application_pipeline",
                schema: "recruiting",
                columns: table => new
                {
                    application_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    job_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    company_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    candidate_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    changed_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_pipeline", x => x.application_uid);
                });

            migrationBuilder.CreateTable(
                name: "interview",
                schema: "recruiting",
                columns: table => new
                {
                    uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    application_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    job_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    company_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    candidate_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    video_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    location = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    candidate_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    internal_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    confirmation_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interview", x => x.uid);
                });

            migrationBuilder.CreateTable(
                name: "saved_filter",
                schema: "recruiting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    user_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    query = table.Column<string>(type: "jsonb", maxLength: 512, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_filter", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "interview_participant",
                schema: "recruiting",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    interview_uid = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interview_participant", x => x.id);
                    table.ForeignKey(
                        name: "FK_interview_participant_interview_interview_uid",
                        column: x => x.interview_uid,
                        principalSchema: "recruiting",
                        principalTable: "interview",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activity_application",
                schema: "recruiting",
                table: "activity_log",
                columns: new[] { "application_uid", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_activity_company",
                schema: "recruiting",
                table: "activity_log",
                columns: new[] { "company_uid", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_note_job",
                schema: "recruiting",
                table: "application_note",
                column: "job_uid");

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_candidate",
                schema: "recruiting",
                table: "application_pipeline",
                column: "candidate_uid");

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_company_changed",
                schema: "recruiting",
                table: "application_pipeline",
                columns: new[] { "company_uid", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_job_stage",
                schema: "recruiting",
                table: "application_pipeline",
                columns: new[] { "job_uid", "stage" });

            migrationBuilder.CreateIndex(
                name: "ix_interview_application_status",
                schema: "recruiting",
                table: "interview",
                columns: new[] { "application_uid", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_interview_candidate",
                schema: "recruiting",
                table: "interview",
                column: "candidate_uid");

            migrationBuilder.CreateIndex(
                name: "ix_interview_job_starts",
                schema: "recruiting",
                table: "interview",
                columns: new[] { "job_uid", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "IX_interview_participant_interview_uid",
                schema: "recruiting",
                table: "interview_participant",
                column: "interview_uid");

            migrationBuilder.CreateIndex(
                name: "ix_saved_filter_owner",
                schema: "recruiting",
                table: "saved_filter",
                columns: new[] { "company_uid", "user_uid" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS recruiting.kz_fold(text);");

            migrationBuilder.DropTable(
                name: "activity_log",
                schema: "recruiting");

            migrationBuilder.DropTable(
                name: "application_note",
                schema: "recruiting");

            migrationBuilder.DropTable(
                name: "application_pipeline",
                schema: "recruiting");

            migrationBuilder.DropTable(
                name: "interview_participant",
                schema: "recruiting");

            migrationBuilder.DropTable(
                name: "saved_filter",
                schema: "recruiting");

            migrationBuilder.DropTable(
                name: "interview",
                schema: "recruiting");
        }
    }
}
