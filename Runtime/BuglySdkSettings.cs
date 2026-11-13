using System;
using UnityEngine;
using ThinkingData.Analytics;

namespace ZeroGame.Networking
{
    public enum BuglyRegion { Overseas, Domestic }

    [CreateAssetMenu(fileName = "BuglyWebRequestSettings", menuName = "Bugly WebRequest/SDK Settings")]
    public sealed class BuglySdkSettings : ScriptableObject
    {
        public const string ResourceName = "BuglyWebRequestSettings";

        [Header("启动初始化" )]
        [Tooltip("下载前已获得隐私同意的项目可启用；不会写入游戏内点击同意记录。" )]
        public bool initializeOnStartup = true;

        [Header("Bugly 网络监控" )]
        public bool enableBugly = true;
        public string buglyAppId = "";
        public string buglyAppKey = "";
        [Tooltip("Bugly 用户 ID；用于匹配后台用户白名单。必须在初始化前配置。" )]
        public string buglyUserId = "";
        public BuglyRegion buglyRegion = BuglyRegion.Overseas;
        public bool buglyDebugMode;
        [Tooltip("输出 Bugly 原生 VERBOSE 日志以排查配置、采集和上报；正式发布建议关闭。独立于 Debug Mode。" )]
        public bool buglyEnableNativeLogs;
        [Tooltip("过滤回环和局域网地址（包括192.168.*）；局域网采集测试时关闭。" )]
        public bool buglyFilterLocalRequests = false;

        [Header("数数埋点")]
        public bool enableThinkingData = true;
        [Tooltip("仅当游戏已自行初始化数数时勾选；SDK 不再覆盖该实例的 Mode/TimeZone/日志/网络配置。")]
        public bool reuseThinkingDataInstance;
        public string thinkingAppId = "";
        public string thinkingServerUrl = "";
        public TDMode thinkingMode = TDMode.Normal;
        public TDTimeZone thinkingTimeZone = TDTimeZone.Local;
        [Tooltip("TimeZone=Other 时填写数数支持的时区 ID。")]
        public string thinkingTimeZoneId = "";
        public TDNetworkType thinkingNetworkType = TDNetworkType.All;
        public bool thinkingEnableLog;

        [Header("默认请求参数（单次请求仍可覆盖）")]
        public RequestOptions defaultRequest = new RequestOptions();

        [Header("测试与日志")]
        public bool enableEditorAnalytics;
        [Tooltip("SDK 信息日志开关；失败日志和规范要求的 net_cost 确认日志仍保留。")]
        public bool enableSdkLogs = true;

        public bool Validate(out string error)
        {
            if (enableBugly && (string.IsNullOrWhiteSpace(buglyAppId) || string.IsNullOrWhiteSpace(buglyAppKey)))
            { error = "请填写 Bugly AppID/AppKey，或关闭 enableBugly。"; return false; }
            if (enableThinkingData)
            {
                if (string.IsNullOrWhiteSpace(thinkingAppId)) { error = "请填写数数 AppID。"; return false; }
                if (!reuseThinkingDataInstance)
                {
                    Uri uri;
                    if (!Uri.TryCreate(thinkingServerUrl, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                    { error = "请填写有效的数数 HTTP(S) 数据接收地址。"; return false; }
                    if (thinkingTimeZone == TDTimeZone.Other && string.IsNullOrWhiteSpace(thinkingTimeZoneId))
                    { error = "TimeZone=Other 时需要填写时区 ID。"; return false; }
                }
            }
            try { CreateRequestOptions(); }
            catch (Exception) { error = "默认请求参数无效，请检查超时、重试次数及响应大小限制。"; return false; }
            error = ""; return true;
        }

        public RequestOptions CreateRequestOptions()
        {
            if (defaultRequest == null) throw new InvalidOperationException("Missing default request options");
            return defaultRequest.Snapshot();
        }

        public TDConfig CreateThinkingConfig()
        {
            return new TDConfig(thinkingAppId, thinkingServerUrl) {
                mode = thinkingMode, timeZone = thinkingTimeZone, timeZoneId = thinkingTimeZoneId
            };
        }
    }
}
