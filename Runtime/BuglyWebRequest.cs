using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;
using Debug = UnityEngine.Debug;

namespace ZeroGame.Networking
{
    public sealed class BuglyWebRequestOperation : CustomYieldInstruction
    {
        public BuglyWebRequest WebRequest { get; private set; }
        public bool IsDone { get { return WebRequest.IsDone; } }
        public override bool keepWaiting { get { return !IsDone; } }
        public event Action<BuglyWebRequestOperation> Completed;
        [Obsolete("Use WebRequest instead.")]
        public BuglyWebRequest webRequest { get { return WebRequest; } }
        [Obsolete("Use IsDone instead.")]
        public bool isDone { get { return IsDone; } }
        [Obsolete("Use Completed instead.")]
        public event Action<BuglyWebRequestOperation> completed { add { Completed += value; } remove { Completed -= value; } }
        internal BuglyWebRequestOperation(BuglyWebRequest owner) { WebRequest = owner; }
        internal void Complete()
        {
            var handlers = Completed;
            if (handlers == null) return;
            foreach (Action<BuglyWebRequestOperation> handler in handlers.GetInvocationList())
                try { handler(this); } catch (Exception error) { Debug.LogException(error); }
        }
    }

    public sealed class BuglyWebRequest : IDisposable
    {
        private readonly string api;
        private readonly string[] urls, headerNames, headerValues;
        private readonly byte[] body;
        private readonly BuglyResponse[] responses;
        private readonly ConcurrentQueue<BuglyResponse> incoming = new ConcurrentQueue<BuglyResponse>();
        private readonly List<UnityWebRequest> managedRequests = new List<UnityWebRequest>();
        private readonly Stopwatch[] clocks;
        private bool sent, disposed, aborted;
        private int count;
        private RequestOptions snapshot;
        private BuglyWebRequestOperation operation;
#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly List<AndroidJavaObject> jobs = new List<AndroidJavaObject>();
        private NativeCallback nativeCallback;
#endif
        public RequestOptions Options { get; private set; }
        public bool IsDone { get; private set; }
        public float Progress { get { return (float)count / responses.Length; } }
        public IReadOnlyList<BuglyResponse> Responses { get { return Array.AsReadOnly(responses); } }
        public event Action<BuglyResponse> UrlCompleted;
        public BuglyDownloadHandler DownloadHandler
        {
            get
            {
                RequireSingleUrl();
                return new BuglyDownloadHandler { Data = responses[0] == null ? null : responses[0].Data };
            }
        }
        public long ResponseCode { get { RequireSingleUrl(); return responses[0] == null ? 0 : responses[0].ResponseCode; } }
        public string Error
        {
            get { foreach (var response in responses) if (response != null && !response.Success) return response.FailureReason; return null; }
        }
        public RequestResult Result
        {
            get
            {
                if (!IsDone) return RequestResult.InProgress;
                foreach (var response in responses) if (!response.Success) return response.Result;
                return RequestResult.Success;
            }
        }
        public int Timeout { get { return Options.TimeoutSeconds; } set { if (sent) throw new InvalidOperationException("Already sent"); Options.TimeoutSeconds = value; } }

        [Obsolete("Use IsDone instead.")]
        public bool isDone { get { return IsDone; } }
        [Obsolete("Use Progress instead.")]
        public float progress { get { return Progress; } }
        [Obsolete("Use DownloadHandler instead.")]
        public BuglyDownloadHandler downloadHandler { get { return DownloadHandler; } }
        [Obsolete("Use ResponseCode instead.")]
        public long responseCode { get { return ResponseCode; } }
        [Obsolete("Use Error instead.")]
        public string error { get { return Error; } }
        [Obsolete("Use Result instead.")]
        public RequestResult result { get { return Result; } }
        [Obsolete("Use Timeout instead.")]
        public int timeout { get { return Timeout; } set { Timeout = value; } }

        private BuglyWebRequest(string api, WWWForm form, string[] urls)
        {
            if (string.IsNullOrWhiteSpace(api) || api.Length > 120 || api.IndexOfAny(new[] {'\n','\r',' '}) >= 0)
                throw new ArgumentException("Use a stable non-empty API name without whitespace", "api");
            if (form == null || urls == null || urls.Length == 0) throw new ArgumentException("Form and at least one URL required");
            this.api = api; this.urls = (string[])urls.Clone();
            foreach (string url in this.urls)
            {
                Uri parsed;
                if (!Uri.TryCreate(url, UriKind.Absolute, out parsed) || (parsed.Scheme != "http" && parsed.Scheme != "https"))
                    throw new ArgumentException("Only absolute HTTP(S) URLs are supported");
            }
            body = (byte[])form.data.Clone();
            var headers = form.headers;
            headerNames = new string[headers.Count]; headerValues = new string[headers.Count];
            int i = 0;
            foreach (var pair in headers) { headerNames[i] = pair.Key; headerValues[i++] = pair.Value; }
            responses = new BuglyResponse[urls.Length]; clocks = new Stopwatch[urls.Length]; Options = BuglySdkManager.CreateRequestOptions();
        }

