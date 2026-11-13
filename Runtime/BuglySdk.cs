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
            if (config.EnableBugly && !IsInitialized)
            {
                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                    using (var sdk = new AndroidJavaClass("cn.zero3d.buglywebrequest.SdkBridge"))
                        IsInitialized = sdk.CallStatic<bool>("initialize", context, config.BuglyAppId, config.BuglyAppKey,
                            Application.version, config.BuglyDebugMode, Debug.isDebugBuild, config.BuglyRegion == BuglyRegion.Overseas, config.BuglyEnableNativeLogs, "", config.BuglyFilterLocalRequests);
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
            bool platformAllowed = Application.platform == RuntimePlatform.Android ||
                (Application.isEditor && config.EnableEditorAnalytics);
            if (config.EnableThinkingData && !ThinkingDataReady && platformAllowed)
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
                    UseInitializedThinkingData(config.ThinkingAppId, config.EnableEditorAnalytics);
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

        // For consumer projects that already initialize TDAnalytics themselves.
        public static void UseInitializedThinkingData(string appId, bool enableEditorAnalytics = false)
        {
            ThinkingDataReady = true;
            Reporter.Sink = new ThinkingSink(appId, enableEditorAnalytics);
        }

        private sealed class ThinkingSink : INetCostSink
        {
            private readonly string appId;
            private readonly bool editorAllowed;
            public ThinkingSink(string id, bool editor) { appId = id; editorAllowed = editor; }
            public bool IsReady { get { return ThinkingDataReady &&
                (Application.platform == RuntimePlatform.Android || (Application.isEditor && editorAllowed)); } }
            public void Track(string name, Dictionary<string, object> properties) { TDAnalytics.Track(name, properties, appId); }
        }
    }
}
