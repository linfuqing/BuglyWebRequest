using System;
using UnityEngine.Serialization;

namespace ZeroGame.Networking
{
    public enum HttpTransport { OkHttp, UrlConnection }
    public enum RequestResult { InProgress, Success, ConnectionError, ProtocolError, Canceled }

    [Serializable]
    public sealed class RequestOptions
    {
        [FormerlySerializedAs("transport")]
        public HttpTransport Transport = HttpTransport.OkHttp;
        [FormerlySerializedAs("timeoutSeconds")]
        public int TimeoutSeconds = 15;
        // Includes first send; use 1 for non-idempotent operations without a requestId.
        [FormerlySerializedAs("maxAttempts")]
        public int MaxAttempts = 3;
        [FormerlySerializedAs("retryDelayMs")]
        public int RetryDelayMs = 250;
        [FormerlySerializedAs("maxResponseBytes")]
        public int MaxResponseBytes = 16 * 1024 * 1024;
        [FormerlySerializedAs("reportNetCost")]
        public bool ReportNetCost = true;
        // Source compatibility; Unity serializes the PascalCase fields above.
        [Obsolete("Use Transport instead.")]
        public HttpTransport transport { get { return Transport; } set { Transport = value; } }
        [Obsolete("Use TimeoutSeconds instead.")]
        public int timeoutSeconds { get { return TimeoutSeconds; } set { TimeoutSeconds = value; } }
        [Obsolete("Use MaxAttempts instead.")]
        public int maxAttempts { get { return MaxAttempts; } set { MaxAttempts = value; } }
        [Obsolete("Use RetryDelayMs instead.")]
        public int retryDelayMs { get { return RetryDelayMs; } set { RetryDelayMs = value; } }
        [Obsolete("Use MaxResponseBytes instead.")]
        public int maxResponseBytes { get { return MaxResponseBytes; } set { MaxResponseBytes = value; } }
        [Obsolete("Use ReportNetCost instead.")]
        public bool reportNetCost { get { return ReportNetCost; } set { ReportNetCost = value; } }

        internal RequestOptions Snapshot()
        {
            if (TimeoutSeconds < 1 || MaxAttempts < 1 || MaxAttempts > 10 || RetryDelayMs < 0 || MaxResponseBytes < 1)
                throw new ArgumentOutOfRangeException("Invalid HTTP options");
            return (RequestOptions)MemberwiseClone();
        }
    }

    public sealed class BuglyResponse
    {
        public int Index { get; internal set; }
        public string Api { get; internal set; }
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
        [Obsolete("Use Data instead.")]
        public byte[] data { get { return Data; } internal set { Data = value; } }
        [Obsolete("Use Text instead.")]
        public string text { get { return Text; } }
    }
}
