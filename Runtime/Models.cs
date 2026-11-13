using System;

namespace ZeroGame.Networking
{
    public enum HttpTransport { OkHttp, UrlConnection }
    public enum RequestResult { InProgress, Success, ConnectionError, ProtocolError, Canceled }

    [Serializable]
    public sealed class RequestOptions
    {
        public HttpTransport transport = HttpTransport.OkHttp;
        public int timeoutSeconds = 15;
        // Includes first send; use 1 for non-idempotent operations without a requestId.
        public int maxAttempts = 3;
        public int retryDelayMs = 250;
        public int maxResponseBytes = 16 * 1024 * 1024;
        public bool reportNetCost = true;
        internal RequestOptions Snapshot()
        {
            if (timeoutSeconds < 1 || maxAttempts < 1 || maxAttempts > 10 || retryDelayMs < 0 || maxResponseBytes < 1)
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
        public byte[] data { get; internal set; }
        public string text { get { return data == null ? string.Empty : System.Text.Encoding.UTF8.GetString(data); } }
    }
}
