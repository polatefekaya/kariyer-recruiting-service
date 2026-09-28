using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kariyer.Recruiting.Api.Common.Persistence;

/// <summary>Design-time only: `dotnet ef` needs a context without booting the host.</summary>
public sealed class RecruitingDbContextFactory : IDesignTimeDbContextFactory<RecruitingDbContext>
{
    public RecruitingDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<RecruitingDbContext> options = new();

        options.UseNpgsql(
            Environment.GetEnvironmentVariable("RECRUITING_DB")
                ?? "Host=localhost;Database=kariyer;Username=postgres;Password=postgres",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", RecruitingDbContext.Schema));

        return new RecruitingDbContext(options.Options);
    }
}
