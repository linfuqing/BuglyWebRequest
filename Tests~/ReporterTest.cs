using System;
using System.Collections.Generic;
using ZeroGame.Networking;

public static class ReporterTest
{
    private sealed class Sink : INetCostSink
    {
        public bool ready = true, fail;
        public int calls;
        public Dictionary<string, object> last;
        public bool IsReady { get { return ready; } }
        public void Track(string name, Dictionary<string, object> properties)
        {
            Check(name == "net_cost", "wrong event"); calls++;
            if (fail) throw new InvalidOperationException("secret payload must not leak");
            last = properties;
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static BuglyResponse Response(string api, bool success)
    {
        return new BuglyResponse { Api = api, Url = "https://user:password@example.com/api?token=secret#fragment",
            Success = success, FailureReason = success ? "" : "http_503", DurationMs = 100, Attempts = 3 };
    }
    public static void Main()
    {
        var logs = new List<string>();
        var sink = new Sink(); var reporter = new NetCostReporter(logs.Add) { Sink = sink };
        var first = Response("query_role", true); reporter.Report(first, false); reporter.Report(first, false);
        Check(sink.calls == 1 && logs.Count == 1, "duplicate terminal event");
        Check((bool)sink.last["first"] && (long)sink.last["duration_ms"] == 100 && (int)sink.last["attempts"] == 3, "bad first/timing/retry fields");
        Check((string)sink.last["url"] == "https://example.com/api", "URL credentials/query were not removed");
        reporter.Report(Response("query_role", false), false);
        Check(!(bool)sink.last["first"] && !(bool)sink.last["success"], "final HTTP failure not counted");
        sink.fail = true; reporter.Report(Response("new_api", true), true);
        Check(logs[logs.Count - 1].Contains("reported=false") && !logs[logs.Count - 1].Contains("secret"), "track failure leaked");
        sink.fail = false; reporter.Report(Response("new_api", true), false);
        Check((bool)sink.last["first"], "failed track consumed first");
        sink.ready = false; int before = sink.calls;
        reporter.Report(Response("not_ready", false), false);
        Check(sink.calls == before && logs[logs.Count - 1].Contains("reported=false"), "unready SDK called");
        sink.ready = true; var negative = Response("not_ready", true); negative.DurationMs = -10;
        reporter.Report(negative, false);
        Check((bool)sink.last["first"] && (long)sink.last["duration_ms"] == 0, "unready first or duration normalization");
        Console.WriteLine("PASS reporter: one event per result, first semantics, failure semantics, URL redaction, track exception isolation, readiness, timing");
    }
}
