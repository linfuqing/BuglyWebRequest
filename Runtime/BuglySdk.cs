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
            config.buglyAppId = buglyAppId; config.buglyAppKey = buglyAppKey;
            config.enableBugly = !string.IsNullOrWhiteSpace(buglyAppId) && !string.IsNullOrWhiteSpace(buglyAppKey);
            config.thinkingAppId = thinkingAppId; config.thinkingServerUrl = thinkingServerUrl;
            config.enableThinkingData = !string.IsNullOrWhiteSpace(thinkingAppId) && !string.IsNullOrWhiteSpace(thinkingServerUrl);
            config.enableEditorAnalytics = enableEditorAnalytics;
            try { InitializeAfterConsent(config); } finally { UnityEngine.Object.Destroy(config); }
        }

        public static bool InitializeAfterConsent(BuglySdkSettings config)
        {
            if (config == null) throw new ArgumentNullException("config");
            string validation;
            if (!config.Validate(out validation)) { Debug.LogError("[BuglyWebRequest] " + validation); return false; }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (config.enableBugly && !IsInitialized)
            {
                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                    using (var sdk = new AndroidJavaClass("cn.zero3d.buglywebrequest.SdkBridge"))
                        IsInitialized = sdk.CallStatic<bool>("initialize", context, config.buglyAppId, config.buglyAppKey,
                            Application.version, config.buglyDebugMode, Debug.isDebugBuild, config.buglyRegion == BuglyRegion.Overseas, config.buglyEnableNativeLogs, config.buglyUserId ?? "", config.buglyFilterLocalRequests);
                }
                catch (Exception error) { Debug.LogError("[BuglyWebRequest] Bugly init failed: " + error.GetType().Name); }
            }
#endif
            bool platformAllowed = Application.platform == RuntimePlatform.Android ||
                (Application.isEditor && config.enableEditorAnalytics);
            if (config.enableThinkingData && !ThinkingDataReady && platformAllowed)
            {
                try
                {
                    if (!config.reuseThinkingDataInstance)
                    {
                        var td = config.CreateThinkingConfig();
                        TDAnalytics.EnableLog(config.thinkingEnableLog);
                        TDAnalytics.Init(td);
                        TDAnalytics.SetNetworkType(config.thinkingNetworkType, config.thinkingAppId);
                    }
                    UseInitializedThinkingData(config.thinkingAppId, config.enableEditorAnalytics);
                }
                catch (Exception error) { Debug.LogError("[BuglyWebRequest] ThinkingData init failed: " + error.GetType().Name); }
            }
            LogInfo("[BuglyWebRequest] Bugly ready=" + IsInitialized + "; ThinkingData ready=" + ThinkingDataReady);
            bool needsBugly = config.enableBugly && Application.platform == RuntimePlatform.Android && !Application.isEditor;
            return (!needsBugly || IsInitialized) && (!config.enableThinkingData || !platformAllowed || ThinkingDataReady);
        }

        internal static void LogInfo(string message)
        {
            var config = BuglySdkManager.CurrentSettings;
            if (config == null || config.enableSdkLogs) Debug.Log(message);
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
