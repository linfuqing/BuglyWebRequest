# Bugly WebRequest + ThinkingData — UPM 0.3.3

Unity 2022.3+。Android 使用 OkHttp / URLConnection，Editor、WebGL 和桌面使用 UnityWebRequest。
保留表单 POST、字节响应、每 URL 独立重试与最终结果一次 net_cost 上报。

## 通过 Git URL 安装

发布到 GitHub 后，在 Unity 的 Window > Package Manager > + > Add package from git URL 中，**按顺序**添加：

1. Bugly Android 适配包：
   `https://github.com/linfuqing/BuglyWebRequest.git?path=/Dependencies~/cn.com.zerogame.bugly-android`
2. 数数 3.5.0：
   `https://github.com/linfuqing/BuglyWebRequest.git?path=/Dependencies~/com.thinkingdata.analytics`
3. 主 SDK：
   `https://github.com/linfuqing/BuglyWebRequest.git`

Unity 需要本机安装 Git。如果已有同名 embedded 包，先备份并移走 Packages 下旧包文件夹，避免覆盖 Git 包。
已有数数 3.5.0 时无需重复安装；已有其他版本时先评估兼容性，不自动覆盖。
Unity 不支持在 package.json 的 dependencies 中写 Git URL，因此必须先安装两个依赖。
主包 Dependencies 将显示 TDAnalytics 3.5.0 和 Bugly Android Network Adapter 4.4.8。
Bugly 的 Maven 原生 SDK 4.4.8.2、OkHttp 与 SSE 在 Android 构建时下载，不是独立 UPM 项目。

团队也可在项目 Packages/manifest.json 的 dependencies 中一次声明三个 Git URL：

```json
{
  "dependencies": {
    "cn.com.zerogame.bugly-android": "https://github.com/linfuqing/BuglyWebRequest.git?path=/Dependencies~/cn.com.zerogame.bugly-android",
    "com.thinkingdata.analytics": "https://github.com/linfuqing/BuglyWebRequest.git?path=/Dependencies~/com.thinkingdata.analytics",
    "cn.com.zerogame.bugly-webrequest": "https://github.com/linfuqing/BuglyWebRequest.git"
  }
}
```

将上述键合并到现有 dependencies，保留项目原有依赖。正式交付建议所有 URL 都追加相同的 `#完整提交SHA`，固定同一仓库快照。

## URL 来源与发送方式（0.3.3）

三种入口均在 Unity 主线程调用：

| 调用 | URL 来源 | 发送方式 |
|---|---|---|
| `Post(api, form)` | 文本配置 | 自动发送，不需要业务回调 |
| `Post(api, form, urls, onComplete)` | 传入的 string[] | 自动发送，每个 URL 最终回调一次 |
| `Post(api, form, urls)` | 传入的 string[] / params | 先配置，调用 SendWebRequest() 才发送 |

```csharp
// 不需要业务回调：读取文本并自动发送，SDK 负责完成后的释放。
BuglyWebRequest.Post("telemetry", form);

// 短连接：只使用显式地址，不读取文本；自动发送并返回结果。
string[] urls = { "https://your-server.example/connect.php" };
BuglyWebRequest.Post("query_level", form, urls, response =>
{
    Debug.Log(response.Success);
    // response.Data 是该 URL 返回的 byte[]。
});
```

自动发送的入口不要再调用 SendWebRequest()，也不要在刚调用后立即 using/Dispose，否则会取消正在进行的请求。自动入口使用 SDK 默认请求选项，返回对象仍可用于 Abort()。
需要逐次设置超时、传输方式等参数时，使用显式 urls 的手动发送入口，示例见下方。
所有 URL 都独立重试和上报；多 URL 回调也是每个地址一次，不会因其中一个成功而忽略其他地址。
显式 urls 为 null、空数组或包含非法地址时直接报错，不回退到文本。

