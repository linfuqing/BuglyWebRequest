# Bugly WebRequest + ThinkingData — UPM 0.3.2

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

## 文本 URL 配置（0.3.1）

直接编辑 Git 包内 `Runtime/Resources/BuglyWebRequestDefaultUrls.txt`，UTF-8 编码，每行一个完整 HTTP(S) URL。支持多个地址、空行和整行 `#` / `//` 注释；不要在 URL 后追加行内注释。
Demo 默认从这个文本读取，无需 Inspector 填写 URL，每次发送前重新加载；读取或格式校验失败时不会发送。
文件预置本地 PHP、腾讯云东京和谷歌云东京三个测试地址，各地址独立请求和重试。只读取配置不会自动发送网络请求。

使用方也可在自己的 `Assets/Resources/BuglyWebRequestUrls.txt` 放置同格式文本，该文件优先于包内默认配置。请保持该资源名称唯一，不要编辑 Library/PackageCache 中的只读 Git 包。
这是构建时携带的本地文本配置；修改仓库文本后需要更新 Git 包，Android/WebGL 等已安装版本需要重新构建发布，不能远程热更新。
URL 文本仅配置请求地址，不决定 HTTP 连接是否复用。

自有业务脚本中可这样使用（Unity 主线程）：

```csharp
using (var request = BuglyWebRequest.Post("your_api", form))
{
    yield return request.SendWebRequest();
}
```

所有 Post 请求都在创建时读取文本配置，不只 Demo。旧的三参数调用保留兼容，但传入的 urls 不再生效，并产生 Obsolete 编译提醒；请改成 Post(api, form)。读取失败或文本格式有误会抛出异常，不创建网络请求。
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
using (var request = BuglyWebRequest.Post("test_post", form))
{
    request.Options.Transport = HttpTransport.OkHttp;
    request.UrlCompleted += response => UnityEngine.Debug.Log(
        "success=" + response.Success + " attempts=" + response.Attempts);
    yield return request.SendWebRequest();
}
```

URL 统一读取文本配置；每个 URL 独立执行和重试，业务返回字节内容由游戏解析。
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
    using (var request = BuglyWebRequest.Post("query_level", form))
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
