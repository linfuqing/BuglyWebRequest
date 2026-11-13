using System;
using UnityEditor;
using UnityEngine;
using ThinkingData.Analytics;

namespace ZeroGame.Networking.Editor
{
    public static class ConfigurationChecks
    {
        [MenuItem("Tools/Bugly WebRequest/Verify Configuration Mapping")]
        public static void Run()
        {
            var config = ScriptableObject.CreateInstance<BuglySdkSettings>();
            try
            {
                string error;
                Check(!config.Validate(out error), "Empty credentials accepted");
                config.enableBugly = false;
                config.thinkingAppId = "configuration-test";
                config.thinkingServerUrl = "https://example.invalid/collect";
                config.thinkingMode = TDMode.DebugOnly;
                config.thinkingTimeZone = TDTimeZone.UTC;
                Check(config.Validate(out error), "Valid configuration rejected");
                var td = config.CreateThinkingConfig();
                Check(td.mode == TDMode.DebugOnly && td.timeZone == TDTimeZone.UTC, "Mode/timezone overwritten by defaults");
                config.thinkingTimeZone = TDTimeZone.Other;
                Check(!config.Validate(out error), "Missing custom timezone accepted");
                config.thinkingTimeZoneId = "Asia/Shanghai";
                Check(config.Validate(out error) && config.CreateThinkingConfig().timeZoneId == "Asia/Shanghai", "Custom timezone mapping failed");
                config.defaultRequest.transport = HttpTransport.UrlConnection;
                config.defaultRequest.timeoutSeconds = 23;
                var options = config.CreateRequestOptions();
                Check(options.transport == HttpTransport.UrlConnection && options.timeoutSeconds == 23, "Request defaults lost");
                options.timeoutSeconds = 2;
                Check(config.defaultRequest.timeoutSeconds == 23, "Per-request override mutated shared settings");
                config.reuseThinkingDataInstance = true;
                config.thinkingServerUrl = "";
                config.thinkingTimeZoneId = "";
                Check(config.Validate(out error), "Reuse incorrectly requires fresh initialization fields");
                Debug.Log("[BuglyWebRequest] Configuration checks PASS: Mode/TimeZone/custom timezone/default request isolation/reuse/validation. No SDK initialized or network sent.");
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