文本入口读取 Git 包内 `Runtime/Resources/BuglyWebRequestDefaultUrls.txt`，UTF-8 编码，每行一个完整 HTTP(S) URL，支持空行和整行 # / // 注释。
使用方的 `Assets/Resources/BuglyWebRequestUrls.txt` 优先于包内默认配置，请保持资源名唯一。
修改 Git 文本需更新包并重新构建 Player，不是远程热更新。短连接的显式 URL 不受文本内容影响。
这些入口都是 HTTP 请求；不提供回调不等于建立 WebSocket 或 Unity Transport 长连接，也不会改变 HTTP Keep-Alive 协商。

## 导入 Demo 与配置

选中 Bugly WebRequest + ThinkingData，展开 Samples，点击 Bugly WebRequest Demo 的 Import。
示例导入到 Assets/Samples；点击 Play 会自动出现测试面板。导入前不会自动加入游戏。
通过 Tools > Bugly WebRequest > Create or Select SDK Configuration 创建项目自己的配置，填写 Bugly AppID/AppKey、地区、数数 AppID/ServerUrl、Mode 和 TimeZone。
配置属于使用方 Assets/Resources/BuglyWebRequestSettings.asset，不存入 SDK 仓库。
InitializeOnStartup 仅用于项目已取得玩家隐私同意的情况；否则关闭该选项，在自己的同意流程后调用 BuglySdkManager.Instance.InitializeAfterConsent()。
LAN 过滤默认关闭，可测试局域网；HTTP 请求另需在 Player Settings 配置 Allow downloads over HTTP。

## 调用示例

```csharp
using ZeroGame.Networking;
// 放入你自己的 IEnumerator 方法内：
var form = new UnityEngine.WWWForm();
form.AddField("function", 111);
using (var request = BuglyWebRequest.Post("test_post", form, urls))
{
    request.Options.Transport = HttpTransport.OkHttp;
    request.UrlCompleted += response => UnityEngine.Debug.Log(
        "success=" + response.Success + " attempts=" + response.Attempts);
    yield return request.SendWebRequest();
}
```

手动发送示例中的 urls 是调用方提供的 string[]；每个 URL 独立执行和重试，业务返回字节内容由游戏解析。
SDK 的网络成功状态不根据业务响应中的 true/false 判断。
数数本地 Track 成功和 Bugly 初始化成功均不能单独证明后台已接收。

## 对外命名（0.3.2）

SDK 公开配置字段、属性、事件和方法统一 PascalCase，参数与内部变量使用 camelCase。
例如 Options.Transport、Options.MaxAttempts、Options.TimeoutSeconds、request.Timeout、request.IsDone、request.DownloadHandler.Data、operation.Completed。
Unity 要求的 keepWaiting override 和 Java JNI 回调 onComplete 保留原名；数数事件 net_cost 的 api、duration_ms 等协议键不变。
旧 camelCase 入口保留为 Obsolete 转发属性/事件，旧调用会有编译警告；Unity 配置字段通过 FormerlySerializedAs 迁移。
请将通过字段名进行反射的代码、外部 JSON 配置生产者或自定义 Inspector 更新为新名称。

```csharp
IEnumerator Query()
{
    var form = new WWWForm();
    form.AddField("function", 111);
    using (var request = BuglyWebRequest.Post("query_level", form, urls))
    {
        request.Options.Transport = HttpTransport.OkHttp;
        request.Options.MaxAttempts = 3; // 含首次请求，1 表示不重试
        request.Timeout = 15;           // 每次尝试的超时秒数
        request.UrlCompleted += response =>
        {
            Debug.Log(response.Success + " " + response.DurationMs + " " + response.FailureReason);
            // response.Data 为该 URL 的 byte[] 返回数据。
        };
        var operation = request.SendWebRequest();
        operation.Completed += op => Debug.Log("全部 URL 完成");
        yield return operation;
        // 多 URL 读取 request.Responses；仅单 URL 可用 request.DownloadHandler.Data。
    }
}
```
## 仓库结构

根目录是主 UPM 包；Dependencies~ 存放两个可独立安装的依赖包，Unity 不会将该目录作为主包源码导入。
Samples~/Demo 是按需导入的示例。数数供应商文件及其 Apache 许可保持原样。
不包含项目的账号密钥、配置资源、服务器地址、APK、Library 或测试日志。
