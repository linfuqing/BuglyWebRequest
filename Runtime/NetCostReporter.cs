using System;
using System.Collections.Generic;

namespace ZeroGame.Networking
{
    public interface INetCostSink
    {
        bool IsReady { get; }
        void Track(string eventName, Dictionary<string, object> properties);
    }

    // Unity main thread only. Each final URL result is accepted once, independently of HTTP retries.
    public sealed class NetCostReporter
    {
        public const int PendingCapacity = 256;
        private struct PendingReport
        {
            internal BuglyResponse Response;
            internal bool Debug, LogSuccessfulReports;
        }
        private readonly Queue<PendingReport> pending = new Queue<PendingReport>();
        private readonly HashSet<string> finishedApis = new HashSet<string>(StringComparer.Ordinal);
        private readonly Action<string> log;
        private long loggedDrops;
        private bool buffering = true;
        public INetCostSink Sink { get; set; }
        public int PendingCount { get { return pending.Count; } }
        public long DroppedCount { get; private set; }
        public NetCostReporter(Action<string> logger) { log = logger ?? (_ => { }); }

        public void Report(BuglyResponse response, bool debug, bool logSuccessfulReports = true)
        {
            if (response.ReportAttempted) return;
            response.ReportAttempted = true;
            if (!buffering) { DroppedCount++; return; }
            if (pending.Count != 0 || !SinkReady())
            {
                // Copy only bounded metadata; never retain a response body, credentials or query string.
                var copy = new BuglyResponse {
                    EventName = Limit(response.EventName, 256), Url = Limit(AnalyticsUrl(response.Url), 2048),
                    FailureReason = Limit(response.FailureReason, 2048), DurationMs = response.DurationMs,
                    LastAttemptDurationMs = response.LastAttemptDurationMs, Success = response.Success,
                    Attempts = response.Attempts, ResponseCode = response.ResponseCode, Transport = response.Transport
                };
                if (pending.Count == PendingCapacity) { pending.Dequeue(); DroppedCount++; }
                pending.Enqueue(new PendingReport { Response = copy, Debug = debug, LogSuccessfulReports = logSuccessfulReports });
                if (logSuccessfulReports || !response.Success) WriteLog(copy, debug, !finishedApis.Contains(copy.EventName), false, true, "");
                return;
            }
            Deliver(response, debug, logSuccessfulReports);
        }

        // Called by the persistent request runner even when every HTTP request has completed.
        public void FlushPending(int maxReports = 8)
        {
            for (int i = 0; i < maxReports && pending.Count != 0; i++)
            {
                if (!SinkReady()) break;
                var item = pending.Dequeue();
                Deliver(item.Response, item.Debug, item.LogSuccessfulReports);
            }
        }

        private bool SinkReady()
        {
            try { return Sink != null && Sink.IsReady; }
            catch { return false; } // Analytics readiness must never block a business request.
        }

        private void Deliver(BuglyResponse response, bool debug, bool logSuccessfulReports)
        {
            bool first = !finishedApis.Contains(response.EventName);
            bool reported = false;
            string trackError = string.Empty;
            try
            {
                Sink.Track("net_cost", new Dictionary<string, object> {
                    { "api", response.EventName }, { "duration_ms", Math.Max(0L, response.DurationMs) },
                    { "success", response.Success }, { "first", first },
                    { "failure_reason", response.FailureReason ?? string.Empty },
                    { "url", AnalyticsUrl(response.Url) }, { "attempts", response.Attempts },
                    { "http_status", response.ResponseCode }, { "transport", response.Transport.ToString() },
                    { "attempt_duration_ms", Math.Max(0L, response.LastAttemptDurationMs) }
                });
                reported = true; // Local Track accepted it, not backend receipt.
                finishedApis.Add(response.EventName);
            }
            catch (Exception error) { trackError = error.GetType().Name; }
            // A throwing Track may already have accepted the event; do not resend and risk duplicates.
            if (!logSuccessfulReports && response.Success && reported && trackError.Length == 0) return;
            WriteLog(response, debug, first, reported, false, trackError);
        }

        private void WriteLog(BuglyResponse response, bool debug, bool first, bool reported, bool deferred, string trackError)
        {
            log("[net_cost] debug=" + Lower(debug) + " api=" + response.EventName +
                " duration_ms=" + Math.Max(0L, response.DurationMs) + " success=" + Lower(response.Success) +
                " first=" + Lower(first) +
                " reported=" + Lower(reported) + " deferred=" + Lower(deferred) + " trackError=" + trackError);
        }

        internal void LogDropped()
        {
            if (DroppedCount == loggedDrops) return;
            log("[net_cost][buffer] dropped=" + (DroppedCount - loggedDrops) + " total_dropped=" + DroppedCount +
                " pending=" + pending.Count + (buffering ? "; 暂存上限=" + PendingCapacity + "，已丢弃最旧记录。" : "; 数数等待已超时，后续埋点不再暂存。") + " 网络请求不受影响。");
            loggedDrops = DroppedCount;
        }

        internal void DiscardPending(string reason)
        {
            if (pending.Count == 0) return;
            DroppedCount += pending.Count;
            pending.Clear();
            log("[net_cost][buffer] " + reason + "; total_dropped=" + DroppedCount);
            loggedDrops = DroppedCount;
        }

        internal void StopBuffering(string reason)
        {
            buffering = false;
            DiscardPending(reason);
        }

        internal void ResumeBuffering() { buffering = true; }

        internal void Reset()
        {
            Sink = null; pending.Clear(); finishedApis.Clear(); DroppedCount = 0; loggedDrops = 0; buffering = true;
        }

        private static string Limit(string value, int length) { return string.IsNullOrEmpty(value) ? "" : value.Length <= length ? value : value.Substring(0, length); }
        private static string Lower(bool value) { return value ? "true" : "false"; }
        public static string AnalyticsUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri)) return "invalid_url";
            return new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.AbsoluteUri;
        }
    }
}
