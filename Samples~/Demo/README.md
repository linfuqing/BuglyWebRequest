# Demo

导入后通过 Tools > Bugly WebRequest > Create or Select SDK Configuration 创建配置，填写自己的 Bugly 与数数参数。
Mode、TimeZone、网络类型及启动初始化均可在配置资源中设置。已有数数实例时使用 ReuseThinkingDataInstance。
示例脚本会在运行场景时自动创建测试面板，无需挂载；只在点击发送后发出测试请求。

URL 从包内 Runtime/Resources/BuglyWebRequestDefaultUrls.txt 读取，每行一个，支持多个地址、空行和整行 # / // 注释。
使用方可以创建 Assets/Resources/BuglyWebRequestUrls.txt 覆盖包内地址，无需在 Inspector 填 URL。
每次发送前重新加载文本，格式错误时不发送。示例表单 function=111，请按实际接口修改。
已安装 Android/WebGL 版本修改内置文本后需要重新打包，Git 文本不会自动远程更新。
Android 真机支持 OkHttp / URLConnection；Editor、WebGL、桌面使用 UnityWebRequest。
结束测试后删除导入的示例文件夹，避免测试面板出现在正式游戏中。
