# Demo

可以先由游戏原生初始化 Bugly（启用 NET_QUALITY）和数数默认实例，无需 SDK 配置组件。也可以将 Runtime/Prefabs/BuglyWebRequest.prefab 拖入场景：开启复用开关沿用原生配置，或关闭开关并填写账号，由 SDK 初始化。
导入示例后，将 BuglyWebRequestDemo 手动挂到场景中的 GameObject，运行时才会显示面板。仅存在于 Assets 中不会创建 Demo 或绘制 UI。DrawUI 默认为 true，可关闭隐藏已挂载 Demo 的面板；仅点击发送才发出请求。

- 勾选“文本 URL 自动发送”：读取 Runtime/Resources/BuglyWebRequestDefaultUrls.txt，或使用方 Assets/Resources/BuglyWebRequestUrls.txt。实际调用 Trace(eventName)，无需表单，自动发送，使用 SDK 默认传输/重试设置。
- 取消勾选：通过面板或 Inspector 的 Url 填写单个 URL，不读取文本。实际调用 Post(eventName, form, Url)，设置传输方式后 SendWebRequest()，通过 UrlCompleted 接收结果。

文本文件每行一个 URL，支持空行和整行 # / // 注释。显式 URL 模式示例表单 function=111，请按实际接口修改。
修改 Git 文本不会更新已安装的游戏，需要更新包并重新构建。两种入口都是 HTTP，不代表实际建立实时消息长连接。
Android 真机支持 OkHttp / URLConnection；Editor、WebGL、桌面使用 UnityWebRequest。
测试结束后删除导入的示例文件夹，避免面板出现在正式游戏。
