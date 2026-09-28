using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Validation;

namespace Kariyer.Recruiting.Domain.Tests;

public class InterviewRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 10, 0, 0, TimeSpan.FromHours(3));

    private static InterviewDraft Valid(Action<InterviewDraftBuilder>? tweak = null)
    {
        InterviewDraftBuilder builder = new();
        tweak?.Invoke(builder);
        return builder.Build(Now);
    }

    [Fact]
    public void Accepts_a_complete_video_invitation() =>
        Assert.True(InterviewRules.Validate(Valid(), Now).IsValid);

    [Fact]
    public void Requires_a_meeting_link_for_video()
    {
        ValidationResult result = InterviewRules.Validate(Valid(b => b.VideoUrl = null), Now);

        Assert.False(result.IsValid);
        Assert.Contains("videoUrl", result.Errors.Keys);
    }

    [Fact]
    public void Requires_an_address_for_in_person()
    {
        ValidationResult result = InterviewRules.Validate(
            Valid(b => { b.Type = InterviewType.InPerson; b.VideoUrl = null; b.Location = null; }), Now);

        Assert.False(result.IsValid);
        Assert.Contains("location", result.Errors.Keys);
    }

    [Fact]
    public void Phone_needs_neither_link_nor_address() =>
        Assert.True(InterviewRules.Validate(
            Valid(b => { b.Type = InterviewType.Phone; b.VideoUrl = null; b.Location = null; }), Now).IsValid);

    [Theory]
    [InlineData(10)]
    [InlineData(181)]
    public void Rejects_durations_outside_the_band(int minutes) =>
        Assert.Contains("durationMinutes",
            InterviewRules.Validate(Valid(b => b.DurationMinutes = minutes), Now).Errors.Keys);

    [Fact]
    public void Rejects_a_start_time_in_the_past() =>
        Assert.Contains("startsAt",
            InterviewRules.Validate(Valid(b => b.StartsAt = Now.AddHours(-2)), Now).Errors.Keys);

    [Fact]
    public void Tolerates_a_start_time_that_just_slipped_past()
    {
        // The recruiter filled the form for 10:00 and submitted at 10:00:30.
        Assert.True(InterviewRules.Validate(Valid(b => b.StartsAt = Now.AddSeconds(-30)), Now).IsValid);
    }

    [Fact]
    public void Rejects_an_unknown_time_zone() =>
        Assert.Contains("timezone",
            InterviewRules.Validate(Valid(b => b.TimeZone = "Mars/Olympus"), Now).Errors.Keys);

    [Fact]
    public void Rejects_a_duplicate_participant() =>
        Assert.Contains("participants", InterviewRules.Validate(
            Valid(b => b.Participants =
            [
                new InterviewParticipantDraft("selin@example.com", InterviewParticipantRole.Recruiter),
                new InterviewParticipantDraft("SELIN@example.com", InterviewParticipantRole.Interviewer),
            ]), Now).Errors.Keys);

    [Fact]
    public void Rejects_a_malformed_participant_email() =>
        Assert.Contains("participants", InterviewRules.Validate(
            Valid(b => b.Participants = [new InterviewParticipantDraft("selin@", InterviewParticipantRole.Recruiter)]),
            Now).Errors.Keys);

    [Fact]
    public void Rejects_an_over_long_candidate_message() =>
        Assert.Contains("candidateMessage", InterviewRules.Validate(
            Valid(b => b.CandidateMessage = new string('a', InterviewRules.MaxCandidateMessageLength + 1)),
            Now).Errors.Keys);

    [Fact]
    public void Requires_an_interviewer() =>
        Assert.Contains("interviewerUid",
            InterviewRules.Validate(Valid(b => b.InterviewerUid = " "), Now).Errors.Keys);

    [Fact]
    public void Reports_every_problem_at_once()
    {
        ValidationResult result = InterviewRules.Validate(
            Valid(b => { b.DurationMinutes = 5; b.VideoUrl = null; b.TimeZone = "Mars/Olympus"; }), Now);

        Assert.Equal(3, result.Errors.Count);
    }

    private sealed class InterviewDraftBuilder
    {
        public string Type { get; set; } = InterviewType.Video;

        public DateTimeOffset? StartsAt { get; set; }

        public int DurationMinutes { get; set; } = 45;

        public string TimeZone { get; set; } = "Europe/Istanbul";

        public string? VideoUrl { get; set; } = "https://meet.google.com/kz-abc-def";

        public string? Location { get; set; }

        public string? CandidateMessage { get; set; } = "Görüşmemiz için davetimizi iletiyoruz.";

        public string InterviewerUid { get; set; } = "mock-user-1";

        public IReadOnlyList<InterviewParticipantDraft> Participants { get; set; } = [];

        public InterviewDraft Build(DateTimeOffset now) => new()
        {
            Type = Type,
            StartsAt = StartsAt ?? now.AddDays(1),
            DurationMinutes = DurationMinutes,
            TimeZone = TimeZone,
            VideoUrl = VideoUrl,
            Location = Location,
            CandidateMessage = CandidateMessage,
            InterviewerUid = InterviewerUid,
            Participants = Participants,
        };
    }
}
