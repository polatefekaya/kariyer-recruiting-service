using Kariyer.Recruiting.Domain.Activity;
using Kariyer.Recruiting.Domain.Applications;
using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Notes;
using Kariyer.Recruiting.Domain.SavedFilters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kariyer.Recruiting.Api.Common.Persistence.Configurations;

public sealed class ApplicationPipelineConfiguration : IEntityTypeConfiguration<ApplicationPipeline>
{
    public void Configure(EntityTypeBuilder<ApplicationPipeline> builder)
    {
        builder.ToTable("application_pipeline");
        builder.HasKey(x => x.ApplicationUid);

        builder.Property(x => x.ApplicationUid).HasColumnName("application_uid").HasMaxLength(128);
        builder.Property(x => x.JobUid).HasColumnName("job_uid").HasMaxLength(128);
        builder.Property(x => x.CompanyUid).HasColumnName("company_uid").HasMaxLength(128);
        builder.Property(x => x.CandidateUid).HasColumnName("candidate_uid").HasMaxLength(128);
        builder.Property(x => x.Stage).HasColumnName("stage").HasMaxLength(32);
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(x => x.ChangedBy).HasColumnName("changed_by").HasMaxLength(128);
        builder.Property(x => x.ChangedAt).HasColumnName("changed_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.Version).HasColumnName("xmin").IsRowVersion();

        builder.HasIndex(x => new { x.JobUid, x.Stage }).HasDatabaseName("ix_pipeline_job_stage");
        builder.HasIndex(x => new { x.CompanyUid, x.ChangedAt }).HasDatabaseName("ix_pipeline_company_changed");
        builder.HasIndex(x => x.CandidateUid).HasDatabaseName("ix_pipeline_candidate");

        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class InterviewConfiguration : IEntityTypeConfiguration<Interview>
{
    public void Configure(EntityTypeBuilder<Interview> builder)
    {
        builder.ToTable("interview");
        builder.HasKey(x => x.Uid);

        builder.Property(x => x.Uid).HasColumnName("uid").HasMaxLength(128);
        builder.Property(x => x.ApplicationUid).HasColumnName("application_uid").HasMaxLength(128);
        builder.Property(x => x.JobUid).HasColumnName("job_uid").HasMaxLength(128);
        builder.Property(x => x.CompanyUid).HasColumnName("company_uid").HasMaxLength(128);
        builder.Property(x => x.CandidateUid).HasColumnName("candidate_uid").HasMaxLength(128);
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(32);
        builder.Property(x => x.StartsAt).HasColumnName("starts_at");
        builder.Property(x => x.DurationMinutes).HasColumnName("duration_minutes");
        builder.Property(x => x.TimeZone).HasColumnName("timezone").HasMaxLength(64);
        builder.Property(x => x.VideoUrl).HasColumnName("video_url").HasMaxLength(1000);
        builder.Property(x => x.Location).HasColumnName("location").HasMaxLength(1000);
        builder.Property(x => x.CandidateMessage).HasColumnName("candidate_message").HasMaxLength(500);
        builder.Property(x => x.InternalNote).HasColumnName("internal_note").HasMaxLength(2000);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
        builder.Property(x => x.ConfirmationStatus).HasColumnName("confirmation_status").HasMaxLength(32);
        builder.Property(x => x.Result).HasColumnName("result").HasMaxLength(32);
        builder.Property(x => x.InterviewerUid).HasColumnName("interviewer_uid").HasMaxLength(128);
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(128);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.OwnsMany(x => x.Participants, participants =>
        {
            participants.ToTable("interview_participant");
            participants.WithOwner().HasForeignKey("interview_uid");
            participants.Property<long>("id");
            participants.HasKey("id");
            participants.Property(p => p.Email).HasColumnName("email").HasMaxLength(320);
            participants.Property(p => p.Role).HasColumnName("role").HasMaxLength(32);
            participants.Property(p => p.Name).HasColumnName("name").HasMaxLength(256);
        });

        builder.HasIndex(x => new { x.JobUid, x.StartsAt }).HasDatabaseName("ix_interview_job_starts");
        builder.HasIndex(x => new { x.ApplicationUid, x.Status }).HasDatabaseName("ix_interview_application_status");
        builder.HasIndex(x => x.CandidateUid).HasDatabaseName("ix_interview_candidate");

        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.EndsAt);
    }
}

public sealed class ApplicationNoteConfiguration : IEntityTypeConfiguration<ApplicationNote>
{
    public void Configure(EntityTypeBuilder<ApplicationNote> builder)
    {
        builder.ToTable("application_note");
        builder.HasKey(x => x.ApplicationUid);

        builder.Property(x => x.ApplicationUid).HasColumnName("application_uid").HasMaxLength(128);
        builder.Property(x => x.JobUid).HasColumnName("job_uid").HasMaxLength(128);
        builder.Property(x => x.CompanyUid).HasColumnName("company_uid").HasMaxLength(128);
        builder.Property(x => x.CandidateUid).HasColumnName("candidate_uid").HasMaxLength(128);
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(2000);
        builder.Property(x => x.AuthorUid).HasColumnName("author_uid").HasMaxLength(128);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.JobUid).HasDatabaseName("ix_note_job");
    }
}

public sealed class ActivityEntryConfiguration : IEntityTypeConfiguration<ActivityEntry>
{
    public void Configure(EntityTypeBuilder<ActivityEntry> builder)
    {
        builder.ToTable("activity_log");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(x => x.ApplicationUid).HasColumnName("application_uid").HasMaxLength(128);
        builder.Property(x => x.JobUid).HasColumnName("job_uid").HasMaxLength(128);
        builder.Property(x => x.CompanyUid).HasColumnName("company_uid").HasMaxLength(128);
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(48);
        builder.Property(x => x.ActorUid).HasColumnName("actor_uid").HasMaxLength(128);
        builder.Property(x => x.ActorName).HasColumnName("actor_name").HasMaxLength(256);
        builder.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(x => new { x.ApplicationUid, x.CreatedAt }).HasDatabaseName("ix_activity_application");
        builder.HasIndex(x => new { x.CompanyUid, x.CreatedAt }).HasDatabaseName("ix_activity_company");
    }
}

public sealed class SavedFilterConfiguration : IEntityTypeConfiguration<SavedFilter>
{
    public void Configure(EntityTypeBuilder<SavedFilter> builder)
    {
        builder.ToTable("saved_filter");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyUid).HasColumnName("company_uid").HasMaxLength(128);
        builder.Property(x => x.UserUid).HasColumnName("user_uid").HasMaxLength(128);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120);
        builder.Property(x => x.Query).HasColumnName("query").HasColumnType("jsonb");
        builder.Property(x => x.IsDefault).HasColumnName("is_default");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.CompanyUid, x.UserUid }).HasDatabaseName("ix_saved_filter_owner");
    }
}
