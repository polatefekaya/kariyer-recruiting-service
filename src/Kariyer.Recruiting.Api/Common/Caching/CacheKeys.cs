namespace Kariyer.Recruiting.Api.Common.Caching;

public static class CacheKeys
{
    public static string JobScope(string prefix, string jobUid) => $"{prefix}:job:{jobUid}";

    public static string ApplicationList(string prefix, string jobUid, string queryHash) =>
        $"{JobScope(prefix, jobUid)}:apps:{queryHash}";

    public static string Stats(string prefix, string jobUid) => $"{JobScope(prefix, jobUid)}:stats";

    public static string Interviews(string prefix, string jobUid) => $"{JobScope(prefix, jobUid)}:interviews";

    public static string CompanyMembers(string prefix, string companyUid) => $"{prefix}:company:{companyUid}:members";

    public static string CompanyName(string prefix, string companyUid) => $"{prefix}:company:{companyUid}:name";
}
