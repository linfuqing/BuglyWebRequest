using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ZeroGame.Networking.Editor
{
    internal static class GradleDependencyUtility
    {
        private static readonly string[] Defaults = {
            "com.tencent.bugly_16kb:bugly-pro:4.4.8.2",
            "com.squareup.okhttp3:okhttp:3.12.13",
            "com.squareup.okhttp3:okhttp-sse:3.12.13"
        };

        internal static string EnsureDependencies(string gradle, Action<string> log, Action<string> warning = null)
        {
            if (gradle == null) throw new ArgumentNullException("gradle", "[BuglyAndroid] Gradle 文件内容不能为 null。");
            bool legacyBugly = ContainsDependency(gradle, "com.tencent.bugly", "bugly-pro");
            bool bugly16k = ContainsDependency(gradle, "com.tencent.bugly_16kb", "bugly-pro");
            if (legacyBugly && bugly16k)
                throw new InvalidOperationException("[BuglyAndroid][16KB] Gradle 同时声明了普通版 com.tencent.bugly:bugly-pro 和 16 KB 版 com.tencent.bugly_16kb:bugly-pro，两者含重复 Java 类和原生库。请仅保留一个制品；发布 Google Play 应使用 16 KB 版。SDK 未自动删除使用者的依赖。");
            if (legacyBugly)
            {
                var report = warning ?? log;
                if (report != null) report("[BuglyWebRequest][16KB] 工程已显式声明普通版 com.tencent.bugly:bugly-pro。SDK 保留现有版本，不另加重复制品；普通版不能据此认定支持 16 KB。发布 Google Play 请将该声明改为 com.tencent.bugly_16kb:bugly-pro，并检查最终包。");
            }
            foreach (var coordinate in Defaults)
            {
                var parts = coordinate.Split(':');
                string module = parts[0] + ":" + parts[1];
                if ((parts[0] == "com.tencent.bugly_16kb" && legacyBugly) || ContainsDependency(gradle, parts[0], parts[1]))
                {
                    if (log != null) log("[BuglyWebRequest] Gradle 已声明 " + (parts[0] == "com.tencent.bugly_16kb" && legacyBugly ? "com.tencent.bugly:bugly-pro" : module) + "，保留工程版本，不重复添加。");
                    continue;
                }
                gradle += "\ndependencies { implementation '" + coordinate + "' }\n";
                if (log != null) log("[BuglyWebRequest] Gradle 补充缺失依赖 " + coordinate);
            }
            return gradle;
        }

        internal static bool ContainsDependency(string gradle, string group, string module)
        {
            string code = RemoveComments(gradle);
            // The boundary prevents okhttp-sse from being mistaken for okhttp.
            string coordinate = Regex.Escape(group + ":" + module);
            if (Regex.IsMatch(code, "[\"']" + coordinate + "(?=[:\"'])")) return true;
            // Groovy map notation, including multiline parenthesized calls and variable versions.
            const string calls = @"\b(?:implementation|api|compile|compileOnly|runtimeOnly|[A-Za-z_$]\w*(?:Implementation|Api|CompileOnly|RuntimeOnly))\s*(?:\((?<args>[\s\S]*?)\)\s*|(?<args>[^\r\n;{}]+))";
            foreach (Match call in Regex.Matches(code, calls))
            {
                string args = call.Groups["args"].Value;
                if (Regex.IsMatch(args, "\\bgroup\\s*:\\s*[\"']" + Regex.Escape(group) + "[\"']") &&
                    Regex.IsMatch(args, "\\bname\\s*:\\s*[\"']" + Regex.Escape(module) + "[\"']")) return true;
            }
            return false;
        }

        private static string RemoveComments(string source)
        {
            var result = new StringBuilder(source.Length);
            char quote = '\0';
            bool escaped = false, triple = false, lineComment = false, blockComment = false;
            for (int i = 0; i < source.Length; i++)
            {
                char current = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';
                if (lineComment)
                {
                    result.Append(current == '\n' || current == '\r' ? current : ' ');
                    if (current == '\n' || current == '\r') lineComment = false;
                    continue;
                }
                if (blockComment)
                {
                    if (current == '*' && next == '/') { result.Append("  "); i++; blockComment = false; }
                    else result.Append(current == '\n' || current == '\r' ? current : ' ');
                    continue;
                }
                if (quote != '\0')
                {
                    result.Append(current);
                    if (escaped) { escaped = false; continue; }
                    if (current == '\\') { escaped = true; continue; }
                    if (current == quote)
                    {
                        if (!triple) quote = '\0';
                        else if (next == quote && i + 2 < source.Length && source[i + 2] == quote)
                        { result.Append(next).Append(next); i += 2; quote = '\0'; triple = false; }
                    }
                    continue;
                }
                if (current == '/' && next == '/') { result.Append("  "); i++; lineComment = true; continue; }
                if (current == '/' && next == '*') { result.Append("  "); i++; blockComment = true; continue; }
                result.Append(current);
                if (current == '\'' || current == '"')
                {
                    quote = current;
                    if (next == quote && i + 2 < source.Length && source[i + 2] == quote)
                    { result.Append(next).Append(next); i += 2; triple = true; }
                }
            }
            return result.ToString();
        }
    }
}
