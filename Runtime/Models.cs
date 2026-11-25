using System;

namespace ZeroGame.Networking
{
    public enum HttpTransport { OkHttp, UrlConnection }
    public enum RequestResult { InProgress, Success, ConnectionError, ProtocolError, Canceled }

    [Serializable]
    public sealed class RequestOptions
    {
        public HttpTransport Transport = HttpTransport.OkHttp;
        public int TimeoutSeconds = 15;
        // Includes first send; use 1 for non-idempotent operations without a requestId.
        public int MaxAttempts = 3;
        public int RetryDelayMs = 250;
        public int MaxResponseBytes = 16 * 1024 * 1024;
        public bool ReportNetCost = true;
        // Post also dispatches independent empty-body probes to the configured text URLs.
        public bool TraceConfiguredUrlsOnPost = true;

        internal RequestOptions Snapshot()
        {
            if (!Enum.IsDefined(typeof(HttpTransport), Transport))
                throw new ArgumentOutOfRangeException("Transport", Transport, "[BuglyWebRequest] Transport 仅支持 OkHttp 或 UrlConnection。");
            if (TimeoutSeconds < 1)
                throw new ArgumentOutOfRangeException("TimeoutSeconds", TimeoutSeconds, "[BuglyWebRequest] 每次请求的超时秒数必须大于或等于 1。");
            if (MaxAttempts < 1 || MaxAttempts > 10)
                throw new ArgumentOutOfRangeException("MaxAttempts", MaxAttempts, "[BuglyWebRequest] 尝试次数必须在 1 到 10 之间，包含首次请求；1 表示不重试。");
            if (RetryDelayMs < 0)
                throw new ArgumentOutOfRangeException("RetryDelayMs", RetryDelayMs, "[BuglyWebRequest] 重试等待毫秒数不能小于 0。");
            if (MaxResponseBytes < 1)
                throw new ArgumentOutOfRangeException("MaxResponseBytes", MaxResponseBytes, "[BuglyWebRequest] 响应大小限制必须大于或等于 1 字节。");
            return (RequestOptions)MemberwiseClone();
        }
    }

    public sealed class BuglyResponse
    {
        public int Index { get; internal set; }
        public string EventName { get; internal set; }
        public string Url { get; internal set; }
        public byte[] Data { get; internal set; }
        public long ResponseCode { get; internal set; }
        public long DurationMs { get; internal set; }
        public long LastAttemptDurationMs { get; internal set; }
        public int Attempts { get; internal set; }
        public bool Success { get; internal set; }
        public string FailureReason { get; internal set; }
        public HttpTransport Transport { get; internal set; }
        public RequestResult Result { get; internal set; }
        internal bool ReportAttempted;
    }

    public sealed class BuglyDownloadHandler
    {
        public byte[] Data { get; internal set; }
        public string Text { get { return Data == null ? string.Empty : System.Text.Encoding.UTF8.GetString(Data); } }
    }
}
