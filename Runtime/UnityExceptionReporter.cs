using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ZeroGame.Networking
{
    [AddComponentMenu("")]
    internal sealed class UnityExceptionReporter : MonoBehaviour
    {
        private const string DiagnosticPrefix = "[BuglyWebRequest][UnityException]";
        private static readonly UnityExceptionBuffer buffer = new UnityExceptionBuffer();
        private static UnityExceptionReporter instance;
        private static volatile bool initialized, capture, captureErrors;
        private static int mainThread, warnedDrops;
        private static long lastWarningMs;
        private static bool manuallyDisabled;
        [ThreadStatic] private static bool handling;
        [ThreadStatic] private static bool submitting;
        internal static bool Enabled { get { return capture; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            initialized = capture = captureErrors = false; handling = submitting = false; mainThread = warnedDrops = 0; lastWarningMs = 0; manuallyDisabled = false; instance = null; buffer.Clear();
        }
        // Called only after Bugly initialization/reuse is verified, on Unity's main thread.
        internal static void Configure(bool ready, bool automatic, bool errors)
        {
            automatic = automatic && !manuallyDisabled;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!ready) { Unsubscribe(); initialized = false; buffer.Clear(); return; }
            if (instance == null)
            {
                mainThread = Thread.CurrentThread.ManagedThreadId;
                instance = FindObjectOfType<UnityExceptionReporter>();
                if (instance == null)
                {
                    var host = new GameObject("Bugly Unity exceptions");
                    DontDestroyOnLoad(host); instance = host.AddComponent<UnityExceptionReporter>();
                }
            }
            initialized = true; captureErrors = errors;
            if (capture == automatic) return;
            Application.logMessageReceivedThreaded -= OnLog;
            capture = automatic;
            if (automatic) Application.logMessageReceivedThreaded += OnLog;
#endif
        }
        internal static void Enable(bool errors) { manuallyDisabled = false; Configure(true, true, errors); }
        internal static void Disable() { manuallyDisabled = true; Unsubscribe(); }
        private static void Unsubscribe()
        {
            capture = false;
            Application.logMessageReceivedThreaded -= OnLog;
        }
        internal static void Report(Exception error)
        {
            if (!initialized) throw new InvalidOperationException(DiagnosticPrefix + " C# 上报器尚未就绪；请先初始化本 SDK，或在原生 Bugly 就绪后调用 BuglySdk.EnableUnityExceptionHandler()。");
            buffer.Enqueue(error.GetType().FullName, error.Message, error.ToString(), ThreadLabel(), NowMs());
        }
        private static void OnLog(string message, string stack, LogType type)
        {
            if (!capture || handling || submitting) return;
            if (type != LogType.Exception && !(captureErrors && (type == LogType.Error || type == LogType.Assert))) return;
            if (message != null && message.StartsWith(DiagnosticPrefix, StringComparison.Ordinal)) return;
            handling = true;
            try
            {
                string name = type == LogType.Exception ? "UnityException" : type == LogType.Assert ? "UnityAssert" : "UnityLogError";
                int colon = message == null ? -1 : message.IndexOf(':');
                if (type == LogType.Exception && colon > 0 && colon <= 256 && message.IndexOf(' ', 0, colon) < 0)
                    name = message.Substring(0, colon);
                buffer.Enqueue(name, message, stack, ThreadLabel(), NowMs());
            }
            catch { /* Never throw or produce another Unity log from the logging callback. */ }
            finally { handling = false; }
        }
        private static string ThreadLabel() { var thread = Thread.CurrentThread; return thread.ManagedThreadId + ":" + (thread.Name ?? "managed"); }
        private static long NowMs() { return (long)(Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency)); }
        private void Update()
        {
            Flush(4);
            int dropped = buffer.DroppedCount; long now = NowMs();
            if (dropped > warnedDrops && now - lastWarningMs >= 10000)
            {
                warnedDrops = dropped; lastWarningMs = now;
                Debug.LogWarning(DiagnosticPrefix + " 异常队列或短时错误数量达到上限，累计未入队 " + dropped + " 条；请检查重复异常源。相同异常去重不计入此数量。");
            }
        }
        private static void Flush(int maximum)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!initialized || Thread.CurrentThread.ManagedThreadId != mainThread) return;
            UnityExceptionRecord record;
            while (maximum-- > 0 && buffer.TryDequeue(out record))
            {
                submitting = true;
                try
                {
                    using (var bridge = new AndroidJavaClass("cn.zero3d.buglywebrequest.SdkBridge"))
                        bridge.CallStatic("reportUnityException", record.Type, record.Message, record.Stack, record.Thread);
                    BuglySdk.LogInfo(DiagnosticPrefix + " 已提交 Android Bugly C# 错误，类型=" + record.Type + "；请在后台“错误”栏目核对接收结果。");
                }
                catch (Exception error)
                {
                    // Prefix and Warning level prevent recursive error reporting; do not echo the original payload.
                    Debug.LogWarning(DiagnosticPrefix + " 提交给 Android Bugly 失败：" + error.GetType().Name + "。请检查原生桥接与 Bugly 依赖。");
                }
                finally { submitting = false; }
            }
#endif
        }
        private void OnApplicationQuit() { Flush(8); Unsubscribe(); initialized = false; }
        private void OnDestroy()
        {
            if (instance != this) return;
            Unsubscribe(); initialized = false; instance = null; buffer.Clear();
        }
    }
}
