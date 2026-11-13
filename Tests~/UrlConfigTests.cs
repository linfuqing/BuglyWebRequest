using System;
using ZeroGame.Networking;
public static class UrlConfigTests
{
    public static int Main()
    {
        var urls = BuglyUrlConfig.Parse("\uFEFF# header\r\n\r\n https://example.com/a?x=1#part \r\n// skip\nhttp://192.168.1.48/connect.php\n");
        if (urls.Length != 2 || urls[0] != "https://example.com/a?x=1#part" || urls[1] != "http://192.168.1.48/connect.php") throw new Exception("Order, comments, BOM, whitespace or fragments failed");
        ExpectInvalid("# empty\n// comment");
        ExpectInvalid("ftp://example.com/file");
        ExpectInvalid("https://example.com\ninvalid");
        ExpectInvalid("https://example.com inline-comment");
        if (BuglyUrlConfig.Parse(System.IO.File.ReadAllText("Runtime/Resources/BuglyWebRequestDefaultUrls.txt")).Length != 3) throw new Exception("Expected three default endpoints");
        Console.WriteLine("PASS: multi-URL order, BOM/CRLF/comments, URL fragment preservation, invalid/empty rejection, three default URLs");
        return 0;
    }
    private static void ExpectInvalid(string text)
    {
        try { BuglyUrlConfig.Parse(text); }
        catch (FormatException) { return; }
        throw new Exception("Invalid config accepted");
    }
}
