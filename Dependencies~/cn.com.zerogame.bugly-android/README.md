# Bugly Android Network Adapter

这是 ZeroGame 的 Unity 适配包，不是腾讯发布的官方 UPM 包。
Android 构建时自动复制 Java 桥接源文件，配置权限、混淆保留规则与以下 Maven 依赖：

- `com.tencent.bugly:bugly-pro:4.4.8.2`
- `com.squareup.okhttp3:okhttp:3.12.13`
- `com.squareup.okhttp3:okhttp-sse:3.12.13`

适配包的 UPM 版本为 4.4.8；原生 Bugly 的真实版本为 4.4.8.2。
需要 Android 构建环境能够访问 Google / Maven Central 仓库。
此包本身不初始化 SDK；配置和初始化由主包负责。
供应商 SDK 的使用与再分发遵循各自许可，详情见主包 THIRD-PARTY-NOTICES.md。
