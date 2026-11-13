using UnityEditor;
using UnityEngine;
using ThinkingData.Analytics;

namespace ZeroGame.Networking.Editor
{
    [CustomEditor(typeof(BuglySdkSettings))]
    public sealed class SettingsEditor : UnityEditor.Editor
    {
        [MenuItem("Tools/Bugly WebRequest/Create or Select SDK Configuration")]
        public static void CreateConfiguration()
        {
            const string path = "Assets/Resources/BuglyWebRequestSettings.asset";
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var config = AssetDatabase.LoadAssetAtPath<BuglySdkSettings>(path);
            if (config == null)
            {
                config = CreateInstance<BuglySdkSettings>();
                AssetDatabase.CreateAsset(config, path); AssetDatabase.SaveAssets();
            }
            Selection.activeObject = config; EditorGUIUtility.PingObject(config);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            bool children = true;
            bool reuse = serializedObject.FindProperty("ReuseThinkingDataInstance").boolValue;
            while (property.NextVisible(children))
            {
                children = false;
                if (property.name == "m_Script") { using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(property); continue; }
                if (reuse && (property.name == "ThinkingMode" || property.name == "ThinkingTimeZone" ||
                    property.name == "ThinkingTimeZoneId" || property.name == "ThinkingNetworkType" || property.name == "ThinkingEnableLog")) continue;
                if (property.name == "ThinkingTimeZoneId" && serializedObject.FindProperty("ThinkingTimeZone").intValue != (int)TDTimeZone.Other) continue;
                if (property.name == "BuglyAppKey") property.stringValue = EditorGUILayout.PasswordField("Bugly App Key", property.stringValue);
                else EditorGUILayout.PropertyField(property, true);
            }
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("配置属于使用方工程。Initialize On Startup 开启时启动自动初始化，适用于下载前已获得同意的流程；关闭时由游戏调用 InitializeAfterConsent()。修改初始化配置需重新启动应用。", MessageType.Info);
            if (GUILayout.Button("校验配置"))
            {
                string error;
                if (((BuglySdkSettings)target).Validate(out error)) Debug.Log("[BuglyWebRequest] 配置校验通过。");
                else Debug.LogWarning("[BuglyWebRequest] " + error);
            }
        }
    }
}
