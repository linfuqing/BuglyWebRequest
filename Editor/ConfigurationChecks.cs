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
            var config = new BuglySdkSettings();
            {
                string error;
                Check(!config.Validate(out error), "Empty credentials accepted");
                config.EnableBugly = false;
                config.ThinkingAppId = "configuration-test";
                config.ThinkingServerUrl = "https://example.invalid/collect";
                config.ThinkingMode = TDMode.DebugOnly;
                config.ThinkingTimeZone = TDTimeZone.UTC;
                Check(config.Validate(out error), "Valid configuration rejected");
                var td = config.CreateThinkingConfig();
                Check(td.mode == TDMode.DebugOnly && td.timeZone == TDTimeZone.UTC, "Mode/timezone overwritten by defaults");
                config.ThinkingTimeZone = TDTimeZone.Other;
                Check(!config.Validate(out error), "Missing custom timezone accepted");
                config.ThinkingTimeZoneId = "Asia/Shanghai";
                Check(config.Validate(out error) && config.CreateThinkingConfig().timeZoneId == "Asia/Shanghai", "Custom timezone mapping failed");
                config.DefaultRequest.Transport = HttpTransport.UrlConnection;
                config.DefaultRequest.TimeoutSeconds = 23;
                var options = config.CreateRequestOptions();
                Check(options.Transport == HttpTransport.UrlConnection && options.TimeoutSeconds == 23, "Request defaults lost");
                options.TimeoutSeconds = 2;
                Check(config.DefaultRequest.TimeoutSeconds == 23, "Per-request override mutated shared settings");
                config.ReuseThinkingDataInstance = true;
                config.ThinkingServerUrl = "";
                config.ThinkingTimeZoneId = "";
                Check(config.Validate(out error), "Reuse incorrectly requires fresh initialization fields");
                Debug.Log("[BuglyWebRequest] Configuration checks PASS: Mode/TimeZone/custom timezone/default request isolation/reuse/validation. No SDK initialized or network sent.");
            }

        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
