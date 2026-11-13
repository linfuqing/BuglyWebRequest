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
        public static readonly NetCostReporter Reporter = new NetCostReporter(message => Debug.Log(message));

        // Compatibility API. New integrations should use a configuration asset and manager.
        [Obsolete("Use BuglySdkManager.Instance.InitializeAfterConsent() with BuglySdkSettings.")]
        public static void InitializeAfterConsent(string buglyAppId, string buglyAppKey,
            string thinkingAppId, string thinkingServerUrl, bool enableEditorAnalytics = false)
        {
            var config = ScriptableObject.CreateInstance<BuglySdkSettings>();
            config.BuglyAppId = buglyAppId; config.BuglyAppKey = buglyAppKey;
            config.EnableBugly = !string.IsNullOrWhiteSpace(buglyAppId) && !string.IsNullOrWhiteSpace(buglyAppKey);
            config.ThinkingAppId = thinkingAppId; config.ThinkingServerUrl = thinkingServerUrl;
            config.EnableThinkingData = !string.IsNullOrWhiteSpace(thinkingAppId) && !string.IsNullOrWhiteSpace(thinkingServerUrl);
            config.EnableEditorAnalytics = enableEditorAnalytics;
            try { InitializeAfterConsent(config); } finally { UnityEngine.Object.Destroy(config); }
        }

        public static bool InitializeAfterConsent(BuglySdkSettings config)
        {
            if (config == null) throw new ArgumentNullException("config");
            string validation;
            if (!config.Validate(out validation)) { Debug.LogError("[BuglyWebRequest] " + validation); return false; }
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
                            Application.version, config.BuglyDebugMode, Debug.isDebugBuild, config.BuglyRegion == BuglyRegion.Overseas, config.BuglyEnableNativeLogs, config.BuglyUserId ?? "", config.BuglyFilterLocalRequests);
                }
                catch (Exception error) { Debug.LogError("[BuglyWebRequest] Bugly init failed: " + error.GetType().Name); }
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
                catch (Exception error) { Debug.LogError("[BuglyWebRequest] ThinkingData init failed: " + error.GetType().Name); }
            }
            LogInfo("[BuglyWebRequest] Bugly ready=" + IsInitialized + "; ThinkingData ready=" + ThinkingDataReady);
            bool needsBugly = config.EnableBugly && Application.platform == RuntimePlatform.Android && !Application.isEditor;
            return (!needsBugly || IsInitialized) && (!config.EnableThinkingData || !platformAllowed || ThinkingDataReady);
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
