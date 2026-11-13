# Demo

先通过 Tools > Bugly WebRequest > Create or Select SDK Configuration 创建配置，填写自己的 Bugly 和数数参数。Mode、TimeZone 等均由配置控制。
导入示例后运行场景会自动创建面板，仅点击发送才发出请求。

- 勾选“文本 URL 自动发送”：读取 Runtime/Resources/BuglyWebRequestDefaultUrls.txt，或使用方 Assets/Resources/BuglyWebRequestUrls.txt。实际调用 Post(api, form)，自动发送，使用 SDK 默认传输/重试设置。
- 取消勾选：通过面板或 Inspector 的 Urls 填写多个显式 URL，不读取文本。实际调用 Post(api, form, Urls)，设置传输方式后 SendWebRequest()，通过 UrlCompleted 接收结果。

每行一个 URL。文本文件支持空行和整行 # / // 注释。示例表单 function=111，请按实际接口修改。
修改 Git 文本不会更新已安装的游戏，需要更新包并重新构建。两种入口都是 HTTP，不代表实际建立实时消息长连接。
Android 真机支持 OkHttp / URLConnection；Editor、WebGL、桌面使用 UnityWebRequest。
测试结束后删除导入的示例文件夹，避免面板出现在正式游戏。
