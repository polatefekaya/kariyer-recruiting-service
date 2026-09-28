namespace Kariyer.Recruiting.Domain.Notes;

public sealed class ApplicationNote
{
    private ApplicationNote()
    {
    }

    public string ApplicationUid { get; private set; } = string.Empty;

    public string JobUid { get; private set; } = string.Empty;

    public string CompanyUid { get; private set; } = string.Empty;

    public string CandidateUid { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public string AuthorUid { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ApplicationNote Create(
        string applicationUid,
        string jobUid,
        string companyUid,
        string candidateUid,
        string body,
        string authorUid,
        DateTimeOffset now) => new()
        {
            ApplicationUid = applicationUid,
            JobUid = jobUid,
            CompanyUid = companyUid,
            CandidateUid = candidateUid,
            Body = body,
            AuthorUid = authorUid,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public void Edit(string body, string authorUid, DateTimeOffset now)
    {
        Body = body;
        AuthorUid = authorUid;
        UpdatedAt = now;
    }
}
