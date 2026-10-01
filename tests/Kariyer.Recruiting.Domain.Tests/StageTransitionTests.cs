using Kariyer.Recruiting.Domain.Pipeline;

namespace Kariyer.Recruiting.Domain.Tests;

public class StageTransitionTests
{
    [Theory]
    [InlineData(ApplicationStage.New, ApplicationStage.Reviewing)]
    [InlineData(ApplicationStage.New, ApplicationStage.Rejected)]
    [InlineData(ApplicationStage.Reviewing, ApplicationStage.Interview)]
    [InlineData(ApplicationStage.Contact, ApplicationStage.Interview)]
    [InlineData(ApplicationStage.Interview, ApplicationStage.Offer)]
    [InlineData(ApplicationStage.Offer, ApplicationStage.Hired)]
    [InlineData(ApplicationStage.Hold, ApplicationStage.Interview)]
    public void Allows_the_documented_moves(string from, string to) =>
        StageTransitions.Assert(from, to);

    [Theory]
    [InlineData(ApplicationStage.New, ApplicationStage.Interview)]   // must be reviewed first
    [InlineData(ApplicationStage.New, ApplicationStage.Hired)]
    [InlineData(ApplicationStage.Reviewing, ApplicationStage.Offer)] // no offer without an interview
    [InlineData(ApplicationStage.Contact, ApplicationStage.Hired)]
    public void Rejects_skipping_stages(string from, string to) =>
        Assert.Throws<InvalidStageTransitionException>(() => StageTransitions.Assert(from, to));

    [Fact]
    public void WITHDRAWN_cannot_be_left()
    {
        Assert.Empty(StageTransitions.From(ApplicationStage.Withdrawn));

        foreach (string target in ApplicationStage.All)
        {
            Assert.Throws<InvalidStageTransitionException>(() => StageTransitions.Assert(ApplicationStage.Withdrawn, target));
        }
    }

    [Theory]
    [InlineData(ApplicationStage.Hired, ApplicationStage.Offer)]
    [InlineData(ApplicationStage.Hired, ApplicationStage.Hold)]
    [InlineData(ApplicationStage.Hired, ApplicationStage.Rejected)]
    [InlineData(ApplicationStage.Rejected, ApplicationStage.Hold)]
    public void A_final_decision_can_be_corrected(string from, string to) =>
        StageTransitions.Assert(from, to);

    [Theory]
    [InlineData(ApplicationStage.Hired, ApplicationStage.New)]
    [InlineData(ApplicationStage.Hired, ApplicationStage.Interview)]
    [InlineData(ApplicationStage.Rejected, ApplicationStage.Interview)] // reopens through HOLD, never straight back in
    [InlineData(ApplicationStage.Rejected, ApplicationStage.Offer)]
    [InlineData(ApplicationStage.Rejected, ApplicationStage.Hired)]
    public void A_correction_is_one_narrow_step(string from, string to) =>
        Assert.Throws<InvalidStageTransitionException>(() => StageTransitions.Assert(from, to));

    [Theory]
    [InlineData(ApplicationStage.Hired)]
    [InlineData(ApplicationStage.Rejected)]
    public void A_correctable_final_is_still_closed(string stage) =>
        Assert.False(ApplicationStage.IsOpen(stage));

    [Fact]
    public void A_no_op_move_is_not_a_transition()
    {
        // Re-applying the current stage is rejected rather than treated as success: a
        // double-submitted button would otherwise write two STAGE_CHANGED entries that claim
        // the same decision was taken twice.
        foreach (string stage in ApplicationStage.All)
        {
            Assert.Throws<InvalidStageTransitionException>(() => StageTransitions.Assert(stage, stage));
        }
    }

    [Fact]
    public void No_company_action_can_reach_WITHDRAWN()
    {
        // Withdrawing is the candidate's move, made in the candidate-facing service. If this
        // ever becomes reachable from here, a recruiter can make it look as though a candidate
        // walked away from a process the company ended.
        foreach (string stage in ApplicationStage.All)
        {
            Assert.DoesNotContain(ApplicationStage.Withdrawn, StageTransitions.From(stage));
        }
    }

    [Fact]
    public void Every_stage_has_a_label_and_a_transition_entry()
    {
        foreach (string stage in ApplicationStage.All)
        {
            Assert.True(ApplicationStage.IsValid(stage));
            Assert.NotEqual(stage, ApplicationStage.Label(stage));
            Assert.NotNull(StageTransitions.From(stage));
        }
    }

    [Theory]
    [InlineData("interview", ApplicationStage.Interview)]
    [InlineData("  Offer ", ApplicationStage.Offer)]
    public void Parses_wire_values_case_insensitively(string input, string expected)
    {
        Assert.True(ApplicationStage.TryParse(input, out string stage));
        Assert.Equal(expected, stage);
    }

    [Fact]
    public void Open_means_not_terminal()
    {
        Assert.True(ApplicationStage.IsOpen(ApplicationStage.New));
        Assert.True(ApplicationStage.IsOpen(ApplicationStage.Hold));
        Assert.False(ApplicationStage.IsOpen(ApplicationStage.Hired));
        Assert.False(ApplicationStage.IsOpen(ApplicationStage.Withdrawn));
    }
}

public class InvitationStageWalkTests
{
    /// <summary>
    /// The invite handler walks NEW → REVIEWING → INTERVIEW rather than refusing. This pins the
    /// two facts that walk depends on.
    /// </summary>
    [Fact]
    public void New_cannot_reach_INTERVIEW_directly_but_can_through_REVIEWING()
    {
        Assert.False(StageTransitions.IsAllowed(ApplicationStage.New, ApplicationStage.Interview));
        Assert.True(StageTransitions.IsAllowed(ApplicationStage.New, ApplicationStage.Reviewing));
        Assert.True(StageTransitions.IsAllowed(ApplicationStage.Reviewing, ApplicationStage.Interview));
    }

    [Theory]
    [InlineData(ApplicationStage.Contact)]
    [InlineData(ApplicationStage.Reviewing)]
    [InlineData(ApplicationStage.Hold)]
    public void Other_open_stages_reach_INTERVIEW_directly(string stage) =>
        Assert.True(StageTransitions.IsAllowed(stage, ApplicationStage.Interview));
}
