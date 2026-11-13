using System.Collections;
using UnityEngine;
using ZeroGame.Networking;

public sealed class BuglyWebRequestDemo : MonoBehaviour
{
    public BuglySdkSettings settings;
    public string api = "test_post";
    public string[] urls = new string[0];
    private string urlText = "";
    private HttpTransport transport;
    private BuglyWebRequest active;
    private string status = "先在 Inspector 填写 SDK 配置；编辑器请求使用 UnityWebRequest。";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot() { if (FindObjectOfType<BuglyWebRequestDemo>() == null) new GameObject("Bugly SDK Demo").AddComponent<BuglyWebRequestDemo>(); }
    private void Awake()
    {
        if (settings == null) settings = BuglySdkManager.CurrentSettings;
        if (settings != null) transport = settings.defaultRequest.transport;
        urlText = string.Join("\n", urls ?? new string[0]);
    }
    private void OnGUI()
    {
        float scale = Mathf.Max(0.1f, Mathf.Min(Screen.width / 800f, Screen.height / 850f));
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        GUILayout.BeginArea(new Rect(15, 15, 750, 720), GUI.skin.box);
        GUILayout.Label("Bugly WebRequest SDK 0.3.0 — 统一配置测试");
        GUILayout.Label(settings == null ? "未找到配置：Tools > Bugly WebRequest > Create or Select SDK Configuration" : "配置：" + settings.name);
        if (settings != null)
        {
            GUILayout.Label("数数 Mode=" + settings.thinkingMode + "，TimeZone=" + settings.thinkingTimeZone + "，复用实例=" + settings.reuseThinkingDataInstance);
            GUILayout.Label("启动初始化=" + settings.initializeOnStartup + "，Bugly=" + BuglySdk.IsInitialized + "，数数=" + BuglySdk.ThinkingDataReady);
            if (GUILayout.Button("手动初始化 / 重试初始化 SDK"))
            {
                var manager = BuglySdkManager.Instance;
                manager.settings = settings;
                status = manager.InitializeAfterConsent() ? "当前平台 SDK 初始化流程完成。Bugly=" + BuglySdk.IsInitialized + "，数数=" + BuglySdk.ThinkingDataReady : manager.LastError;
            }
        }
        GUILayout.Label("api 埋点名"); api = GUILayout.TextField(api);
        GUILayout.Label("每行一个 URL（Inspector 中为 string[] 数组）");
        urlText = GUILayout.TextArea(urlText, GUILayout.Height(100));
        urls = urlText.Split(new[] {'\r','\n'}, System.StringSplitOptions.RemoveEmptyEntries);
        transport = (HttpTransport)GUILayout.SelectionGrid((int)transport, new[] { "OkHttp", "URLConnection" }, 2);
        GUI.enabled = active == null;
        if (GUILayout.Button("发送测试表单 function=111")) StartCoroutine(Send());
        GUI.enabled = true;
        if (active != null && GUILayout.Button("取消请求")) active.Abort();
        GUILayout.Label(status); GUILayout.EndArea();
    }
    private IEnumerator Send()
    {
        var form = new WWWForm(); form.AddField("function", 111);
        BuglyWebRequest request;
        try { request = BuglyWebRequest.Post(api, form, urls); }
        catch (System.Exception error) { status = error.Message; yield break; }
        active = request;
        using (request)
        {
            request.Options.transport = transport;
            request.UrlCompleted += response => status = response.Index + ": success=" + response.Success +
                " attempts=" + response.Attempts + " ms=" + response.DurationMs + " reason=" + response.FailureReason;
            yield return request.SendWebRequest();
        }
        active = null;
    }
    private void OnDestroy() { if (active != null) active.Dispose(); }
}
