using System;
using System.Collections.Generic;
using UnityEngine;
using ThinkingData.Analytics;

namespace ZeroGame.Networking
{
    // Query public SDK APIs only. Never initialize or overwrite a consumer-owned instance.
    internal static class NativeSdkInstances
    {
        internal static bool BuglyNetworkReady()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var bridge = new AndroidJavaClass("cn.zero3d.buglywebrequest.SdkBridge"))
                return bridge.CallStatic<bool>("reuseInitialized");
#else
            return false;
#endif
        }

        internal static bool ThinkingReady(string appId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var instance = ThinkingInstance(appId))
                return instance != null && instance.GetRawObject() != IntPtr.Zero;
#else
            return !string.IsNullOrEmpty(TDAnalytics.GetDistinctId(appId ?? ""));
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject ThinkingInstance(string appId)
        {
            using (var sdk = new AndroidJavaClass("cn.thinkingdata.analytics.TDAnalyticsAPI"))
                return sdk.CallStatic<AndroidJavaObject>("getInstance", appId ?? "");
        }

        internal static void TrackThinking(string appId, string eventName, Dictionary<string, object> properties)
        {
            using (var instance = ThinkingInstance(appId))
            {
                if (instance == null || instance.GetRawObject() == IntPtr.Zero)
                    throw new InvalidOperationException("[BuglyWebRequest][Reuse.ThinkingData] 目标数数原生实例不可用；请先初始化，AppID 留空选择默认实例。");
                // Native-only initialization does not start the Unity facade; call the existing instance directly.
                using (var json = new AndroidJavaObject("org.json.JSONObject", ThinkingData.Analytics.Utils.TDMiniJson.Serialize(properties)))
                    instance.Call("track", eventName, json);
            }
        }
#endif
    }
}
