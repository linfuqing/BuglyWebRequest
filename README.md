Bugly WebRequest + ThinkingData

——1. 导入 SDK

打开 **Window > Package Manager > + > Add package from git URL**，请按顺序添加！！！：

1. Bugly Android 适配包（我们自己的桥接代码；Bugly 原生 SDK 由 Android 构建时从 Maven Central 下载，不随本仓库分发）：
   ```text
   https://github.com/linfuqing/BuglyWebRequest.git?path=/Dependencies~/cn.com.zerogame.bugly-android
   ```
2. 数数官方 3.5.0（已有相同版本可跳过）：
   ```text
   https://github.com/ThinkingDataAnalytics/unity-sdk.git#v3.5.0
   ```
3. 主 SDK：
   ```text
   https://github.com/linfuqing/BuglyWebRequest.git
   ```

——2. 配置 Prefab

将主 SDK 中的 `Runtime/Prefabs/BuglySDK.prefab` 拖入启动场景，或通过 **Tools > Bugly WebRequest > Add SDK Prefab To Scene** 添加。
Prefab 已挂载 BuglySdkManager，在场景实例的 Inspector 中填写 Bugly、数数及 DefaultRequest 参数，不需要创建或引用配置 Asset。

开启 **InitializeOnStartup** 后自动初始化并跨场景保留，只需在启动场景放一份。需要等待隐私同意时关闭该选项，在同意后调用：

```csharp
BuglySdkManager.Instance.InitializeAfterConsent();
```

旧的 BuglyWebRequestSettings.asset 不再读取，请将原参数填写到 Prefab 实例后删除旧 Asset。不要把项目账号填写到 SDK 仓库自带的 Prefab 中。

Trace 发送前会检查初始化状态；启用的模块尚未就绪时抛出明确异常，请求不会发送。缺少场景 Prefab 也会提示。
Android 检查启用的 Bugly 和数数；Editor 仅在 EnableEditorAnalytics 开启时检查数数；WebGL/桌面不要求 Android Bugly 初始化。
主动关闭的模块不检查；单次请求 ReportNetCost=false 时不要求数数就绪。自动初始化发生在 SDK 组件的 Start，业务代码请在其后发送请求。
##Bugly配置信息
	Enable Bugly:勾选后使用Bugly

	Bugly AppID和AppKey分别是在Bugly网站https://bugly.tds.tencent.com  设置 > 产品信息 >APP ID 和 APP KEY中

	Bugly Region(地区)：Overseas：海外版或Domestic：国内版

	Bugly Debug Mode： 启用 Bugly SDK 的调试模式。与 Unity 的 Development Build、详细日志开关是不同配置。

	Bugly Enable Native Logs：输出 Android 原生详细日志，便于排查初始化、采集和上报情况。关闭后仍保留警告级别日志。

	Bugly Filter Local Requests: 过滤本地地址请求的网络监控采集，不影响游戏请求本身,打开后本地URL将不会发送Bugly上报

##数数埋点配置信息
	Enable Thinking Data:勾选后使用数数埋点
	
	Reuse Thinking Data Instance： 表示复用游戏中已经初始化的数数 SDK 实例，如果自己工程中没有用到其他数数埋点实例则不用勾选
	
	Thinking App Id和Thinking Server Url 分别是在数数埋点网址https://te.lightboat.games  数据 > 数据接入 > APP_ID和公网地址

##默认请求配置信息（DefaultRequest）
	Transport：Android 请求方式，OkHttp 或 UrlConnection，默认 OkHttp；Editor、WebGL、桌面端使用 UnityWebRequest。

	Timeout Seconds：每次尝试的超时时间，单位为秒，默认 15 秒；发生重试时，每次尝试分别计时。

	Max Attempts：最多尝试次数，包含首次请求，默认 3 次；设为 1 表示不重试。超时、网络错误及部分 HTTP 错误才会重试。

	Retry Delay Ms：重试等待的基础时间，单位为毫秒，默认 250 毫秒；随重试次数递增，单次等待最多 2000 毫秒。

	Max Response Bytes：最大响应大小，单位为字节，默认 16777216（16 MiB）；超过限制按失败处理。

	Report Net Cost：勾选后向数数上报请求的 net_cost 埋点，默认开启；每个 URL 重试结束后只上报一次最终结果，不控制 Bugly 网络监控。

—— 3. 三种调用入口

在需要调用接口的脚本中添加命名空间
以下方法放在自己的 MonoBehaviour 类中：

```csharp
using System.Collections;
using UnityEngine;
using ZeroGame.Networking;
```

### 入口一：文本 URL，自动发送，无需业务回调（自走棋长链接推荐调用）

```csharp
void SendFromConfig()
{
    BuglyWebRequest.Trace("report_event");
}
```

入口一无需创建 WWWForm，默认发送空 POST；需要参数时仍可用 Trace("report_event", form)，显式传 null 也表示空请求体。入口二、三继续传入 form 和 urls。

编辑包内 `Runtime/Resources/BuglyWebRequestDefaultUrls.txt`，或在项目创建 `Assets/Resources/BuglyWebRequestUrls.txt` 覆盖默认地址。每行一个完整 HTTP(S) URL，可填写多个；支持空行和整行 `#` / `//` 注释。
### 入口二：显式 URL，自动发送，接收回调

```csharp
void SendWithCallback()
{
    var form = new WWWForm();
    form.AddField("function", 111);
    string[] urls = { "https://your-server.example/connect.php" };

    BuglyWebRequest.Trace("query_level", form, urls, response =>
    {
        Debug.Log($"URL={response.Url} 成功={response.Success}");
        byte[] data = response.Data; // 解析服务器返回的数据
    });
}
```

只使用传入的 urls，不读取文本。多个 URL 各自执行、重试，每个地址最终回调一次。

### 入口三：显式 URL，自定义参数，手动发送

通过 `StartCoroutine(SendManually())` 调用：

```csharp
IEnumerator SendManually()
{
    var form = new WWWForm();
    form.AddField("function", 111);
    string[] urls = { "https://your-server.example/connect.php" };

    using (var request = BuglyWebRequest.Trace("query_level", form, urls))
    {
        request.Options.Transport = HttpTransport.OkHttp; // 或 UrlConnection
        request.Options.MaxAttempts = 3; // 含首次请求，1 表示不重试
        request.Timeout = 15;            // 每次尝试的超时秒数
        request.UrlCompleted += response =>
        {
            Debug.Log($"成功={response.Success} 耗时={response.DurationMs}ms 原因={response.FailureReason}");
            byte[] data = response.Data;
        };

        yield return request.SendWebRequest(); // 等待全部 URL 完成
    }
}
```

入口一、二自动发送并释放，不要再调用 SendWebRequest() 或立即 using/Dispose。第一个字符串参数是埋点标识，form 是服务器表单参数；三种入口均为 HTTP 请求，在 Unity 主线程调用。

——4. Demo（可选）

在 Package Manager 选中主 SDK，展开 **Samples > Bugly WebRequest Demo > Import**。
将 BuglyWebRequestDemo 挂到场景对象后运行；DrawUI 控制面板显示，仅导入文件不会显示 UI。

Android 真机使用 OkHttp / URLConnection；Editor、WebGL、桌面使用 UnityWebRequest。HTTP 地址需在 Player Settings 中配置 **Allow downloads over HTTP**。

##错误信息

参数错误会指出具体字段、限制范围或 URL 行号；自动启动失败包含 eventName、传输方式和 URL 数量。原始异常保留在 InnerException，记录日志请使用 Debug.LogException(exception)，不要只记录异常类型。初始化失败可读取 BuglySdkManager.Instance.LastError。
