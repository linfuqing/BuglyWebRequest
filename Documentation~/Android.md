# 主 SDK 内置 Android 网络适配

这是主 SDK 中 ZeroGame 自有的 Android 桥接代码，不再作为独立 UPM 包发布。
Android 构建时自动复制 Java 桥接源文件，配置权限、混淆保留规则与以下 Maven 依赖：

- `com.tencent.bugly:bugly-pro:4.4.8.2`
- `com.squareup.okhttp3:okhttp:3.12.13`
- `com.squareup.okhttp3:okhttp-sse:3.12.13`

以上版本仅作为缺失时的默认值。构建时按 group:artifact 检测 unityLibrary/build.gradle，已声明任意版本就保留，不重复添加或覆盖版本。支持字符串坐标、版本变量和常见 Groovy group/name/version 写法，注释不算已安装依赖。

主 SDK 版本为 0.4.1；缺失时默认接入的原生 Bugly 版本为 4.4.8.2。
需要 Android 构建环境能够访问 Google / Maven Central 仓库。
可由 BuglyWebRequest.prefab 初始化；已有官方原生初始化时可直接复用，无需挂载 Prefab。Bugly 需已启动 NET_QUALITY，数数需已初始化默认实例；两种复用均不覆盖原生配置。
仓库仅包含 ZeroGame 自有适配代码。Bugly、OkHttp、SSE 从 Maven Central 下载，遵循各供应商许可。Bugly 官方接入文档：https://bugly.tds.tencent.com/docs/sdk/android/
