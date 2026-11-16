using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeroGame.Networking
{
    public static class BuglyUrlConfig
    {
        public const string ProjectResourceName = "BuglyWebRequestUrls";
        public const string DefaultResourceName = "BuglyWebRequestDefaultUrls";

        private static string[] cachedUrls;

        // Also reset between Editor play sessions when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            cachedUrls = null;
        }

        /// <summary>Loads and parses once per run; returns a copy so callers cannot change the cache.</summary>
        public static string[] Load()
        {
            return (string[])LoadCached().Clone();
        }

        // Trace already copies the URLs into its request, so it need not allocate another copy here.
        internal static string[] LoadCached()
        {
            if (cachedUrls != null) return cachedUrls;
            var asset = Resources.Load<TextAsset>(ProjectResourceName);
            if (asset == null) asset = Resources.Load<TextAsset>(DefaultResourceName);
            if (asset == null)
                throw new InvalidOperationException("[BuglyWebRequest][URL配置] 两份 URL 文本均未找到。请创建 Assets/Resources/" + ProjectResourceName + ".txt，或恢复 SDK 中的 Runtime/Resources/" + DefaultResourceName + ".txt；每行填写一个完整 HTTP(S) URL。");
            cachedUrls = Parse(asset.text, "Resources/" + asset.name + ".txt");
            return cachedUrls;
        }

        public static string[] Parse(string text)
        {
            return Parse(text, "传入的 URL 文本");
        }

        private static string[] Parse(string text, string source)
        {
            if (text == null) throw new ArgumentNullException("text", "[BuglyWebRequest][URL配置] " + source + " 为 null；请提供每行一个 HTTP(S) URL 的文本。");
            var urls = new List<string>();
            var lines = text.Split(new[] { '\n' });
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim().TrimStart('\uFEFF').Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal)) continue;
                string context = "[BuglyWebRequest][URL配置] " + source + " 第 " + (i + 1) + " 行：";
                foreach (char character in line)
                    if (char.IsWhiteSpace(character)) throw new FormatException(context + "URL 包含空白字符；请一行只填一个地址，不支持 URL 后的行内注释。");
                Uri uri;
                if (!Uri.TryCreate(line, UriKind.Absolute, out uri) || string.IsNullOrEmpty(uri.Host))
                    throw new FormatException(context + "不是包含主机名的完整 URL；格式应为 http://host/path 或 https://host/path。");
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                    throw new FormatException(context + "协议为 " + uri.Scheme + "；仅支持 http 和 https。");
                urls.Add(line);
            }
            if (urls.Count == 0) throw new FormatException("[BuglyWebRequest][URL配置] " + source + " 中没有有效地址，文件为空或仅含注释；请至少填写一个完整 HTTP(S) URL。");
            return urls.ToArray();
        }
    }
}
