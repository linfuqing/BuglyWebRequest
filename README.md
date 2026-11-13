# Bugly WebRequest + ThinkingData

—— 1. 导入 SDK

打开 **Window > Package Manager > + > Add package from git URL**，按顺序导入：

数数官方包（已安装 3.5.0 则跳过）：
```text
https://github.com/ThinkingDataAnalytics/unity-sdk.git#v3.5.0
```

主 SDK（已包含 Android 适配）：
```text
https://github.com/linfuqing/BuglyWebRequest.git
```
自己已经配置的Bugly Android依赖不会被覆盖

—— 2. 配置 SDK（如果已经自行完成或拥有完成官方 Bugly（启用 NET_QUALITY）和数数默认实例初始化，可忽略该步骤）

由本 SDK 初始化：将包内 `Runtime/Prefabs/BuglyWebRequest.prefab` 拖入启动场景，在 Inspector 中填写配置，只需放一份。


需要自定义超时等参数或选择数数实例时，仍可挂 Prefab，开启 Reuse Bugly Instance / Reuse Thinking Data Instance。复用时 Bugly 账号无需填写；数数 AppID 留空使用默认实例，填写可选择对应实例。原生实例未就绪时会明确报错，请求不会发送。

开启 **InitializeOnStartup** 自动初始化，业务请求在初始化后发送。关闭时，在玩家同意隐私政策后调用：

```csharp
BuglySdkManager.Instance.InitializeAfterConsent();
```

——Bugly配置信息

| 配置 | 说明 |
| --- | --- |
| Enable Bugly | 启用 Bugly 网络监控。 |
| Reuse Bugly Instance | 复用游戏已初始化的 Bugly，不重复初始化或覆盖配置。 |
| Bugly AppID / AppKey | Bugly 后台「设置 > 产品信息」中的 APP ID / APP KEY。 |
| Bugly Region | Overseas：海外版；Domestic：国内版，按应用所属版本选择。 |
| Bugly Debug Mode | Bugly 调试模式，测试时按需开启。 |
| Bugly Enable Native Logs | Android 原生详细日志，排查问题时开启。 |
| Bugly Filter Local Requests | 开启后不采集局域网、回环地址；测试本地服务器时关闭。 |

—— 数数埋点配置信息

| 配置 | 说明 |
| --- | --- |
| Enable Thinking Data | 启用数数埋点。 |
| Thinking App Id / Server Url | 数数项目提供的 APP_ID 和数据接收地址。 |
| Thinking Mode | Normal：正常上报；Debug：调试且入库；DebugOnly：调试不入库。 |
| Thinking Time Zone | Local：设备时区；UTC：UTC 时区；Other：填写 Time Zone Id。 |
| Reuse Thinking Data Instance | 复用已初始化的数数；AppID 留空使用默认实例，填写选择对应实例，不覆盖配置。 |
| Thinking Network Type | 数数数据上报使用的网络类型。 |
| Thinking Enable Log | 输出数数 SDK 日志。 |
| Enable Editor Analytics | 允许在 Unity Editor 中初始化数数并上报埋点。 |

—— 默认请求配置信息（DefaultRequest）

| 配置 | 说明 |
| --- | --- |
| Transport | Android 使用 OkHttp 或 UrlConnection，默认 OkHttp；Editor、WebGL、桌面端使用 UnityWebRequest。 |
| Timeout Seconds | 每次尝试的超时秒数，默认 15。 |
| Max Attempts | 最多尝试次数，含首次，默认 3；设为 1 不重试。超时、网络错误及部分 HTTP 错误会重试。 |
| Retry Delay Ms | 重试基础间隔，默认 250 毫秒，随重试次数递增，最多 2000 毫秒。 |
| Max Response Bytes | 响应大小上限，默认 16 MiB，超出按失败处理。 |
| Report Net Cost | 向数数上报 net_cost，每个 URL 重试结束后仅上报一次最终结果；不控制 Bugly 采集。 |

使用 HTTP 地址时，在 Player Settings 中设置 **Allow downloads over HTTP**。

—— 3. 两种调用入口

以下示例放在自己的 MonoBehaviour 中，并添加：

```csharp
using System.Collections;
using UnityEngine;
using ZeroGame.Networking;
```

—— 一：埋点式，长链接采用，每次跟服务器交互时同步调用
```csharp
void SendFromConfig()
{
    // report_event：本次请求的埋点标识，可按业务命名。
    BuglyWebRequest.Trace("report_event");
}
```

—— 二：平替UnityWebRequest，短链接采用， 传入 URL，自定义参数，接收回调

通过 `StartCoroutine(SendManually())` 调用：

```csharp
IEnumerator SendManually()
{
    var form = new WWWForm();
    form.AddField("function", 111);
    string url = "https://your-server.example/connect.php";

    // query_level：本次请求的埋点标识，可按业务命名。
    // form：发送给服务器的 WWWForm 表单参数。
    // url：本次请求的单个完整 HTTP(S) 地址，不读取文本配置。
    // request：待发送的请求对象，设置参数后调用 SendWebRequest() 开始请求。
    using (var request = BuglyWebRequest.Post("query_level", form, url))
    {
      // request.Options.Transport = HttpTransport.OkHttp; // Android 请求方式
      // request.Options.MaxAttempts = 3; // 最多尝试次数，含首次
      // request.Timeout = 15; // 每次尝试的超时秒数
        request.UrlCompleted += response =>
        {
            Debug.Log($"成功={response.Success} 耗时={response.DurationMs}ms 原因={response.FailureReason}");
            byte[] data = response.Data;
        };

        yield return request.SendWebRequest(); // 开始发送并等待该 URL 完成
    }
}
```


完成以上任意一种，SDK会按配置进行 Bugly 网络监控和数数网络情况埋点
