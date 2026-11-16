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
        private readonly string eventName;
        private readonly string[] urls, headerNames, headerValues;
        private readonly byte[] body;
        private readonly BuglyResponse[] responses;
        private readonly ConcurrentQueue<BuglyResponse> incoming = new ConcurrentQueue<BuglyResponse>();
        private readonly List<UnityWebRequest> managedRequests = new List<UnityWebRequest>();
        private readonly Stopwatch[] clocks;
        private bool sent, disposed, aborted, autoDispose, reportNetCost;
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
        public int Timeout { get { return Options.TimeoutSeconds; } set { if (disposed) throw new ObjectDisposedException("BuglyWebRequest", "[BuglyWebRequest] 请求已释放，不能修改 Timeout；请创建新的请求。"); if (sent) throw new InvalidOperationException("[BuglyWebRequest] 请求已启动，不能再修改 Timeout；手动入口请在 SendWebRequest() 之前设置，自动入口请在场景 SDK 组件的 DefaultRequest 中配置。"); Options.TimeoutSeconds = value; } }

        private BuglyWebRequest(string eventName, WWWForm form, string[] urls)
        {
            ValidateEventName(eventName);
            if (urls == null) throw new ArgumentNullException("urls", "[BuglyWebRequest] 显式 URL 入口的 urls 不能为 null；请传入至少一个 HTTP(S) 地址。读取文本配置请使用 Trace(eventName)。");
            if (urls.Length == 0) throw new ArgumentException("[BuglyWebRequest] urls 数组为空；请传入至少一个 HTTP(S) 地址，显式入口不会回退到文本配置。", "urls");
            this.eventName = eventName; this.urls = (string[])urls.Clone();
            for (int index = 0; index < this.urls.Length; index++)
            {
                string url = this.urls[index];
                ValidateUrl(url, "urls", "[BuglyWebRequest] urls[" + index + "]");
            }
            if (form == null)
            {
                body = new byte[0];
                headerNames = new string[0]; headerValues = new string[0];
            }
            else
            {
                try
                {
                    body = (byte[])form.data.Clone();
                    var headers = form.headers;
                    headerNames = new string[headers.Count]; headerValues = new string[headers.Count];
                    int i = 0;
                    foreach (var pair in headers) { headerNames[i] = pair.Key; headerValues[i++] = pair.Value; }
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException("[BuglyWebRequest][Request.FormSerialization] eventName='" + eventName + "'：无法读取 WWWForm 的请求体或请求头。请在 Unity 主线程创建并调用请求。原始原因：" + error.Message, error);
                }
            }
            responses = new BuglyResponse[urls.Length]; clocks = new Stopwatch[urls.Length];
            try { Options = BuglySdkManager.CreateRequestOptions(); }
            catch (Exception error)
            {
                throw new InvalidOperationException("[BuglyWebRequest][Request.DefaultRequest] eventName='" + eventName + "'：无法读取场景 SDK 组件的 DefaultRequest。原始原因：" + error.Message, error);
            }
        }

        // One-line HTTP dispatch: read configured URLs, start immediately, and own cleanup.
        public static BuglyWebRequest Trace(string eventName)
        {
            return Trace(eventName, null);
        }

        public static BuglyWebRequest Trace(string eventName, WWWForm form)
        {
            ValidateEventName(eventName);
            string[] configuredUrls;
            try { configuredUrls = BuglyUrlConfig.LoadCached(); }
            catch (Exception error)
            {
                throw new InvalidOperationException("[BuglyWebRequest][Trace.UrlConfiguration] eventName='" + eventName + "'：读取文本 URL 配置失败。原始原因：" + error.Message, error);
            }
            return StartOwned(new BuglyWebRequest(eventName, form, configuredUrls));
        }

        /// <summary>创建单地址 POST 请求；设置参数后调用 SendWebRequest() 才开始发送。</summary>
        /// <param name="eventName">本次请求的埋点标识。</param>
        /// <param name="form">发送给服务器的表单参数；无字段时传 new WWWForm()。</param>
        /// <param name="url">单个完整 HTTP(S) 地址，不读取文本 URL 配置。</param>
        public static BuglyWebRequest Post(string eventName, WWWForm form, string url)
        {
            ValidateEventName(eventName);
            if (form == null) throw new ArgumentNullException("form", "[BuglyWebRequest][Post] form 不能为 null；没有字段时传 new WWWForm()。读取文本地址并自动发送请使用 Trace(eventName)。");
            if (url == null) throw new ArgumentNullException("url", "[BuglyWebRequest][Post] url 不能为 null；请传入单个完整 HTTP(S) 地址，不读取文本配置。");
            ValidateUrl(url, "url", "[BuglyWebRequest][Post] url");
            return new BuglyWebRequest(eventName, form, new[] { url });
        }

        private static BuglyWebRequest StartOwned(BuglyWebRequest request)
        {
            request.autoDispose = true;
            try
            {
                request.SendWebRequest();
                return request;
            }
            catch (Exception startError)
            {
                Exception cleanupError = null;
                try { request.Dispose(); }
                catch (Exception error) { cleanupError = error; }
                string context = "[BuglyWebRequest][Trace.AutoStart] eventName='" + request.eventName +
                    "', transport=" + request.Options.Transport + ", urlCount=" + request.urls.Length +
                    "：自动发送启动失败。原始异常 " + startError.GetType().Name + "：" + startError.Message;
                if (cleanupError != null)
                    throw new InvalidOperationException(context + "；释放请求时又发生 " + cleanupError.GetType().Name + "：" + cleanupError.Message + "。两个异常均保留在 InnerException 中。",
                        new AggregateException("请求启动与失败清理分别发生异常。", startError, cleanupError));
                throw new InvalidOperationException(context, startError);
            }
        }

        // Unity main thread only. Requests are started once and complete independently.
        public BuglyWebRequestOperation SendWebRequest()
        {
            if (disposed) throw new ObjectDisposedException("BuglyWebRequest", "[BuglyWebRequest][SendWebRequest] eventName='" + eventName + "'：请求已被 Dispose 或自动释放，不能再次发送；请创建新的请求。");
            if (sent) throw new InvalidOperationException("[BuglyWebRequest][SendWebRequest] eventName='" + eventName + "'：同一个请求只能启动一次。Trace(eventName) 和 Trace(eventName, form) 已自动启动，不要再调用 SendWebRequest()。");
            snapshot = Options.Snapshot();
            var config = RequestInitializationGuard.EnsureReady(eventName, snapshot);
            bool androidPlayer = Application.platform == RuntimePlatform.Android && !Application.isEditor;
            reportNetCost = config.EnableThinkingData && snapshot.ReportNetCost &&
                RequestInitializationGuard.SupportsThinkingData(config, androidPlayer, Application.isEditor);
            sent = true;
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
                            snapshot.MaxResponseBytes, config.EnableBugly && BuglySdk.IsInitialized, nativeCallback));
                }
                catch (Exception exception) { LogRequestException("Android.BridgeSubmit", i, exception); Queue(Failure(i, "bridge_error:" + exception.GetType().Name)); }
            }
            BuglySdk.LogInfo("[BuglyWebRequest] Android " + snapshot.Transport + "; urls=" + urls.Length);
