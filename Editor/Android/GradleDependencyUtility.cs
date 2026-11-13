using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ZeroGame.Networking.Editor
{
    internal static class GradleDependencyUtility
    {
        private static readonly string[] Defaults = {
            "com.tencent.bugly:bugly-pro:4.4.8.2",
            "com.squareup.okhttp3:okhttp:3.12.13",
            "com.squareup.okhttp3:okhttp-sse:3.12.13"
        };

        internal static string EnsureDependencies(string gradle, Action<string> log)
        {
            if (gradle == null) throw new ArgumentNullException("gradle", "[BuglyAndroid] Gradle 文件内容不能为 null。");
            foreach (var coordinate in Defaults)
            {
                var parts = coordinate.Split(':');
                string module = parts[0] + ":" + parts[1];
                if (ContainsDependency(gradle, parts[0], parts[1]))
                {
                    if (log != null) log("[BuglyWebRequest] Gradle 已声明 " + module + "，保留工程版本，不重复添加。");
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
