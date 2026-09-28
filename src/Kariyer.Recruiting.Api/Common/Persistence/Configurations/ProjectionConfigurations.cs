using Kariyer.Recruiting.Api.Common.Persistence.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kariyer.Recruiting.Api.Common.Persistence.Configurations;

public sealed class JobApplicationProjectionConfiguration : IEntityTypeConfiguration<JobApplicationProjection>
{
    public void Configure(EntityTypeBuilder<JobApplicationProjection> builder)
    {
        builder.HasNoKey().ToView("job_application", "public");

        builder.Property(x => x.Uid).HasColumnName("uid");
        builder.Property(x => x.JobUid).HasColumnName("job_uid");
        builder.Property(x => x.ApplicantUid).HasColumnName("applicant_uid");
        builder.Property(x => x.ResumeId).HasColumnName("resume_id");
        builder.Property(x => x.ApplicationStatus).HasColumnName("application_status");
        builder.Property(x => x.AppliedAt).HasColumnName("applied_at");
    }
}

public sealed class CompanyJobProjectionConfiguration : IEntityTypeConfiguration<CompanyJobProjection>
{
    public void Configure(EntityTypeBuilder<CompanyJobProjection> builder)
    {
        builder.HasNoKey().ToView("company_job", "public");

        builder.Property(x => x.Uid).HasColumnName("uid");
        builder.Property(x => x.CompanyUid).HasColumnName("company_uid");
        builder.Property(x => x.Title).HasColumnName("title");
        builder.Property(x => x.Department).HasColumnName("department");
        builder.Property(x => x.Position).HasColumnName("position");
        builder.Property(x => x.Province).HasColumnName("province");
        builder.Property(x => x.Town).HasColumnName("town");
    }
}

public sealed class EmployeeProjectionConfiguration : IEntityTypeConfiguration<EmployeeProjection>
{
    public void Configure(EntityTypeBuilder<EmployeeProjection> builder)
    {
        builder.HasNoKey().ToView("employee", "public");

        builder.Property(x => x.Uid).HasColumnName("uid");
        builder.Property(x => x.Username).HasColumnName("username");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.Surname).HasColumnName("surname");
        builder.Property(x => x.Email).HasColumnName("email");
        builder.Property(x => x.Phone).HasColumnName("phone");
        builder.Property(x => x.PhotoUrl).HasColumnName("photo_url");
        builder.Property(x => x.Province).HasColumnName("province");
        builder.Property(x => x.Town).HasColumnName("town");
    }
}

public sealed class CompanyEmployeeProjectionConfiguration : IEntityTypeConfiguration<CompanyEmployeeProjection>
{
    public void Configure(EntityTypeBuilder<CompanyEmployeeProjection> builder)
    {
        builder.HasNoKey().ToView("company_employee", "public");

        builder.Property(x => x.CompanyUid).HasColumnName("company_uid");
        builder.Property(x => x.EmployeeUid).HasColumnName("employee_uid");
        builder.Property(x => x.Position).HasColumnName("position");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
    }
}

public sealed class CompanyProjectionConfiguration : IEntityTypeConfiguration<CompanyProjection>
{
    public void Configure(EntityTypeBuilder<CompanyProjection> builder)
    {
        builder.HasNoKey().ToView("company", "public");

        builder.Property(x => x.Uid).HasColumnName("uid");
        builder.Property(x => x.ExternalId).HasColumnName("external_id");
        builder.Property(x => x.CompanyName).HasColumnName("company_name");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.IsAccountCompleted).HasColumnName("is_account_completed");
    }
}