        // Resolve configuration at request creation, on Unity's main thread.
        public static BuglyWebRequest Post(string api, WWWForm form)
        {
            return new BuglyWebRequest(api, form, BuglyUrlConfig.Load());
        }

        [Obsolete("URLs are now read from the text configuration. The urls argument is ignored; use Post(api, form).")]
        public static BuglyWebRequest Post(string api, WWWForm form, params string[] urls)
        {
            return Post(api, form);
        }

        // Unity main thread only. Requests are started once and complete independently.
        public BuglyWebRequestOperation SendWebRequest()
        {
            if (sent || disposed) throw new InvalidOperationException("Request already sent or disposed");
            snapshot = Options.Snapshot(); sent = true;
            operation = new BuglyWebRequestOperation(this);
            var runner = RequestRunner.Ensure(); runner.Add(this);
#if UNITY_ANDROID && !UNITY_EDITOR
            nativeCallback = new NativeCallback(this);
            for (int i = 0; i < urls.Length; i++)
            {
                try
                {
                    using (var bridge = new AndroidJavaClass("cn.zero3d.buglywebrequest.WireBridge"))
                        jobs.Add(bridge.CallStatic<AndroidJavaObject>("submit", i, urls[i], body, headerNames, headerValues,
                            (int)snapshot.Transport, snapshot.TimeoutSeconds, snapshot.MaxAttempts, snapshot.RetryDelayMs,
                            snapshot.MaxResponseBytes, BuglySdk.IsInitialized, nativeCallback));
                }
                catch (Exception exception) { Queue(Failure(i, "bridge_error:" + exception.GetType().Name)); }
            }
            BuglySdk.LogInfo("[BuglyWebRequest] Android " + snapshot.Transport + "; urls=" + urls.Length);
#else
            for (int i = 0; i < urls.Length; i++) runner.StartCoroutine(RunManaged(i));
            BuglySdk.LogInfo("[BuglyWebRequest] UnityWebRequest fallback (Editor/WebGL/desktop); urls=" + urls.Length);
#endif
            return operation;
        }

        private void RequireSingleUrl() { if (urls.Length != 1) throw new InvalidOperationException("Use Responses for multiple URLs"); }
        private BuglyResponse Failure(int index, string reason)
        {
            return new BuglyResponse { Index = index, Api = api, Url = urls[index], Data = new byte[0],
                FailureReason = reason, Success = false, Transport = snapshot.Transport,
                DurationMs = clocks[index] == null ? 0 : clocks[index].ElapsedMilliseconds,
                Result = reason == "canceled" ? RequestResult.Canceled : RequestResult.ConnectionError };
        }
        private void Queue(BuglyResponse response) { incoming.Enqueue(response); }

        internal void Pump()
        {
            BuglyResponse response;
            while (incoming.TryDequeue(out response))
            {
                if (response.Index < 0 || response.Index >= responses.Length || responses[response.Index] != null) continue;
                responses[response.Index] = response; count++;
                if (snapshot.ReportNetCost) BuglySdk.Reporter.Report(response, Debug.isDebugBuild);
                var handlers = UrlCompleted;
                if (handlers != null)
                    foreach (Action<BuglyResponse> handler in handlers.GetInvocationList())
                        try { handler(response); } catch (Exception error) { Debug.LogException(error); }
            }
            if (!IsDone && count == urls.Length)
            {
                IsDone = true;
#if UNITY_ANDROID && !UNITY_EDITOR
                foreach (var job in jobs) job.Dispose();
                jobs.Clear();
#endif
                operation.Complete();
            }
        }

