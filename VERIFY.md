# UPM 0.3.0 包装验证（2026-11-13）

- Unity 2022.3.62f3，独立空工程，Android 为目标平台。
- 三个包通过本地 Git URL + 提交 SHA 安装成功；packages-lock 确认为 git 来源。
- 主包依赖关系正确：Bugly Android Network Adapter 4.4.8、TDAnalytics 3.5.0。
- Runtime、Editor、独立 Android 构建适配程序集以及导入的 Demo 编译通过；批处理退出码 0。
- Configuration checks PASS：Mode、TimeZone、自定义时区、请求配置隔离、复用实例和参数校验。
- 889 个数数原始文件逐字节一致；Java 桥接源文件内容保持不变。
- 包中未包含项目 Bugly 凭据及本地测试服务器地址。
- 本次未构建 APK、未验证远程 GitHub 安装或 Android Gradle 下载、未发送统计或业务请求。
- 用户仓库尚未提交/推送，远程 Git URL 需发布后使用。

以下为原 SDK 的历史验证记录，不代表本次重新执行：
# 验证记录

日期：2026-11-11，Unity 2022.3.62f3。

- Java WireBridge / SdkBridge：实际 Bugly AAR + OkHttp + Android SDK 引用，Java 8 编译通过。
- 本机 HTTP 集成：OkHttp / URLConnection 两种传输均通过；覆盖并发独立 URL、表单编码/头/二进制返回、503 后重试成功、404 不重试、超时重试结束、响应大小限制、取消和重复取消；每个 URL 只有一个最终回调。
- C# reporter：net_cost 事件去重、api 首次语义、所有最终失败语义、URL 脱敏、Track 异常隔离、未就绪不调用 Track、负耗时归零，测试通过。
- Runtime：Android / WebGL / Editor 条件编译通过，引用实际 ThinkingAnalytics 3.5.0 程序集。
- Editor 导出 hook 与测试面板：独立编译通过。
- Maven 依赖：沿用已经验证的 Bugly 4.4.8.2 + okhttp/okhttp-sse 3.12.13；必须保留 SSE 类依赖以避免 Bugly JNI 解析缺类。
- 新工程完整 Unity 导入/编译：已在现有编辑器完成，SDK/Editor 程序集生成，Console 0 errors；测试面板 Play 正常显示。
- 真机 JNI 回调、Bugly 管理台入库、数数管理台入库：未验证；未向真实业务或统计后台发送测试请求。

测试为本机纯 HTTP/内存适配器验证，不能替代安卓 Player 验收。重试发送成功不会绕过业务幂等要求。

Unity Play 本机 HTTP 测试：两个独立 URL，/ok 成功一次，/fail 返回503共尝试3次；服务器计数 ok=1、failureAttempts=3，最终只有两条 net_cost 日志（success=true/false 各一条）。未配置数数时 reported=false，无后台上报。测试结束已停止 Play，并关闭临时本机服务。
新工程已通过 SDK 菜单将 HTTP 设置为 Development Only，并开启 Development Build 供局域网测试；正式包仍按策略限制 HTTP。
批处理导入因工程已在另一个 Unity 实例打开而中止，最终以现有实例实际导入/编译/Play 结果为准。

## 0.2.0 配置升级
新增 ScriptableObject 配置、单例管理器、Inspector/创建菜单、实际 TDConfig Mode/TimeZone/网络/日志映射、地区选择及默认请求快照。旧字符串初始化 API 保留但标记过时；使用方配置不放进 SDK 分发包。
Android/WebGL/Editor Runtime、Editor Inspector/菜单/Demo、Java 地区参数编译通过。Unity 中配置映射检查与导入验证完成后补充。


0.2.0 实际 Unity 验证完成：Runtime 与 Editor 程序集均生成，Console 0 errors。菜单 Verify Configuration Mapping 输出 PASS，涵盖可选 Mode/TimeZone、自定义时区、请求默认参数复制隔离、配置校验和复用分支校验；没有初始化 SDK 或发送网络数据。Create or Select SDK Configuration 成功定位使用方配置资源，Inspector 已确认全部可填写字段。

实际导入发现并修复 Editor asmdef 对 ThinkingAnalytics 的直接引用缺失。新配置资产留在 Assets/Resources/BuglyWebRequestSettings.asset，通用分发包明确不包含此使用方资产。旧 SDK 0.1.0 包保留供历史对照。

0.2.1：新增 BeforeSceneLoad 启动初始化（由资源配置 initializeOnStartup 控制，默认开启），不写入同意记录；Demo 显示真实模块状态并保留手动重试。Runtime Android/WebGL/Editor 和 Demo 编译 PASS。未执行真实 SDK 初始化及后台验收。

0.2.2：实际 Bugly 4.4.8.2 字节码确认 BuglyBuilder.logLevel 默认 LEVEL_WARN。新增独立原生详细日志开关，启用时设置 LEVEL_VERBOS，未改变 debugMode/网络采样。Runtime Android/WebGL/Editor compile PASS；Java bridge 对实际 SDK/Android35 compile PASS（仅 Java8 bootclasspath 常规警告）。未执行设备采集及后台上报验收。

0.2.4发行：包含已在Android实机验证的userId、原生详细日志和本地地址过滤可配置JNI。默认不滤局域网，Demo版本统一。2026-11-12局域网两通道HTTP200，net_quality实际上传在10:47:08返回code0/msg success。原工程专用凭据、配置资产、场景、后台日志与设备数据库不进入分发包。

0.2.4发行检查：Runtime Android/WebGL/Editor及Demo编译PASS；Java桥接对实际Bugly4.4.8.2依赖编译PASS；分发目录凭据/真实服务器地址扫描PASS。Java首次检查引用旧Temp依赖路径失败，改为从Gradle实际缓存提取classes.jar后通过，非代码错误。


## URL 文本配置 0.3.1（2026-11-13）

- 新增 Runtime/BuglyUrlConfig.cs 与 Runtime/Resources/BuglyWebRequestDefaultUrls.txt。
- 使用 Unity 2022.3.62f3 真实程序集编译 Runtime（UNITY_EDITOR/UNITY_ANDROID）与 Demo 通过。
- 解析验证 PASS：多地址顺序、BOM、CRLF、空行、整行注释、URL fragment 保留、非法协议/非法行/空配置拒绝、默认三个地址。
- Tests~/UrlConfigTests.cs 为独立解析测试，不在 Unity 游戏程序集中编译。
- 本次未重新构建 Android/WebGL Player，未向服务器发送请求；未执行 Git 提交或推送。

- 随后将读取入口移到主 API：Post(api, form) 和兼容旧签名均统一读取文本；Runtime 与 Demo 再次编译通过。

## 公开命名统一 0.3.2（2026-11-13）

- Runtime（Editor/Android/WebGL 条件）、Editor 和 Demo 使用 Unity 2022.3.62f3 实际程序集编译通过。
- NamingTests PASS：28 个配置字段迁移属性及兼容别名、旧选项读写转发、新命名 API、请求配置克隆隔离。
- 测试验证迁移标记及转发行为；本次未在 Unity 中实际重开旧序列化资源，也未构建或运行真机 Player。
- 测试源文件位于 Tests~/NamingTests.cs，正式包不编译该目录。
- 未修改原生 Java、数数 SDK 或埋点协议键；未发送网络请求，未提交或推送。
