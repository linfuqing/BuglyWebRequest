using System.Collections;
using UnityEngine.Serialization;
using UnityEngine;
using ZeroGame.Networking;

public sealed class BuglyWebRequestDemo : MonoBehaviour
{
    [FormerlySerializedAs("settings")]
    public BuglySdkSettings Settings;
    [FormerlySerializedAs("api")]
    public string Api = "test_post";
    public string[] Urls = new string[0];
    private bool useTextConfig = true;
    private string explicitUrlText = "";
    private string[] urls = new string[0];
    private string urlText = "";
    private HttpTransport transport;
    private BuglyWebRequest active;
    private string status = "先在 Inspector 填写 SDK 配置；编辑器请求使用 UnityWebRequest。";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot() { if (FindObjectOfType<BuglyWebRequestDemo>() == null) new GameObject("Bugly SDK Demo").AddComponent<BuglyWebRequestDemo>(); }
    private void Awake()
    {
        if (Settings == null) Settings = BuglySdkManager.CurrentSettings;
        if (Settings != null) transport = Settings.DefaultRequest.Transport;
        explicitUrlText = string.Join("\n", Urls ?? new string[0]);
        TryLoadUrls();
    }
    private bool TryLoadUrls()
    {
        try
        {
            urls = BuglyUrlConfig.Load();
            urlText = string.Join("\n", urls);
            return true;
        }
        catch (System.Exception error)
        {
            urls = new string[0];
            urlText = "";
            status = "URL 文本配置错误：" + error.Message;
            Debug.LogError("[BuglyWebRequestDemo] " + status);
            return false;
        }
    }
    private void OnGUI()
    {
        float scale = Mathf.Max(0.1f, Mathf.Min(Screen.width / 800f, Screen.height / 850f));
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        GUILayout.BeginArea(new Rect(15, 15, 750, 720), GUI.skin.box);
        GUILayout.Label("Bugly WebRequest SDK 0.3.3 — 统一配置测试");
        GUILayout.Label(Settings == null ? "未找到配置：Tools > Bugly WebRequest > Create or Select SDK Configuration" : "配置：" + Settings.name);
        if (Settings != null)
        {
            GUILayout.Label("数数 Mode=" + Settings.ThinkingMode + "，TimeZone=" + Settings.ThinkingTimeZone + "，复用实例=" + Settings.ReuseThinkingDataInstance);
            GUILayout.Label("启动初始化=" + Settings.InitializeOnStartup + "，Bugly=" + BuglySdk.IsInitialized + "，数数=" + BuglySdk.ThinkingDataReady);
            if (GUILayout.Button("手动初始化 / 重试初始化 SDK"))
            {
                var manager = BuglySdkManager.Instance;
                manager.Settings = Settings;
                status = manager.InitializeAfterConsent() ? "当前平台 SDK 初始化流程完成。Bugly=" + BuglySdk.IsInitialized + "，数数=" + BuglySdk.ThinkingDataReady : manager.LastError;
            }
        }
        GUILayout.Label("api 埋点名"); Api = GUILayout.TextField(Api);
        useTextConfig = GUILayout.Toggle(useTextConfig, "文本 URL 自动发送（取消勾选：显式 URL 获取响应）");
        if (useTextConfig)
        {
            GUILayout.Label("文本配置，共 " + urls.Length + " 个 URL；传输方式使用 SDK 默认配置");
            GUILayout.Label(urlText, GUI.skin.textArea, GUILayout.Height(100));
            if (GUILayout.Button("重新读取 URL 文本")) TryLoadUrls();
        }
        else
        {
            GUILayout.Label("显式 URL：每行一个，不读取文本配置");
            explicitUrlText = GUILayout.TextArea(explicitUrlText, GUILayout.Height(100));
            Urls = explicitUrlText.Split(new[] {'\r', '\n'}, System.StringSplitOptions.RemoveEmptyEntries);
            transport = (HttpTransport)GUILayout.SelectionGrid((int)transport, new[] { "OkHttp", "URLConnection" }, 2);
        }
        GUI.enabled = active == null;
        if (GUILayout.Button("发送测试表单 function=111")) StartCoroutine(Send());
        GUI.enabled = true;
        if (active != null && GUILayout.Button("取消请求")) active.Abort();
        GUILayout.Label(status); GUILayout.EndArea();
    }
    private IEnumerator Send()
    {
        bool textMode = useTextConfig;
        if (textMode && !TryLoadUrls()) yield break;
        var form = new WWWForm(); form.AddField("function", 111);
        BuglyWebRequest request;
        try { request = textMode ? BuglyWebRequest.Post(Api, form) : BuglyWebRequest.Post(Api, form, Urls); }
        catch (System.Exception error) { status = error.Message; yield break; }
        active = request;
        using (request)
        {
            if (!textMode) request.Options.Transport = transport;
            request.UrlCompleted += response => status = response.Index + ": success=" + response.Success +
                " attempts=" + response.Attempts + " ms=" + response.DurationMs + " reason=" + response.FailureReason;
            if (textMode) yield return new WaitUntil(() => request.IsDone);
            else yield return request.SendWebRequest();
        }
        active = null;
    }
    private void OnDestroy() { if (active != null) active.Dispose(); }
}