        public void Abort()
        {
            if (!sent || IsDone || aborted) return;
            aborted = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (var job in jobs)
                try { job.Call("cancel"); } catch (Exception error) { Debug.LogWarning("[BuglyWebRequest] Cancel: " + error.GetType().Name); }
#endif
            foreach (var request in managedRequests) request.Abort();
            for (int i = 0; i < urls.Length; i++) if (responses[i] == null) Queue(Failure(i, "canceled"));
        }
        public void Dispose() { if (disposed) return; Abort(); disposed = true; }

#if UNITY_ANDROID && !UNITY_EDITOR
        [Preserve]
        private sealed class NativeCallback : AndroidJavaProxy
        {
            private readonly BuglyWebRequest owner;
            public NativeCallback(BuglyWebRequest owner) : base("cn.zero3d.buglywebrequest.WireBridge$Callback") { this.owner = owner; }
            [Preserve]
            public void onComplete(int index, AndroidJavaObject native)
            {
                try
                {
                    using (native)
                    {
                        bool success = native.Call<bool>("isSuccess");
                        string reason = native.Call<string>("getFailure");
                        int status = native.Call<int>("getStatus");
                        owner.Queue(new BuglyResponse { Index = index, Api = owner.api, Url = owner.urls[index],
                            Data = native.Call<byte[]>("getData"), ResponseCode = status, Success = success,
                            FailureReason = reason, Attempts = native.Call<int>("getAttempts"),
                            DurationMs = native.Call<long>("getDurationMs"), LastAttemptDurationMs = native.Call<long>("getLastAttemptMs"),
                            Transport = owner.snapshot.Transport,
                            Result = success ? RequestResult.Success : reason == "canceled" ? RequestResult.Canceled :
                                status >= 400 ? RequestResult.ProtocolError : RequestResult.ConnectionError });
                    }
                }
                catch (Exception error) { owner.Queue(owner.Failure(index, "callback_error:" + error.GetType().Name)); }
            }
        }
#else
        private IEnumerator RunManaged(int index)
        {
            var total = Stopwatch.StartNew();
            clocks[index] = total;
            BuglyResponse terminal = Failure(index, "connection_error");
            for (int attempt = 1; attempt <= snapshot.MaxAttempts && !aborted; attempt++)
            {
                var clock = Stopwatch.StartNew();
                using (var request = new UnityWebRequest(urls[index], "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(body); request.downloadHandler = new DownloadHandlerBuffer();
                    request.timeout = snapshot.TimeoutSeconds;
                    for (int h = 0; h < headerNames.Length; h++) request.SetRequestHeader(headerNames[h], headerValues[h]);
                    managedRequests.Add(request);
                    UnityWebRequestAsyncOperation started = null;
                    string sendFailure = null;
                    try { started = request.SendWebRequest(); }
                    catch (Exception error) { sendFailure = "send_error:" + error.GetType().Name; }
                    try { if (started != null) yield return started; }
                    finally { managedRequests.Remove(request); }
                    terminal = Failure(index, "connection_error"); terminal.Attempts = attempt;
                    terminal.ResponseCode = started == null ? 0 : request.responseCode;
                    terminal.Data = started == null ? new byte[0] : request.downloadHandler.data ?? new byte[0];
                    terminal.Success = started != null && request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300;
                    bool timeout = !string.IsNullOrEmpty(request.error) && (request.error.ToLowerInvariant().Contains("timeout") || request.error.ToLowerInvariant().Contains("timed out"));
                    terminal.FailureReason = terminal.Success ? "" : aborted ? "canceled" : sendFailure ?? (timeout ? "timeout" : request.responseCode >= 400 ? "http_" + request.responseCode : "connection_error");
                    if (terminal.Data.Length > snapshot.MaxResponseBytes) { terminal.Data = new byte[0]; terminal.Success = false; terminal.FailureReason = "response_too_large"; }
                    terminal.LastAttemptDurationMs = clock.ElapsedMilliseconds;
                    terminal.Result = terminal.Success ? RequestResult.Success : aborted ? RequestResult.Canceled :
                        request.responseCode >= 400 ? RequestResult.ProtocolError : RequestResult.ConnectionError;
                }
                bool retry = terminal.ResponseCode == 0 || terminal.ResponseCode == 408 || terminal.ResponseCode == 429 || terminal.ResponseCode >= 500;
                if (terminal.Success || !retry || aborted || terminal.FailureReason == "response_too_large" || terminal.FailureReason.StartsWith("send_error:", StringComparison.Ordinal) || attempt == snapshot.MaxAttempts) break;
                yield return new WaitForSecondsRealtime(Math.Min(2000L, (long)snapshot.RetryDelayMs * attempt) / 1000f);
            }
            terminal.DurationMs = total.ElapsedMilliseconds;
            if (aborted) { terminal.Success = false; terminal.FailureReason = "canceled"; terminal.Result = RequestResult.Canceled; }
            Queue(terminal);
        }
#endif
    }

    internal sealed class RequestRunner : MonoBehaviour
    {
        private static RequestRunner instance;
        private readonly List<BuglyWebRequest> requests = new List<BuglyWebRequest>();
        public static RequestRunner Ensure()
        {
            if (instance == null)
            {
                var gameObject = new GameObject("BuglyWebRequest callbacks");
                DontDestroyOnLoad(gameObject); instance = gameObject.AddComponent<RequestRunner>();
            }
            return instance;
        }
        public void Add(BuglyWebRequest request) { requests.Add(request); }
        private void Update()
        {
            for (int i = requests.Count - 1; i >= 0; i--) { requests[i].Pump(); if (requests[i].IsDone) requests.RemoveAt(i); }
        }
        private void OnApplicationQuit() { foreach (var request in requests) request.Abort(); }
    }
}
