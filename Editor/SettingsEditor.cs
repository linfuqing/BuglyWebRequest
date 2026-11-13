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
            var path = Path.Combine(package.assetPath, "Runtime/Prefabs/BuglySDK.prefab").Replace('\\', '/');
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
            while (next && !SerializedProperty.EqualContents(property, end))
            {
                bool hidden = reuse && (property.name == "ThinkingMode" || property.name == "ThinkingTimeZone" ||
                    property.name == "ThinkingTimeZoneId" || property.name == "ThinkingNetworkType" || property.name == "ThinkingEnableLog");
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
