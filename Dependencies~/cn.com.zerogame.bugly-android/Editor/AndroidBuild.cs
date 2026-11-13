#if UNITY_ANDROID
using System;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.PackageManager;
using UnityEngine;

namespace ZeroGame.Networking.Editor
{
    public sealed class AndroidBuild : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder { get { return 1500; } }
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AndroidBuild).Assembly);
            if (package == null) throw new InvalidOperationException("SDK package root not found");
            var javaPath = Path.Combine(path, "src/main/java/cn/zero3d/buglywebrequest");
            Directory.CreateDirectory(javaPath);
            foreach (var name in new[] { "WireBridge", "SdkBridge" })
                File.Copy(Path.Combine(package.resolvedPath, "Runtime/Android/" + name + ".java.txt"), Path.Combine(javaPath, name + ".java"), true);
            const string ns = "http://schemas.android.com/apk/res/android";
            var xml = new XmlDocument();
            var manifest = Path.Combine(path, "src/main/AndroidManifest.xml"); xml.Load(manifest);
            var app = xml.DocumentElement.SelectSingleNode("application") as XmlElement;
            if (app == null) throw new InvalidOperationException("Missing application in manifest");
            bool cleartext = PlayerSettings.insecureHttpOption == InsecureHttpOption.AlwaysAllowed ||
                (PlayerSettings.insecureHttpOption == InsecureHttpOption.DevelopmentOnly && EditorUserBuildSettings.development);
            app.SetAttribute("usesCleartextTraffic", ns, cleartext ? "true" : "false");
            foreach (var name in new[] { "android.permission.INTERNET", "android.permission.ACCESS_NETWORK_STATE" })
            {
                bool exists = false;
                foreach (XmlElement item in xml.DocumentElement.GetElementsByTagName("uses-permission")) exists |= item.GetAttribute("name", ns) == name;
                if (!exists) { var item = xml.CreateElement("uses-permission"); item.SetAttribute("name", ns, name); xml.DocumentElement.AppendChild(item); }
            }
            xml.Save(manifest);
            File.WriteAllText(Path.Combine(path, "bugly-webrequest.pro"),
                "-keep class cn.zero3d.buglywebrequest.** { *; }\n" +
                "-keep class com.tencent.bugly.library.** { public *; }\n" +
                "-keep class com.tencent.bugly.network.** { public *; }\n" +
                "-keep class okhttp3.sse.** { public *; }\n");
            var gradlePath = Path.Combine(path, "build.gradle");
            var gradle = File.ReadAllText(gradlePath);
            foreach (var coordinate in new[] { "com.tencent.bugly:bugly-pro:4.4.8.2", "com.squareup.okhttp3:okhttp:3.12.13", "com.squareup.okhttp3:okhttp-sse:3.12.13" })
                if (!gradle.Contains(coordinate)) gradle += "\ndependencies { implementation '" + coordinate + "' }\n";
            if (!gradle.Contains("consumerProguardFiles 'bugly-webrequest.pro'"))
                gradle += "\nandroid { defaultConfig { consumerProguardFiles 'bugly-webrequest.pro' } }\n";
            File.WriteAllText(gradlePath, gradle);
            Debug.Log("[BuglyWebRequest] Native sources/dependencies prepared; cleartext=" + cleartext);
        }
    }
}
#endif
