using System;
using System.Collections.Generic;

namespace ZeroGame.Networking
{
    public interface INetCostSink
    {
        bool IsReady { get; }
        void Track(string eventName, Dictionary<string, object> properties);
    }

    // Called only on Unity's main thread. Retries never call this reporter.
    public sealed class NetCostReporter
    {
        private readonly HashSet<string> finishedApis = new HashSet<string>(StringComparer.Ordinal);
        private readonly Action<string> log;
        public INetCostSink Sink { get; set; }
        public NetCostReporter(Action<string> logger) { log = logger ?? (_ => { }); }

        public void Report(BuglyResponse response, bool debug, bool logSuccessfulReports = true)
        {
            if (response.ReportAttempted) return;
            response.ReportAttempted = true;
            bool first = !finishedApis.Contains(response.EventName);
            bool reported = false;
            string trackError = string.Empty;
            try
            {
                if (Sink != null && Sink.IsReady)
                {
                    Sink.Track("net_cost", new Dictionary<string, object> {
                        { "api", response.EventName }, { "duration_ms", Math.Max(0L, response.DurationMs) },
                        { "success", response.Success }, { "first", first },
                        { "failure_reason", response.FailureReason ?? string.Empty },
                        { "url", AnalyticsUrl(response.Url) }, { "attempts", response.Attempts },
                        { "http_status", response.ResponseCode }, { "transport", response.Transport.ToString() },
                        { "attempt_duration_ms", Math.Max(0L, response.LastAttemptDurationMs) }
                    });
                    reported = true; // Accepted by local SDK call, not backend delivery confirmation.
                    finishedApis.Add(response.EventName);
                }
            }
            catch (Exception error) { trackError = error.GetType().Name; }
            if (!logSuccessfulReports && response.Success && reported && trackError.Length == 0) return;
            // Do not log response bodies, query strings, tokens or SDK exception messages.
            log("[net_cost] debug=" + Lower(debug) + " api=" + response.EventName +
                " duration_ms=" + Math.Max(0L, response.DurationMs) + " success=" + Lower(response.Success) +
                " first=" + Lower(first) + " reported=" + Lower(reported) + " trackError=" + trackError);
        }

        private static string Lower(bool value) { return value ? "true" : "false"; }
        public static string AnalyticsUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri)) return "invalid_url";
            return new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.AbsoluteUri;
        }
    }
}
