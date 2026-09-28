using Kariyer.Recruiting.Api.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Recruiting.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public sealed class SchemaTests(RecruitingDatabase database)
{
    [Fact]
    public async Task Migrations_create_every_table_in_the_recruiting_schema()
    {
        string[] expected =
        [
            "activity_log", "application_note", "application_pipeline",
            "interview", "interview_participant", "saved_filter",
        ];

        foreach (string table in expected)
        {
            long? count = await database.ScalarAsync<long>(
                $"SELECT count(*) FROM information_schema.tables " +
                $"WHERE table_schema = 'recruiting' AND table_name = '{table}'");

            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task Nothing_is_created_in_the_public_schema_beyond_the_stand_ins()
    {
        // The five tables and the marker all come from deploy/smoke/public_standins.sql — the
        // fixture, not the migrations. `__standin_marker` is what the dev-only stand-in seeding
        // endpoint checks before it will write to public at all.
        long? count = await database.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' " +
            "AND table_name NOT IN ('company', 'employee', 'company_employee', 'company_job', " +
            "'job_application', '__standin_marker')");

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Migration_history_lives_in_the_recruiting_schema()
    {
        long? inRecruiting = await database.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.tables " +
            "WHERE table_schema = 'recruiting' AND table_name = '__EFMigrationsHistory'");

        long? inPublic = await database.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory'");

        Assert.Equal(1, inRecruiting);
        Assert.Equal(0, inPublic);
    }

    [Theory]
    [InlineData("Şimşek", "simsek")]
    [InlineData("İNCİ", "inci")]
    [InlineData("Gülşah ÜNAL", "gulsah unal")]
    public async Task Kz_fold_matches_the_domain_fold(string input, string expected)
    {
        string? folded = await database.ScalarAsync<string>($"SELECT recruiting.kz_fold('{input}')");

        Assert.Equal(expected, folded);
        Assert.Equal(Domain.Search.TurkishCasing.Normalize(input), folded);
    }

    [Fact]
    public async Task Projections_read_the_node_owned_tables()
    {
        await database.ExecuteAsync(
            """
            INSERT INTO public.company (uid, external_id, company_name)
            VALUES ('c-projection', 'sub-projection', 'PSB')
            ON CONFLICT (uid) DO NOTHING;
            """);

        await using RecruitingDbContext db = database.CreateContext();

        Assert.True(await db.Companies.AnyAsync(c => c.Uid == "c-projection"));
    }
}
