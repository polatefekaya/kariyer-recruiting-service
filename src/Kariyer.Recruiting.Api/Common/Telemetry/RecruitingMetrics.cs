using System.Diagnostics.Metrics;

namespace Kariyer.Recruiting.Api.Common.Telemetry;

public sealed class RecruitingMetrics : IDisposable
{
    public const string MeterName = "Kariyer.Recruiting";

    private readonly Meter _meter;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    private readonly Counter<long> _stageChanges;
    private readonly Counter<long> _interviews;
    private readonly Histogram<double> _listLatency;

    public RecruitingMetrics(IMeterFactory factory)
    {
        _meter = factory.Create(MeterName);
        _cacheHits = _meter.CreateCounter<long>("recruiting.cache.hits");
        _cacheMisses = _meter.CreateCounter<long>("recruiting.cache.misses");
        _stageChanges = _meter.CreateCounter<long>("recruiting.stage.changes");
        _interviews = _meter.CreateCounter<long>("recruiting.interviews");
        _listLatency = _meter.CreateHistogram<double>("recruiting.application_list.duration", "ms");
    }

    public void CacheHit(string kind) => _cacheHits.Add(1, new KeyValuePair<string, object?>("kind", kind));

    public void CacheMiss(string kind) => _cacheMisses.Add(1, new KeyValuePair<string, object?>("kind", kind));

    public void StageChanged(string from, string to) => _stageChanges.Add(
        1, new KeyValuePair<string, object?>("from", from), new KeyValuePair<string, object?>("to", to));

    public void InterviewAction(string action) =>
        _interviews.Add(1, new KeyValuePair<string, object?>("action", action));

    public void ListCompleted(double milliseconds, bool cached) => _listLatency.Record(
        milliseconds, new KeyValuePair<string, object?>("cached", cached));

    public void Dispose() => _meter.Dispose();
}
