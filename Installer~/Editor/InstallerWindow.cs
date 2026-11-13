using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ZeroGame.Networking.Installer
{
    public sealed class InstallerWindow : EditorWindow
    {
        private bool allowVersionReplacement;
        private bool showUrls;
        private Vector2 scroll;

        [MenuItem("Tools/Bugly WebRequest/Install SDK And Dependencies")]
        public static void Open()
        {
            var window = GetWindow<InstallerWindow>("Bugly SDK 安装器");
            window.minSize = new Vector2(560, 420);
            window.Show();
        }

        private void OnEnable() { InstallerService.Changed += Repaint; }
        private void OnDisable() { InstallerService.Changed -= Repaint; }
        private void OnInspectorUpdate() { Repaint(); }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Bugly WebRequest 一键安装", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("一次安装主 SDK 和两个依赖。数数使用官方 Git，Bugly 原生 SDK 在 Android 构建时从 Maven Central 下载。", MessageType.Info);
            try
            {
                var plan = InstallerService.Preview();
                var installed = PackageInfo.GetAllRegisteredPackages();
                foreach (var target in plan.Packages)
                {
                    var current = InstallerPlan.Find(installed, target.Name);
                    EditorGUILayout.LabelField(target.DisplayName + "  " + target.Version, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(current == null ? "尚未安装" : "当前：" + current.version + "（" + current.source + "）");
                }
                showUrls = EditorGUILayout.Foldout(showUrls, "查看安装来源", true);
                if (showUrls) foreach (var target in plan.Packages) EditorGUILayout.SelectableLabel(target.Url, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(38));
                EditorGUILayout.Space();
                using (new EditorGUI.DisabledScope(InstallerService.IsBusy || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
                {
                    allowVersionReplacement = EditorGUILayout.ToggleLeft("允许替换已有的不同版本（可能降级现有版本）", allowVersionReplacement);
                    if (GUILayout.Button("一键安装 / 修复 SDK 与依赖", GUILayout.Height(34)))
                    {
                        try { InstallerService.Install(allowVersionReplacement); }
                        catch (Exception error) { InstallerService.ShowError(error); }
                    }
                }
            }
            catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(InstallerService.Status, InstallerService.Failed ? MessageType.Error : MessageType.Info);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/cn.com.zerogame.bugly-webrequest/Runtime/Prefabs/BuglySDK.prefab");
            if (prefab != null && !InstallerService.IsBusy && GUILayout.Button("在 Project 中选中 SDK 配置 Prefab"))
            {
                Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
