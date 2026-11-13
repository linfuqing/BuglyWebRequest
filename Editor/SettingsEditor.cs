using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using ThinkingData.Analytics;

namespace ZeroGame.Networking.Editor
{
    [CustomEditor(typeof(BuglySdkManager))]
    public sealed class SettingsEditor : UnityEditor.Editor
    {
        [MenuItem("Tools/Bugly WebRequest/Add SDK Prefab To Scene")]
        public static void AddPrefabToScene()
        {
            var existing = Object.FindObjectOfType<BuglySdkManager>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(SettingsEditor).Assembly);
            if (package == null) { Debug.LogError("[BuglyWebRequest] SDK package not found."); return; }
            var path = Path.Combine(package.assetPath, "Runtime/Prefabs/BuglyWebRequest.prefab").Replace('\\', '/');
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogError("[BuglyWebRequest] SDK prefab not found: " + path); return; }
            var created = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(created, "Add Bugly SDK prefab");
            Selection.activeGameObject = created;
            EditorSceneManager.MarkSceneDirty(created.scene);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            var config = serializedObject.FindProperty("Settings");
            var property = config.Copy();
            var end = property.GetEndProperty();
            bool next = property.NextVisible(true);
            bool reuse = config.FindPropertyRelative("ReuseThinkingDataInstance").boolValue;
            bool reuseBugly = config.FindPropertyRelative("ReuseBuglyInstance").boolValue;
            while (next && !SerializedProperty.EqualContents(property, end))
            {
                bool hidden = reuse && (property.name == "ThinkingServerUrl" || property.name == "ThinkingMode" || property.name == "ThinkingTimeZone" ||
                    property.name == "ThinkingTimeZoneId" || property.name == "ThinkingNetworkType" || property.name == "ThinkingEnableLog");
                hidden |= reuseBugly && (property.name == "BuglyAppId" || property.name == "BuglyAppKey" ||
                    property.name == "BuglyRegion" || property.name == "BuglyDebugMode" || property.name == "BuglyEnableNativeLogs" || property.name == "BuglyFilterLocalRequests");
                hidden |= property.name == "ThinkingTimeZoneId" && config.FindPropertyRelative("ThinkingTimeZone").intValue != (int)TDTimeZone.Other;
                if (!hidden)
                {
                    if (property.name == "BuglyAppKey") property.stringValue = EditorGUILayout.PasswordField("Bugly App Key", property.stringValue);
                    else EditorGUILayout.PropertyField(property, true);
                }
                next = property.NextVisible(false);
            }
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("直接在场景 Prefab 实例上填写配置。Initialize On Startup 开启时自动初始化；关闭后由游戏在同意隐私政策后调用 InitializeAfterConsent()。", MessageType.Info);
            if (reuseBugly || reuse)
                EditorGUILayout.HelpBox("复用模式：先由游戏初始化官方 SDK，再发送请求。Bugly 需启用 NET_QUALITY；数数 AppID 留空使用默认实例，填写可选择对应实例。本组件不修改原生配置。", MessageType.Info);
            if (GUILayout.Button("校验配置"))
            {
                string error = "";
                var settings = ((BuglySdkManager)target).Settings;
                if (settings != null && settings.Validate(out error)) Debug.Log("[BuglyWebRequest] 配置校验通过。");
                else Debug.LogWarning("[BuglyWebRequest] " + (settings == null ? "配置为空。" : error));
            }
        }
    }
}
