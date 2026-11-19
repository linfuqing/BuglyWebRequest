using System;
using System.Collections.Generic;
using UnityEngine;
using ThinkingData.Analytics;

namespace ZeroGame.Networking
{
    public static class BuglySdk
    {
        public static bool IsInitialized { get; private set; }
        public static bool ThinkingDataReady { get; private set; }
        public static string LastInitializationError { get; private set; } = string.Empty;
        public static readonly NetCostReporter Reporter = new NetCostReporter(message => Debug.Log(message));

        public static bool InitializeAfterConsent(BuglySdkSettings config)
        {
            if (config == null) throw new ArgumentNullException("config", "[BuglyWebRequest][Initialize] SDK 配置为 null；请使用场景 BuglySDK Prefab 的 Settings 初始化。");
            LastInitializationError = string.Empty;
            var failures = new List<string>();
            string validation;
            if (!config.Validate(out validation))
            {
                LastInitializationError = "[BuglyWebRequest][Initialize.Configuration] " + validation;
                Debug.LogError(LastInitializationError); return false;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (config.EnableBugly && (config.ReuseBuglyInstance || !IsInitialized))
            {
                try
                {
                    if (config.ReuseBuglyInstance)
                        RequireReusedBugly();
                    else
                    {
                        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                        using (var sdk = new AndroidJavaClass("cn.zero3d.buglywebrequest.SdkBridge"))
                            IsInitialized = sdk.CallStatic<bool>("initialize", context, config.BuglyAppId, config.BuglyAppKey,
                                Application.version, config.BuglyDebugMode, Debug.isDebugBuild, config.BuglyRegion == BuglyRegion.Overseas, config.BuglyEnableNativeLogs, "", config.BuglyFilterLocalRequests);
                    }
                    if (!IsInitialized)
                    {
                        string message = "[BuglyWebRequest][Initialize.Bugly] Android Bugly.init 返回 false，原生 SDK 未提供详细错误文本。请开启 BuglyEnableNativeLogs，查看 Android Logcat 中 Bugly/RMonitor 的初始化日志；当前地区=" + config.BuglyRegion + "。";
                        failures.Add(message); Debug.LogError(message);
                    }
                }
                catch (Exception error)
                {
                    var diagnostic = new InvalidOperationException("[BuglyWebRequest][Initialize.Bugly] Android Bugly 初始化发生 " + error.GetType().Name + "，地区=" + config.BuglyRegion + "。请检查原生桥接及 Maven 依赖是否已打入 Android 包。原始原因：" + error.Message, error);
                    failures.Add(diagnostic.Message); Debug.LogException(diagnostic);
                }
            }
#endif
            UnityExceptionReporter.Configure(config.EnableBugly && IsInitialized, config.CaptureUnityExceptions, config.CaptureUnityErrors);
            bool platformAllowed = Application.platform == RuntimePlatform.Android ||
                (Application.isEditor && config.EnableEditorAnalytics);
            if (config.EnableThinkingData && (config.ReuseThinkingDataInstance || !ThinkingDataReady) && platformAllowed)
            {
                try
                {
                    if (!config.ReuseThinkingDataInstance)
                    {
                        var td = config.CreateThinkingConfig();
                        TDAnalytics.EnableLog(config.ThinkingEnableLog);
                        TDAnalytics.Init(td);
                        TDAnalytics.SetNetworkType(config.ThinkingNetworkType, config.ThinkingAppId);
                    }
                    BindThinkingData(config.ThinkingAppId, config.EnableEditorAnalytics, config.ReuseThinkingDataInstance);
                }
                catch (Exception error)
                {
                    var diagnostic = new InvalidOperationException("[BuglyWebRequest][Initialize.ThinkingData] 数数初始化或绑定实例发生 " + error.GetType().Name + "，ReuseThinkingDataInstance=" + config.ReuseThinkingDataInstance + "。原始原因：" + error.Message, error);
                    failures.Add(diagnostic.Message); Debug.LogException(diagnostic);
                }
            }
            LogInfo("[BuglyWebRequest] Bugly ready=" + IsInitialized + "; ThinkingData ready=" + ThinkingDataReady);
            bool needsBugly = config.EnableBugly && Application.platform == RuntimePlatform.Android && !Application.isEditor;
            bool ready = (!needsBugly || IsInitialized) && (!config.EnableThinkingData || !platformAllowed || ThinkingDataReady);
            LastInitializationError = string.Join("\n", failures.ToArray());
            if (!ready && failures.Count == 0)
                LastInitializationError = "[BuglyWebRequest][Initialize] 初始化未完成：BuglyReady=" + IsInitialized + ", ThinkingDataReady=" + ThinkingDataReady + "；请查看对应模块初始化日志。";
            return ready;
        }

        internal static void LogInfo(string message)
        {
            var config = BuglySdkManager.CurrentSettings;
            if (config == null || config.EnableSdkLogs) Debug.Log(message);
        }

        // Used when a project supplies all native configuration and has no SDK scene component.
        internal static BuglySdkSettings ExternalReuseSettings()
        {
            return new BuglySdkSettings { InitializeOnStartup = false, ReuseBuglyInstance = true,
                ReuseThinkingDataInstance = true };
        }

        internal static void RefreshReusedInstances(BuglySdkSettings config, bool reportNetCost)
        {
            if (config.EnableBugly && config.ReuseBuglyInstance && Application.platform == RuntimePlatform.Android && !Application.isEditor)
                RequireReusedBugly();
            UnityExceptionReporter.Configure(config.EnableBugly && IsInitialized, config.CaptureUnityExceptions, config.CaptureUnityErrors);
            if (config.EnableThinkingData && config.ReuseThinkingDataInstance && reportNetCost &&
                RequestInitializationGuard.SupportsThinkingData(config, Application.platform == RuntimePlatform.Android && !Application.isEditor, Application.isEditor))
                BindThinkingData(config.ThinkingAppId, config.EnableEditorAnalytics, true);
        }

        private static void RequireReusedBugly()
        {
            IsInitialized = NativeSdkInstances.BuglyNetworkReady();
            if (!IsInitialized)
                throw new InvalidOperationException("[BuglyWebRequest][Reuse.Bugly] 未检测到已启动的 NET_QUALITY 网络监控模块。请先由游戏完成官方 Bugly 初始化并启用该模块，等待模块启动后重试；本 SDK 不会重复初始化或覆盖原生配置。");
        }

        public static bool UnityExceptionHandlerEnabled { get { return UnityExceptionReporter.Enabled; } }

        /// <summary>Native-only integration: call on Unity's main thread after native Bugly NET_QUALITY is ready. Never initializes Bugly again.</summary>
        public static void EnableUnityExceptionHandler(bool captureErrors = true)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var config = BuglySdkManager.CurrentSettings;
            if (config != null && !config.EnableBugly)
                throw new InvalidOperationException("[BuglyWebRequest][UnityException] EnableBugly 已关闭，请先启用或由游戏配置原生 Bugly。");
            if (!IsInitialized) RequireReusedBugly();
            UnityExceptionReporter.Enable(captureErrors);
#else
            throw new PlatformNotSupportedException("[BuglyWebRequest][UnityException] C# 异常上报当前仅支持 Android 真机，不在 Editor/WebGL/桌面端发送。");
#endif
        }

