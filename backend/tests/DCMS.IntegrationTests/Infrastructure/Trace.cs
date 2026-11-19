using System.Reflection;
using System.Text.Json;
using Xunit.Sdk;

namespace DCMS.IntegrationTests;

/// <summary>
/// Describes one integration test case for the report (Google Sheet 5.2): a short description, the pre-condition and the steps.
/// While the test runs, every HTTP exchange is recorded under the case key; the run writes them to a JSON file
/// (path in DCMS_IT_TRACE) so the report shows the real response, not a hand-written one.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CaseAttribute(string key, string description, string preCondition, params string[] steps) : BeforeAfterTestAttribute
{
    public string Key { get; } = key;
    public string Description { get; } = description;
    public string PreCondition { get; } = preCondition;
    public string[] Steps { get; } = steps;

    public override void Before(MethodInfo methodUnderTest) => Trace.Begin(this);
    public override void After(MethodInfo methodUnderTest) => Trace.End();
}

internal static class Trace
{
    private sealed record Exchange(string Method, string Path, int Status, string Body);
    private sealed class Entry(CaseAttribute info) { public CaseAttribute Info { get; } = info; public List<Exchange> Exchanges { get; } = []; }

    // All API tests share one collection, so they run one after another and a plain static is safe.
    private static Entry? current;
    private static readonly List<Entry> finished = [];

    public static void Begin(CaseAttribute info) => current = new Entry(info);
    public static void End() { if (current != null) finished.Add(current); current = null; }

    public static void Record(string method, string path, ApiResponse response) =>
        current?.Exchanges.Add(new(method, path, response.Status, response.Body.Length > 4000 ? response.Body[..4000] : response.Body));

    public static void Flush()
    {
        var path = Environment.GetEnvironmentVariable("DCMS_IT_TRACE");
        if (string.IsNullOrWhiteSpace(path)) return;
        File.WriteAllText(path, JsonSerializer.Serialize(finished.Select(x => new
        {
            x.Info.Key, x.Info.Description, x.Info.PreCondition, x.Info.Steps, x.Exchanges
        }), new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
}
