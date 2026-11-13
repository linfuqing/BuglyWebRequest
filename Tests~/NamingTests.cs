using System;
using System.Reflection;
using UnityEngine.Serialization;
using ZeroGame.Networking;
public static class NamingTests
{
    public static int Main()
    {
        int fields = CheckFields(typeof(RequestOptions)) + CheckFields(typeof(BuglySdkSettings)) + CheckFields(typeof(BuglySdkManager));
        if (fields != 28) throw new Exception("Expected 28 serialized field migrations, got " + fields);
        var options = new RequestOptions();
#pragma warning disable 618
        options.transport = HttpTransport.UrlConnection;
        options.timeoutSeconds = 19;
        options.maxAttempts = 4;
        options.retryDelayMs = 50;
        options.maxResponseBytes = 1024;
        options.reportNetCost = false;
#pragma warning restore 618
        if (options.Transport != HttpTransport.UrlConnection || options.TimeoutSeconds != 19 || options.MaxAttempts != 4 || options.RetryDelayMs != 50 || options.MaxResponseBytes != 1024 || options.ReportNetCost) throw new Exception("Legacy aliases diverged");
        var copy = typeof(RequestOptions).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(options, null) as RequestOptions;
        copy.TimeoutSeconds = 5;
        if (options.TimeoutSeconds != 19) throw new Exception("Snapshot isolation failed");
        foreach(var name in new[] { "IsDone", "Progress", "DownloadHandler", "ResponseCode", "Error", "Result", "Timeout" })
            if (typeof(BuglyWebRequest).GetProperty(name) == null) throw new Exception("Missing request property " + name);
        if (typeof(BuglyWebRequestOperation).GetEvent("Completed") == null || typeof(BuglyDownloadHandler).GetProperty("Data") == null) throw new Exception("Missing consistent API");
        Console.WriteLine("PASS: 28 serialized migrations, obsolete source aliases, option passthrough/isolation, PascalCase request APIs");
        return 0;
    }
    private static int CheckFields(Type type)
    {
        int count = 0;
        foreach(var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var attribute = (FormerlySerializedAsAttribute)Attribute.GetCustomAttribute(field, typeof(FormerlySerializedAsAttribute));
            if (attribute == null) continue;
            if (!char.IsUpper(field.Name[0])) throw new Exception("Lowercase field " + field.Name);
            var legacy = type.GetProperty(attribute.oldName);
            if (legacy == null || !Attribute.IsDefined(legacy, typeof(ObsoleteAttribute))) throw new Exception("Missing legacy alias " + attribute.oldName);
            count++;
        }
        return count;
    }
}
