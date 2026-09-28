using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kariyer.Recruiting.Api.Migrations
{
    /// <inheritdoc />
    public partial class InterviewerOnInterview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "interviewer_uid",
                schema: "recruiting",
                table: "interview",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "interviewer_uid",
                schema: "recruiting",
                table: "interview");
        }
    }
}
