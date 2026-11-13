using System;
using UnityEngine;
using ThinkingData.Analytics;

namespace ZeroGame.Networking
{
    public enum BuglyRegion { Overseas, Domestic }

    [Serializable]
    public sealed class BuglySdkSettings
    {
        [Header("启动初始化" )]
        [Tooltip("下载前已获得隐私同意的项目可启用；不会写入游戏内点击同意记录。" )]
        public bool InitializeOnStartup = true;

        [Header("Bugly 网络监控" )]
        public bool EnableBugly = true;
        public string BuglyAppId = "";
        public string BuglyAppKey = "";
        public BuglyRegion BuglyRegion = BuglyRegion.Overseas;
        public bool BuglyDebugMode;
        [Tooltip("输出 Bugly 原生 VERBOSE 日志以排查配置、采集和上报；正式发布建议关闭。独立于 Debug Mode。" )]
        public bool BuglyEnableNativeLogs;
        [Tooltip("过滤回环和局域网地址（包括192.168.*）；局域网采集测试时关闭。" )]
        public bool BuglyFilterLocalRequests = false;

        [Header("数数埋点")]
        public bool EnableThinkingData = true;
        [Tooltip("仅当游戏已自行初始化数数时勾选；SDK 不再覆盖该实例的 Mode/TimeZone/日志/网络配置。")]
        public bool ReuseThinkingDataInstance;
        public string ThinkingAppId = "";
        public string ThinkingServerUrl = "";
        public TDMode ThinkingMode = TDMode.Normal;
        public TDTimeZone ThinkingTimeZone = TDTimeZone.Local;
        [Tooltip("TimeZone=Other 时填写数数支持的时区 ID。")]
        public string ThinkingTimeZoneId = "";
        public TDNetworkType ThinkingNetworkType = TDNetworkType.All;
        public bool ThinkingEnableLog;

        [Header("默认请求参数（单次请求仍可覆盖）")]
        public RequestOptions DefaultRequest = new RequestOptions();

        [Header("测试与日志")]
        public bool EnableEditorAnalytics;
        [Tooltip("SDK 信息日志开关；失败日志和规范要求的 net_cost 确认日志仍保留。")]
        public bool EnableSdkLogs = true;

        public bool Validate(out string error)
        {
            if (EnableBugly && string.IsNullOrWhiteSpace(BuglyAppId))
            { error = "EnableBugly 已开启，但 BuglyAppId 为空；请在场景中的 Bugly SDK 组件填写 AppID。"; return false; }
            if (EnableBugly && string.IsNullOrWhiteSpace(BuglyAppKey))
            { error = "EnableBugly 已开启，但 BuglyAppKey 为空；请填写对应 AppKey，或关闭 EnableBugly。"; return false; }
            if (EnableBugly && !Enum.IsDefined(typeof(BuglyRegion), BuglyRegion))
            { error = "BuglyRegion 无效；请选择 Overseas 或 Domestic。"; return false; }
            if (EnableThinkingData)
            {
                if (string.IsNullOrWhiteSpace(ThinkingAppId)) { error = "EnableThinkingData 已开启，但 ThinkingAppId 为空；请填写需要上报的数数项目 AppID。"; return false; }
                if (!ReuseThinkingDataInstance)
                {
                    Uri uri;
                    if (!Uri.TryCreate(ThinkingServerUrl, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https") || string.IsNullOrEmpty(uri.Host))
                    { error = "ThinkingServerUrl 无效；请填写含主机名的完整 HTTP(S) 数据接收地址。已自行初始化数数时可勾选 ReuseThinkingDataInstance。"; return false; }
                    if (ThinkingTimeZone == TDTimeZone.Other && string.IsNullOrWhiteSpace(ThinkingTimeZoneId))
                    { error = "ThinkingTimeZone 为 Other，但 ThinkingTimeZoneId 为空；请填写数数支持的时区 ID，或改选 Local/UTC。"; return false; }
                }
            }
            try { CreateRequestOptions(); }
            catch (Exception cause) { error = "DefaultRequest 配置无效：" + cause.Message; return false; }
            error = ""; return true;
        }

        public RequestOptions CreateRequestOptions()
        {
            if (DefaultRequest == null) throw new InvalidOperationException("[BuglyWebRequest] 场景 SDK 组件的 DefaultRequest 为 null；请恢复默认请求配置后再初始化或发送请求。");
            return DefaultRequest.Snapshot();
        }

        internal BuglySdkSettings Snapshot()
        {
            var copy = (BuglySdkSettings)MemberwiseClone();
            copy.DefaultRequest = CreateRequestOptions();
            return copy;
        }

        public TDConfig CreateThinkingConfig()
        {
            return new TDConfig(ThinkingAppId, ThinkingServerUrl) {
                mode = ThinkingMode, timeZone = ThinkingTimeZone, timeZoneId = ThinkingTimeZoneId
            };
        }
    }
}
