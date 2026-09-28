namespace Kariyer.Recruiting.Domain.Pipeline;

/// <summary>
/// The stage an application sits at in the hiring pipeline (technical document §4).
///
/// Stored and transported as the SCREAMING_SNAKE string, not as an integer: the same vocabulary
/// is written to <c>public.job_application.application_status</c>, read by the Node application
/// and the candidate-facing site, and sent to the frontend. An integer would make the database
/// unreadable to every one of those readers and would tie the wire format to declaration order.
///
/// WITHDRAWN is not in the technical document. It exists anyway because the product has always
/// let a candidate withdraw (<c>PUT /job_applications/applications/:uid/withdraw</c>), and a
/// stage set the doc does not model is still a row the database can contain. It is terminal and
/// the company can never set it — see <see cref="StageTransitions"/>.
/// </summary>
public static class ApplicationStage
{
    public const string New = "NEW";
    public const string Reviewing = "REVIEWING";
    public const string Contact = "CONTACT";
    public const string Interview = "INTERVIEW";
    public const string Offer = "OFFER";
    public const string Hired = "HIRED";
    public const string Hold = "HOLD";
    public const string Rejected = "REJECTED";

    /// <summary>Candidate-initiated and terminal; never a company action.</summary>
    public const string Withdrawn = "WITHDRAWN";

    /// <summary>Every stage, in pipeline order — the order the UI shows chips in.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        New, Reviewing, Contact, Interview, Offer, Hired, Hold, Rejected, Withdrawn
    ];

    /// <summary>Turkish UI labels (technical document §4, status sözlüğü).</summary>
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        [New] = "Yeni",
        [Reviewing] = "İnceleniyor",
        [Contact] = "İletişim",
        [Interview] = "Mülakat",
        [Offer] = "Teklif",
        [Hired] = "İşe alındı",
        [Hold] = "Karar verilmedi",
        [Rejected] = "Reddedildi",
        [Withdrawn] = "Geri çekildi",
    };

    /// <summary>Stages nothing can leave.</summary>
    private static readonly HashSet<string> Terminal = new(StringComparer.Ordinal)
    {
        Hired, Rejected, Withdrawn
    };

    /// <summary>
    /// Stages that count as "still in the running". Used for the open/closed split the lists
    /// show and for the "aktif başvuru" rule — a candidate may not hold two open applications
    /// to the same posting.
    /// </summary>
    public static bool IsOpen(string stage) => !Terminal.Contains(stage);

    public static bool IsTerminal(string stage) => Terminal.Contains(stage);

    public static bool IsValid(string? stage) => stage is not null && Labels.ContainsKey(stage);

    public static string Label(string stage) =>
        Labels.TryGetValue(stage, out string? label) ? label : stage;

    /// <summary>
    /// Normalises a stage that arrived from the wire. Case is folded with the invariant rules on
    /// purpose: these are ASCII protocol constants, and folding them with the Turkish rules
    /// would turn "INTERVIEW" into "ınterview".
    /// </summary>
    public static bool TryParse(string? value, out string stage)
    {
        stage = value?.Trim().ToUpperInvariant() ?? string.Empty;
        return Labels.ContainsKey(stage);
    }
}
