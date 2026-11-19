using System;
using UnityEngine;
using ZeroGame.Networking;

// Attach to a test scene and wire these methods to UI buttons. Never throws automatically.
public sealed class BuglyExceptionDemo : MonoBehaviour
{
    public void TestUnityException() { throw new InvalidOperationException("BUGLY_CSHARP_TEST: Unity unhandled exception"); }
    public void TestUnityLogError() { Debug.LogError("BUGLY_CSHARP_TEST: Unity LogError"); }
    public void TestCaughtException()
    {
        try { throw new InvalidOperationException("BUGLY_CSHARP_TEST: caught exception"); }
        catch (Exception error) { BuglySdk.ReportException(error); }
    }
}
