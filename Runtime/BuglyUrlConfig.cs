using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeroGame.Networking
{
    /// <summary>Loads HTTP endpoints from a UTF-8 TextAsset on the Unity main thread.</summary>
    public static class BuglyUrlConfig
    {
        public const string ProjectResourceName = "BuglyWebRequestUrls";
        public const string DefaultResourceName = "BuglyWebRequestDefaultUrls";

        public static string[] Load()
        {
            var asset = Resources.Load<TextAsset>(ProjectResourceName);
            if (asset == null) asset = Resources.Load<TextAsset>(DefaultResourceName);
            if (asset == null)
                throw new InvalidOperationException("URL text configuration was not found. Expected Resources/" + DefaultResourceName + ".txt");
            return Parse(asset.text);
        }

        public static string[] Parse(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            var urls = new List<string>();
            var lines = text.Split(new[] { '\n' });
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim().TrimStart('\uFEFF').Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) ||
                    line.StartsWith("//", StringComparison.Ordinal)) continue;
                Uri uri;
                if (!Uri.TryCreate(line, UriKind.Absolute, out uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                    string.IsNullOrEmpty(uri.Host) || line.IndexOfAny(new[] { ' ', '\t', '\r' }) >= 0)
                    throw new FormatException("Invalid HTTP(S) URL on configuration line " + (i + 1));
                urls.Add(line);
            }
            if (urls.Count == 0) throw new FormatException("URL configuration has no HTTP(S) URLs");
            return urls.ToArray();
        }
    }
}
