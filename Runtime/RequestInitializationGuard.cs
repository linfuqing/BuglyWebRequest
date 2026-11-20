using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeroGame.Networking
{
    internal static class RequestInitializationGuard
    {
        internal static BuglySdkSettings EnsureReady(string eventName, RequestOptions options)
        {
            var config = BuglySdkManager.CurrentSettings ?? BuglySdk.ExternalReuseSettings();
            try { BuglySdk.RefreshReusedInstances(config); }
            catch (Exception error)
            {
                throw new InvalidOperationException("[BuglyWebRequest][Request.Reuse] eventName='" + eventName +
                    "'：复用原生 SDK 失败。" + error.Message + " 请求尚未发送；请先完成原生初始化后重试。", error);
            }
            bool androidPlayer = Application.platform == RuntimePlatform.Android && !Application.isEditor;
            bool editor = Application.isEditor;
            bool sinkReady = false;
            if (config != null && config.EnableThinkingData && !config.ReuseThinkingDataInstance && options.ReportNetCost &&
                SupportsThinkingData(config, androidPlayer, editor) && BuglySdk.ThinkingDataReady)
            {
                try { sinkReady = BuglySdk.Reporter.Sink != null && BuglySdk.Reporter.Sink.IsReady; }
                catch (Exception error)
                {
                    throw new InvalidOperationException("[BuglyWebRequest][Trace.Initialization] eventName='" + eventName +
                        "'：检查数数埋点实例 IsReady 时发生异常，请检查已绑定的 INetCostSink。原始原因：" + error.Message, error);
                }
            }
            Validate(eventName, config, options, androidPlayer, editor, BuglySdk.IsInitialized,
                BuglySdk.ThinkingDataReady, sinkReady, BuglySdk.LastInitializationError);
            if (config.EnableThinkingData && config.ReuseThinkingDataInstance && options.ReportNetCost &&
                SupportsThinkingData(config, androidPlayer, editor))
                DeferredThinkingData.Begin(config);
            return config;
        }

        internal static bool SupportsThinkingData(BuglySdkSettings config, bool androidPlayer, bool editor)
        {
            return androidPlayer || (editor && config.EnableEditorAnalytics);
        }

        // Pure policy: platform/module states are explicit so every supported combination can be verified.
        internal static void Validate(string eventName, BuglySdkSettings config, RequestOptions options,
            bool androidPlayer, bool editor, bool buglyReady, bool thinkingReady, bool sinkReady, string lastError)
        {
            string prefix = "[BuglyWebRequest][Trace.Initialization] eventName='" + eventName + "'：";
            if (config == null)
                throw new InvalidOperationException(prefix + "SDK 配置缺失；可挂载 BuglyWebRequest.prefab，或先完成 Bugly/数数原生初始化以使用无组件复用模式；请求尚未发送。");
            var missing = new List<string>();
            if (config.EnableBugly && androidPlayer && !buglyReady)
                missing.Add("Bugly 网络监控已启用，但 Bugly 尚未初始化成功");
            if (config.EnableThinkingData && !config.ReuseThinkingDataInstance && options.ReportNetCost && SupportsThinkingData(config, androidPlayer, editor))
            {
                if (!thinkingReady) missing.Add("数数埋点已启用，但数数尚未初始化或绑定实例");
                else if (!sinkReady) missing.Add("数数初始化标记已就绪，但埋点实例未绑定或 IsReady=false");
            }
            if (missing.Count == 0) return;
            string message = prefix + string.Join("；", missing.ToArray()) +
                "。请开启场景 SDK 组件的 InitializeOnStartup 并在其 Start 完成后调用 Trace，或在隐私同意后先调用 BuglySdkManager.Instance.InitializeAfterConsent() 并确认返回 true。";
            if (config.ReuseThinkingDataInstance && config.EnableThinkingData && options.ReportNetCost && SupportsThinkingData(config, androidPlayer, editor))
                message += " ReuseThinkingDataInstance 已开启：请先由游戏初始化对应 AppID 的数数实例，再发送请求以绑定该实例；AppID 留空使用默认实例。";
            if (config.ReuseBuglyInstance && config.EnableBugly && androidPlayer)
                message += " ReuseBuglyInstance 已开启：请先由游戏初始化 Bugly 并启用 NET_QUALITY，等待模块启动后重试。";
            if (!string.IsNullOrEmpty(lastError)) message += " 上次初始化错误：" + lastError;
            throw new InvalidOperationException(message + " 请求尚未发送。");
        }
    }
}
