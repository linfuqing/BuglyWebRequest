# Bugly WebRequest + ThinkingData — UPM 0.3.0

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

## 导入 Demo 与配置

选中 Bugly WebRequest + ThinkingData，展开 Samples，点击 Bugly WebRequest Demo 的 Import。
示例导入到 Assets/Samples；点击 Play 会自动出现测试面板。导入前不会自动加入游戏。
通过 Tools > Bugly WebRequest > Create or Select SDK Configuration 创建项目自己的配置，填写 Bugly AppID/AppKey、地区、数数 AppID/ServerUrl、Mode 和 TimeZone。
配置属于使用方 Assets/Resources/BuglyWebRequestSettings.asset，不存入 SDK 仓库。
initializeOnStartup 仅用于项目已取得玩家隐私同意的情况；否则关闭该选项，在自己的同意流程后调用 BuglySdkManager.Instance.InitializeAfterConsent()。
LAN 过滤默认关闭，可测试局域网；HTTP 请求另需在 Player Settings 配置 Allow downloads over HTTP。

## 调用示例

```csharp
using ZeroGame.Networking;
// 放入你自己的 IEnumerator 方法内：
var form = new UnityEngine.WWWForm();
form.AddField("function", 111);
using (var request = BuglyWebRequest.Post("test_post", form, urls))
{
    request.Options.transport = HttpTransport.OkHttp;
    request.UrlCompleted += response => UnityEngine.Debug.Log(
        "success=" + response.Success + " attempts=" + response.Attempts);
    yield return request.SendWebRequest();
}
```

urls 是 string[]；每个 URL 独立执行和重试，业务返回字节内容由游戏解析。
SDK 的网络成功状态不根据业务响应中的 true/false 判断。
数数本地 Track 成功和 Bugly 初始化成功均不能单独证明后台已接收。

## 仓库结构

根目录是主 UPM 包；Dependencies~ 存放两个可独立安装的依赖包，Unity 不会将该目录作为主包源码导入。
Samples~/Demo 是按需导入的示例。数数供应商文件及其 Apache 许可保持原样。
不包含项目的账号密钥、配置资源、服务器地址、APK、Library 或测试日志。
