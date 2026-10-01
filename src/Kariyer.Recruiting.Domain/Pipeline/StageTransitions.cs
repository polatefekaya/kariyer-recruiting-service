namespace Kariyer.Recruiting.Domain.Pipeline;

/// <summary>
/// Which stage moves are legal (technical document §4, <c>allowedTransitions</c>).
///
/// Enforced HERE rather than only in the UI because the frontend's job is to hide impossible
/// buttons, not to guarantee the pipeline. Two recruiters working the same candidate, a stale
/// tab, a retried request or a direct API call all produce moves the UI would never offer.
///
/// HIRED and REJECTED are final but correctable: a recruiter who marks the wrong person hired,
/// or rejects the wrong row, must be able to put it back. The way back is deliberately narrow —
/// one step to where the decision was made (OFFER) or to the parking space (HOLD) — so a
/// correction is a visible, logged move rather than a free walk that makes the funnel unreadable.
/// </summary>
public static class StageTransitions
{
    private static readonly Dictionary<string, string[]> Allowed = new(StringComparer.Ordinal)
    {
        [ApplicationStage.New] =
            [ApplicationStage.Reviewing, ApplicationStage.Contact, ApplicationStage.Rejected, ApplicationStage.Hold],
        [ApplicationStage.Reviewing] =
            [ApplicationStage.Contact, ApplicationStage.Interview, ApplicationStage.Rejected, ApplicationStage.Hold],
        [ApplicationStage.Contact] =
            [ApplicationStage.Interview, ApplicationStage.Rejected, ApplicationStage.Hold],
        [ApplicationStage.Interview] =
            [ApplicationStage.Offer, ApplicationStage.Rejected, ApplicationStage.Hold],
        [ApplicationStage.Offer] =
            [ApplicationStage.Hired, ApplicationStage.Rejected, ApplicationStage.Hold],

        // HOLD is the parking space, so it returns to any active stage — including INTERVIEW,
        // which is how a candidate parked mid-process resumes without losing their history.
        [ApplicationStage.Hold] =
        [
            ApplicationStage.New, ApplicationStage.Reviewing, ApplicationStage.Contact,
            ApplicationStage.Interview, ApplicationStage.Rejected
        ],

        // A mistaken hire goes back to the offer it came from, is parked, or is turned into the
        // rejection it should have been.
        [ApplicationStage.Hired] =
            [ApplicationStage.Offer, ApplicationStage.Hold, ApplicationStage.Rejected],

        // A mistaken rejection reopens into HOLD only. Which stage the candidate really belongs
        // in is a second, deliberate move from there — REJECTED does not remember where it came
        // from, and guessing would put people back into an interview nobody scheduled.
        [ApplicationStage.Rejected] = [ApplicationStage.Hold],

        // Only the candidate withdraws, and only the candidate-facing service does that; from
        // this side it is a dead end in both directions.
        [ApplicationStage.Withdrawn] = [],
    };

    public static IReadOnlyList<string> From(string stage) =>
        Allowed.TryGetValue(stage, out string[]? next) ? next : [];

    public static bool IsAllowed(string from, string to) =>
        Allowed.TryGetValue(from, out string[]? next) && Array.IndexOf(next, to) >= 0;

    /// <summary>
    /// Throws <see cref="InvalidStageTransitionException"/> unless the move is legal. The caller
    /// turns that into the 409 + <c>INVALID_STATUS_TRANSITION</c> body the document specifies.
    /// </summary>
    public static void Assert(string from, string to)
    {
        if (!ApplicationStage.IsValid(from))
        {
            throw new ArgumentException($"Unknown stage: {from}", nameof(from));
        }

        if (!ApplicationStage.IsValid(to))
        {
            throw new ArgumentException($"Unknown stage: {to}", nameof(to));
        }

        // A no-op move is allowed to reach here and is rejected like any other illegal move:
        // reporting "already in this stage" as success would make a double-clicked button look
        // like two distinct decisions in the activity log.
        if (!IsAllowed(from, to))
        {
            throw new InvalidStageTransitionException(from, to);
        }
    }
}

/// <summary>A move the pipeline does not permit. Maps to HTTP 409.</summary>
public sealed class InvalidStageTransitionException(string from, string to)
    : InvalidOperationException($"Invalid stage transition: {from} -> {to}")
{
    public string From { get; } = from;

    public string To { get; } = to;
}