        public static void DisableUnityExceptionHandler() { UnityExceptionReporter.Disable(); }

        /// <summary>Queue a caught C# exception after SDK initialization; never logs the exception a second time.</summary>
        public static void ReportException(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException("exception", "[BuglyWebRequest][UnityException] 要上报的异常不能为 null。");
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsInitialized) throw new InvalidOperationException("[BuglyWebRequest][UnityException] Bugly 尚未初始化，请先完成初始化或原生实例复用。");
            UnityExceptionReporter.Report(exception);
#else
            throw new PlatformNotSupportedException("[BuglyWebRequest][UnityException] C# 异常上报当前仅支持 Android 真机。");
#endif
        }

        // For consumer projects that already initialize the official SDK themselves.
        public static void UseInitializedThinkingData(string appId = "", bool enableEditorAnalytics = false)
        {
            BindThinkingData(appId, enableEditorAnalytics, true);
        }

        private static void BindThinkingData(string appId, bool enableEditorAnalytics, bool nativeReuse)
        {
            string id = appId ?? "";
            if (!NativeSdkInstances.ThinkingReady(id))
            {
                ThinkingDataReady = false;
                throw new InvalidOperationException("[BuglyWebRequest][Reuse.ThinkingData] 未找到已初始化的数数实例（" +
                    (id.Length == 0 ? "默认实例" : "指定 AppID/实例标识") + "）。请先由游戏初始化官方 SDK；复用模式无需填写 ServerUrl、Mode 或 TimeZone，AppID 留空使用默认实例。");
            }
            Reporter.Sink = new ThinkingSink(id, enableEditorAnalytics, nativeReuse);
            ThinkingDataReady = true;
        }

        private sealed class ThinkingSink : INetCostSink
        {
            private readonly string appId;
            private readonly bool editorAllowed, nativeReuse;
            public ThinkingSink(string id, bool editor, bool reuse) { appId = id; editorAllowed = editor; nativeReuse = reuse; }
            public bool IsReady { get { return ThinkingDataReady &&
                (Application.platform == RuntimePlatform.Android || (Application.isEditor && editorAllowed)) && NativeSdkInstances.ThinkingReady(appId); } }
            public void Track(string name, Dictionary<string, object> properties)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (nativeReuse) { NativeSdkInstances.TrackThinking(appId, name, properties); return; }
#endif
                TDAnalytics.Track(name, properties, appId);
            }
        }
    }
}
