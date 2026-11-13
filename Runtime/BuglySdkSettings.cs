using System;
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("initializeOnStartup")]
        public bool InitializeOnStartup = true;

        [Header("Bugly 网络监控" )]
        [FormerlySerializedAs("enableBugly")]
        public bool EnableBugly = true;
        [FormerlySerializedAs("buglyAppId")]
        public string BuglyAppId = "";
        [FormerlySerializedAs("buglyAppKey")]
        public string BuglyAppKey = "";
        [FormerlySerializedAs("buglyRegion")]
        public BuglyRegion BuglyRegion = BuglyRegion.Overseas;
        [FormerlySerializedAs("buglyDebugMode")]
        public bool BuglyDebugMode;
        [Tooltip("输出 Bugly 原生 VERBOSE 日志以排查配置、采集和上报；正式发布建议关闭。独立于 Debug Mode。" )]
        [FormerlySerializedAs("buglyEnableNativeLogs")]
        public bool BuglyEnableNativeLogs;
        [Tooltip("过滤回环和局域网地址（包括192.168.*）；局域网采集测试时关闭。" )]
        [FormerlySerializedAs("buglyFilterLocalRequests")]
        public bool BuglyFilterLocalRequests = false;

        [Header("数数埋点")]
        [FormerlySerializedAs("enableThinkingData")]
        public bool EnableThinkingData = true;
        [Tooltip("仅当游戏已自行初始化数数时勾选；SDK 不再覆盖该实例的 Mode/TimeZone/日志/网络配置。")]
        [FormerlySerializedAs("reuseThinkingDataInstance")]
        public bool ReuseThinkingDataInstance;
        [FormerlySerializedAs("thinkingAppId")]
        public string ThinkingAppId = "";
        [FormerlySerializedAs("thinkingServerUrl")]
        public string ThinkingServerUrl = "";
        [FormerlySerializedAs("thinkingMode")]
        public TDMode ThinkingMode = TDMode.Normal;
        [FormerlySerializedAs("thinkingTimeZone")]
        public TDTimeZone ThinkingTimeZone = TDTimeZone.Local;
        [Tooltip("TimeZone=Other 时填写数数支持的时区 ID。")]
        [FormerlySerializedAs("thinkingTimeZoneId")]
        public string ThinkingTimeZoneId = "";
        [FormerlySerializedAs("thinkingNetworkType")]
        public TDNetworkType ThinkingNetworkType = TDNetworkType.All;
        [FormerlySerializedAs("thinkingEnableLog")]
        public bool ThinkingEnableLog;

        [Header("默认请求参数（单次请求仍可覆盖）")]
        [FormerlySerializedAs("defaultRequest")]
        public RequestOptions DefaultRequest = new RequestOptions();

        [Header("测试与日志")]
        [FormerlySerializedAs("enableEditorAnalytics")]
        public bool EnableEditorAnalytics;
        [Tooltip("SDK 信息日志开关；失败日志和规范要求的 net_cost 确认日志仍保留。")]
        [FormerlySerializedAs("enableSdkLogs")]
        public bool EnableSdkLogs = true;

        // Source compatibility; Unity serializes the PascalCase fields above.
        [Obsolete("Use InitializeOnStartup instead.")]
        public bool initializeOnStartup { get { return InitializeOnStartup; } set { InitializeOnStartup = value; } }
        [Obsolete("Use EnableBugly instead.")]
        public bool enableBugly { get { return EnableBugly; } set { EnableBugly = value; } }
        [Obsolete("Use BuglyAppId instead.")]
        public string buglyAppId { get { return BuglyAppId; } set { BuglyAppId = value; } }
        [Obsolete("Use BuglyAppKey instead.")]
        public string buglyAppKey { get { return BuglyAppKey; } set { BuglyAppKey = value; } }
        [Obsolete("Use BuglyRegion instead.")]
        public BuglyRegion buglyRegion { get { return BuglyRegion; } set { BuglyRegion = value; } }
        [Obsolete("Use BuglyDebugMode instead.")]
        public bool buglyDebugMode { get { return BuglyDebugMode; } set { BuglyDebugMode = value; } }
        [Obsolete("Use BuglyEnableNativeLogs instead.")]
        public bool buglyEnableNativeLogs { get { return BuglyEnableNativeLogs; } set { BuglyEnableNativeLogs = value; } }
        [Obsolete("Use BuglyFilterLocalRequests instead.")]
        public bool buglyFilterLocalRequests { get { return BuglyFilterLocalRequests; } set { BuglyFilterLocalRequests = value; } }
        [Obsolete("Use EnableThinkingData instead.")]
        public bool enableThinkingData { get { return EnableThinkingData; } set { EnableThinkingData = value; } }
        [Obsolete("Use ReuseThinkingDataInstance instead.")]
        public bool reuseThinkingDataInstance { get { return ReuseThinkingDataInstance; } set { ReuseThinkingDataInstance = value; } }
        [Obsolete("Use ThinkingAppId instead.")]
        public string thinkingAppId { get { return ThinkingAppId; } set { ThinkingAppId = value; } }
        [Obsolete("Use ThinkingServerUrl instead.")]
        public string thinkingServerUrl { get { return ThinkingServerUrl; } set { ThinkingServerUrl = value; } }
        [Obsolete("Use ThinkingMode instead.")]
        public TDMode thinkingMode { get { return ThinkingMode; } set { ThinkingMode = value; } }
        [Obsolete("Use ThinkingTimeZone instead.")]
        public TDTimeZone thinkingTimeZone { get { return ThinkingTimeZone; } set { ThinkingTimeZone = value; } }
        [Obsolete("Use ThinkingTimeZoneId instead.")]
        public string thinkingTimeZoneId { get { return ThinkingTimeZoneId; } set { ThinkingTimeZoneId = value; } }
        [Obsolete("Use ThinkingNetworkType instead.")]
        public TDNetworkType thinkingNetworkType { get { return ThinkingNetworkType; } set { ThinkingNetworkType = value; } }
        [Obsolete("Use ThinkingEnableLog instead.")]
        public bool thinkingEnableLog { get { return ThinkingEnableLog; } set { ThinkingEnableLog = value; } }
        [Obsolete("Use DefaultRequest instead.")]
        public RequestOptions defaultRequest { get { return DefaultRequest; } set { DefaultRequest = value; } }
        [Obsolete("Use EnableEditorAnalytics instead.")]
        public bool enableEditorAnalytics { get { return EnableEditorAnalytics; } set { EnableEditorAnalytics = value; } }
        [Obsolete("Use EnableSdkLogs instead.")]
        public bool enableSdkLogs { get { return EnableSdkLogs; } set { EnableSdkLogs = value; } }

        public bool Validate(out string error)
        {
            if (EnableBugly && (string.IsNullOrWhiteSpace(BuglyAppId) || string.IsNullOrWhiteSpace(BuglyAppKey)))
            { error = "请填写 Bugly AppID/AppKey，或关闭 enableBugly。"; return false; }
            if (EnableThinkingData)
            {
                if (string.IsNullOrWhiteSpace(ThinkingAppId)) { error = "请填写数数 AppID。"; return false; }
                if (!ReuseThinkingDataInstance)
                {
                    Uri uri;
                    if (!Uri.TryCreate(ThinkingServerUrl, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                    { error = "请填写有效的数数 HTTP(S) 数据接收地址。"; return false; }
                    if (ThinkingTimeZone == TDTimeZone.Other && string.IsNullOrWhiteSpace(ThinkingTimeZoneId))
                    { error = "TimeZone=Other 时需要填写时区 ID。"; return false; }
                }
            }
            try { CreateRequestOptions(); }
            catch (Exception) { error = "默认请求参数无效，请检查超时、重试次数及响应大小限制。"; return false; }
            error = ""; return true;
        }

        public RequestOptions CreateRequestOptions()
        {
            if (DefaultRequest == null) throw new InvalidOperationException("Missing default request options");
            return DefaultRequest.Snapshot();
        }

        public TDConfig CreateThinkingConfig()
        {
            return new TDConfig(ThinkingAppId, ThinkingServerUrl) {
                mode = ThinkingMode, timeZone = ThinkingTimeZone, timeZoneId = ThinkingTimeZoneId
            };
        }
    }
}