#else
            for (int i = 0; i < urls.Length; i++) runner.StartCoroutine(RunManaged(i));
            BuglySdk.LogInfo("[BuglyWebRequest] UnityWebRequest fallback (Editor/WebGL/desktop); urls=" + urls.Length);
#endif
            return operation;
        }

        private static void ValidateUrl(string url, string parameter, string context)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException(context + " 为空；请填写完整 HTTP(S) 地址。", parameter);
            Uri parsed;
            if (!Uri.TryCreate(url, UriKind.Absolute, out parsed) || string.IsNullOrEmpty(parsed.Host))
                throw new ArgumentException(context + " 不是包含主机名的完整 URL；格式应为 http://host/path 或 https://host/path。", parameter);
            if (parsed.Scheme != "http" && parsed.Scheme != "https")
                throw new ArgumentException(context + " 使用了不支持的协议 " + parsed.Scheme + "；仅支持 http 和 https。", parameter);
            foreach (char character in url)
                if (char.IsWhiteSpace(character)) throw new ArgumentException(context + " 包含空白字符；请移除空格、换行或行内注释，需要的空格请进行 URL 编码。", parameter);
        }

        private static void ValidateEventName(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName)) throw new ArgumentException("[BuglyWebRequest] eventName 不能为空，请传入稳定的埋点名称，例如 query_level。", "eventName");
            if (eventName.Length > 120) throw new ArgumentException("[BuglyWebRequest] eventName 长度为 " + eventName.Length + "，超过最大限制 120 个字符。", "eventName");
            foreach (char character in eventName)
                if (char.IsWhiteSpace(character)) throw new ArgumentException("[BuglyWebRequest] eventName 不能包含空格、Tab 或换行，请使用 query_level 等不含空白的埋点名称。", "eventName");
        }

        private void RequireSingleUrl()
        {
            if (urls.Length != 1) throw new InvalidOperationException("[BuglyWebRequest] eventName='" + eventName + "' 配置了 " + urls.Length + " 个 URL，不能使用单地址属性 DownloadHandler/ResponseCode；请读取 Responses，或在 UrlCompleted 中处理每个地址的结果。");
        }

        private void LogRequestException(string stage, int index, Exception error)
        {
            Debug.LogException(new InvalidOperationException("[BuglyWebRequest][" + stage + "] eventName='" + eventName +
                "', urls[" + index + "]=" + NetCostReporter.AnalyticsUrl(urls[index]) +
                ", transport=" + (snapshot == null ? Options.Transport : snapshot.Transport) + "：" + error.Message, error));
        }
        private BuglyResponse Failure(int index, string reason)
        {
            return new BuglyResponse { Index = index, EventName = eventName, Url = urls[index], Data = new byte[0],
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
                if (reportNetCost) BuglySdk.Reporter.Report(response, Debug.isDebugBuild);
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
                if (autoDispose) Dispose();
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
                        owner.Queue(new BuglyResponse { Index = index, EventName = owner.eventName, Url = owner.urls[index],
                            Data = native.Call<byte[]>("getData"), ResponseCode = status, Success = success,
                            FailureReason = reason, Attempts = native.Call<int>("getAttempts"),
                            DurationMs = native.Call<long>("getDurationMs"), LastAttemptDurationMs = native.Call<long>("getLastAttemptMs"),
                            Transport = owner.snapshot.Transport,
                            Result = success ? RequestResult.Success : reason == "canceled" ? RequestResult.Canceled :
                                status >= 400 ? RequestResult.ProtocolError : RequestResult.ConnectionError });
                    }
                }
                catch (Exception error) { owner.LogRequestException("Android.ReadCallback", index, error); owner.Queue(owner.Failure(index, "callback_error:" + error.GetType().Name)); }
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
                    catch (Exception error) { LogRequestException("UnityWebRequest.Send", index, error); sendFailure = "send_error:" + error.GetType().Name; }
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
