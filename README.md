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

——2. 创建配置

打开 **Tools > Bugly WebRequest > Create or Select SDK Configuration**，创建：

```text
Assets/Resources/BuglyWebRequestSettings.asset//该资源路径不可更改！！！
```
填写 Bugly AppID、AppKey、地区，以及数数 AppID、ServerUrl、Mode、TimeZone。超时、重试和传输方式可在 DefaultRequest 中设置。

开启 **InitializeOnStartup** 后自动初始化，无需挂载脚本或手动调用。
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
    var form = new WWWForm();
    form.AddField("function", 111); // 按服务器接口填写表单
    BuglyWebRequest.Post("report_event", form);
}
```

编辑包内 `Runtime/Resources/BuglyWebRequestDefaultUrls.txt`，或在项目创建 `Assets/Resources/BuglyWebRequestUrls.txt` 覆盖默认地址。每行一个完整 HTTP(S) URL，可填写多个；支持空行和整行 `#` / `//` 注释。
### 入口二：显式 URL，自动发送，接收回调

```csharp
void SendWithCallback()
{
    var form = new WWWForm();
    form.AddField("function", 111);
    string[] urls = { "https://your-server.example/connect.php" };

    BuglyWebRequest.Post("query_level", form, urls, response =>
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

    using (var request = BuglyWebRequest.Post("query_level", form, urls))
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
