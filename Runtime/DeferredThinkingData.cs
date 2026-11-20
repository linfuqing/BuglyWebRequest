using System;
using UnityEngine;

namespace ZeroGame.Networking
{
    // Starts only from a real send, not prefab startup or Post construction. Main thread only.
    internal static class DeferredThinkingData
    {
        private static bool active, timedOut, probeErrorLogged;
        private static string appId;
        private static bool editorAllowed;
        private static float startedAt, nextProbe, timeoutSeconds, nextDropLog;

        internal static void Begin(BuglySdkSettings config)
        {
            string id = config.ThinkingAppId ?? "";
            if (active && !string.Equals(id, appId, StringComparison.Ordinal))
            {
                BuglySdk.Reporter.DiscardPending("数数目标实例切换，旧实例暂存埋点已丢弃，避免发往错误项目");
                BuglySdk.ClearThinkingBinding();
                Reset();
            }
            if (!active)
            {
                BuglySdk.Reporter.ResumeBuffering();
                active = true; appId = id; editorAllowed = config.EnableEditorAnalytics;
                startedAt = nextProbe = nextDropLog = Time.realtimeSinceStartup;
                timeoutSeconds = config.ThinkingReuseWaitTimeoutSeconds;
                RequestRunner.Ensure(); // Keeps checking after HTTP work is done.
            }
            TryBind(Time.realtimeSinceStartup);
        }

        private static void TryBind(float now)
        {
            if (timedOut) return;
            if (!BuglySdk.ThinkingDataReady && now - startedAt >= timeoutSeconds)
            {
                timedOut = true;
                BuglySdk.ClearThinkingBinding();
                BuglySdk.Reporter.StopBuffering("等待数数初始化超时，暂存的 net_cost 已丢弃，停止检测和继续暂存");
                Debug.LogException(new InvalidOperationException(
                    "[BuglyWebRequest][Reuse.ThinkingData] 未找到已初始化的数数实例（" +
                    (string.IsNullOrEmpty(appId) ? "默认实例" : "指定 AppID/实例标识") +
                    "）。首次实际请求后已等待 " + timeoutSeconds +
                    " 秒，本次运行已停止该实例的自动检测。请先由游戏初始化官方 SDK；复用模式无需填写 ServerUrl、Mode 或 TimeZone，AppID 留空使用默认实例。网络请求继续执行，未上报的 net_cost 不再暂存。"));
                return;
            }
            if (now < nextProbe) return;
            nextProbe = now + 1f;
            if (BuglySdk.ThinkingDataReady)
            {
                if (BuglySdk.Reporter.PendingCount == 0) return;
                try { if (BuglySdk.Reporter.Sink != null && BuglySdk.Reporter.Sink.IsReady) return; }
                catch { /* A lost analytics instance must not affect network traffic. */ }
                BuglySdk.ClearThinkingBinding();
                startedAt = now;
            }
            try
            {
                if (NativeSdkInstances.ThinkingReady(appId))
                {
                    BuglySdk.BindThinkingData(appId, editorAllowed, true);
                    BuglySdk.LogInfo("[BuglyWebRequest][ThinkingData] 复用实例已就绪，将补报暂存的 net_cost；未重复初始化数数。");
                }
            }
            catch (Exception error)
            {
                if (!probeErrorLogged)
                {
                    probeErrorLogged = true;
                    Debug.LogWarning("[BuglyWebRequest][ThinkingData] 复用实例检测发生 " + error.GetType().Name +
                        "，请检查官方 SDK 原生依赖和 AppID；仅在设定时限内继续检测，网络请求不等待，net_cost 有限暂存。");
                }
            }
        }

        internal static void Tick()
        {
            if (!active) return;
            float now = Time.realtimeSinceStartup;
            TryBind(now);
            if (!timedOut) BuglySdk.Reporter.FlushPending(8);
            if (now >= nextDropLog) { nextDropLog = now + 1f; BuglySdk.Reporter.LogDropped(); }
        }

        internal static void EndSession()
        {
            BuglySdk.Reporter.FlushPending(8);
            BuglySdk.Reporter.DiscardPending("进程退出，未提交给数数的内存暂存记录无法保留");
            Reset();
        }

        internal static void Reset()
        {
            active = timedOut = probeErrorLogged = false; appId = null; editorAllowed = false;
            startedAt = nextProbe = timeoutSeconds = nextDropLog = 0;
        }
    }
}
