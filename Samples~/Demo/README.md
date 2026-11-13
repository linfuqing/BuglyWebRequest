# Demo

导入后先通过 Tools > Bugly WebRequest > Create or Select SDK Configuration 创建配置，填写自己的 Bugly 与数数参数。
Mode、TimeZone、网络类型及启动初始化均可在配置资源中设置。已有数数实例时使用 reuseThinkingDataInstance。
此示例脚本会在运行场景时自动创建测试面板，无需挂载；只在点击发送后发出测试请求。
URLs 每行一个，Inspector 对应 string[]。示例表单为 function=111，请按实际接口修改。
Android 真机支持 OkHttp / URLConnection；Editor、WebGL、桌面使用 UnityWebRequest。
结束测试后删除导入的示例文件夹，避免测试面板出现在正式游戏中。
