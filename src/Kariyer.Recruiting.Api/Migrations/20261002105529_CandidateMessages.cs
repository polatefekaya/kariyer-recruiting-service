using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kariyer.Recruiting.Api.Migrations
{
    /// <inheritdoc />
    public partial class CandidateMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_message",
                schema: "recruiting",
                columns: table => new
                {
                    uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    job_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    company_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    subject = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sent_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sent_by_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    recipient_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_message", x => x.uid);
                });

            migrationBuilder.CreateIndex(
                name: "ix_message_job_created",
                schema: "recruiting",
                table: "candidate_message",
                columns: new[] { "job_uid", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_message",
                schema: "recruiting");
        }
    }
}
