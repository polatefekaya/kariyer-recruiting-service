using Microsoft.EntityFrameworkCore;

namespace Kariyer.Recruiting.Api.Common.Persistence;

/// <summary>
/// Maps to <c>recruiting.kz_fold(text)</c>, created by the first migration: lowercases and
/// strips Turkish diacritics inside Postgres so the search predicate stays sargable against the
/// expression indexes rather than pulling rows into memory to fold them.
/// </summary>
public static class RecruitingFunctions
{
    public static string Fold(string value) => throw new NotSupportedException("Database function.");

    /// <summary>
    /// Maps <c>job_application.application_status</c> to the opening pipeline stage, in SQL, so
    /// the list can project a stage for applications the pipeline has not touched yet.
    /// </summary>
    public static string StageFromLegacy(string value) => throw new NotSupportedException("Database function.");

    public static ModelBuilder RegisterRecruitingFunctions(this ModelBuilder builder)
    {
        builder.HasDbFunction(typeof(RecruitingFunctions).GetMethod(nameof(Fold))!)
            .HasName("kz_fold")
            .HasSchema(RecruitingDbContext.Schema);

        builder.HasDbFunction(typeof(RecruitingFunctions).GetMethod(nameof(StageFromLegacy))!)
            .HasName("kz_stage_from_legacy")
            .HasSchema(RecruitingDbContext.Schema);

        return builder;
    }
}
