# Changelog

## 0.3.2

- 对外字段、属性和事件统一 PascalCase；同步 SDK、Inspector 与 Demo。
- 旧字段名增加序列化迁移，旧源码入口保留 Obsolete 转发兼容。
- 保持 JNI、Unity override 和埋点协议键不变。

## 0.3.1

- 增加多 URL 文本配置及 Resources 加载/格式校验 API。
- 所有 Post 请求统一从文本读取 URL；支持使用方工程本地文本覆盖。旧 urls 参数保留但忽略，使用 Post(api, form)。

## 0.3.0

- Git URL UPM 布局；拆分 Bugly Android 适配包并声明真实包依赖。
- 保留数数 3.5.0 官方文件及许可。
- Samples 提供可导入的 Demo，配置和账号留在使用方工程。
- 基于 SDK 0.2.4，保持网络请求、重试与埋点行为。
