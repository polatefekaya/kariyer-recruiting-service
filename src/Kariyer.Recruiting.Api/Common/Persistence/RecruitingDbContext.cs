using Kariyer.Recruiting.Api.Common.Persistence.Projections;
using Kariyer.Recruiting.Domain.Activity;
using Kariyer.Recruiting.Domain.Applications;
using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Notes;
using Kariyer.Recruiting.Domain.SavedFilters;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Recruiting.Api.Common.Persistence;

public sealed class RecruitingDbContext(DbContextOptions<RecruitingDbContext> options) : DbContext(options)
{
    public const string Schema = "recruiting";

    public DbSet<ApplicationPipeline> Pipelines => Set<ApplicationPipeline>();

    public DbSet<Interview> Interviews => Set<Interview>();

    public DbSet<ApplicationNote> Notes => Set<ApplicationNote>();

    public DbSet<ActivityEntry> Activity => Set<ActivityEntry>();

    public DbSet<Domain.Messaging.CandidateMessage> Messages => Set<Domain.Messaging.CandidateMessage>();

    public DbSet<SavedFilter> SavedFilters => Set<SavedFilter>();

    public DbSet<JobApplicationProjection> JobApplications => Set<JobApplicationProjection>();

    public DbSet<CompanyJobProjection> CompanyJobs => Set<CompanyJobProjection>();

    public DbSet<EmployeeProjection> Employees => Set<EmployeeProjection>();

    public DbSet<CompanyEmployeeProjection> CompanyEmployees => Set<CompanyEmployeeProjection>();

    public DbSet<CompanyProjection> Companies => Set<CompanyProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RecruitingDbContext).Assembly);
        modelBuilder.RegisterRecruitingFunctions();

        // The outbox lives in this schema and in this context on purpose: an event is staged in
        // the same transaction as the rows that justify it, which is only true while both are
        // written through the same connection.
        modelBuilder.AddTransactionalOutboxEntities();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<string>().HaveMaxLength(512);
    }
}
