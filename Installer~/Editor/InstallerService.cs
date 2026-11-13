using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ZeroGame.Networking.Installer
{
    [InitializeOnLoad]
    internal static class InstallerService
    {
        private const string PendingKey = "ZeroGame.BuglyInstaller.Pending";
        private const string StatusKey = "ZeroGame.BuglyInstaller.Status";
        private const string FailedKey = "ZeroGame.BuglyInstaller.Failed";
        private static AddAndRemoveRequest request;
        private static InstallPlan pending;
        private static double nextCheck;
        internal static event Action Changed;
        internal static bool IsBusy { get { return pending != null; } }
        internal static string Status { get { return SessionState.GetString(StatusKey, "点击安装，将一次加入含 Android 功能的主 SDK 和数数官方 SDK。"); } }
        internal static bool Failed { get { return SessionState.GetBool(FailedKey, false); } }
        internal static PackageInfo Self { get { return PackageInfo.FindForAssembly(typeof(InstallerService).Assembly); } }

        static InstallerService()
        {
            string json = SessionState.GetString(PendingKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { pending = JsonUtility.FromJson<InstallPlan>(json); }
                catch (Exception error) { Fail("[BuglyInstaller][Resume] 安装状态读取失败：" + error.Message); }
            }
            EditorApplication.update += Update;
            EditorApplication.delayCall += AutoOpen;
        }

        private static void AutoOpen()
        {
            if (Application.isBatchMode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += AutoOpen; return; }
            var self = Self;
            if (self == null) return;
            string version = self.version + (self.git == null ? "" : self.git.hash);
            string key = "ZeroGame.BuglyInstaller.Shown." + Application.dataPath;
            if (pending == null && EditorPrefs.GetString(key, "") == version) return;
            EditorPrefs.SetString(key, version);
            InstallerWindow.Open();
        }

        internal static InstallPlan Preview()
        {
            return InstallerPlan.Create(Self);
        }

        internal static void Install(bool allowVersionReplacement)
        {
            if (IsBusy) throw new InvalidOperationException("[BuglyInstaller][Install] 已有安装任务，请等待当前任务完成。");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("[BuglyInstaller][Install] 请先退出 Play 模式再安装 SDK。");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("[BuglyInstaller][Install] Unity 正在编译或导入资源，请等待完成后再试。");
            var plan = Preview();
            var installed = PackageInfo.GetAllRegisteredPackages();
            InstallerPlan.CheckConflicts(plan, installed, allowVersionReplacement);
            var legacyAdapter = InstallerPlan.Find(installed, InstallerPlan.LegacyAdapterName);
            plan.RemovePackages = legacyAdapter != null && legacyAdapter.isDirectDependency ? new[] { InstallerPlan.LegacyAdapterName } : new string[0];
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string backupFolder = Path.Combine(project, "Library/BuglyWebRequestInstaller");
            Directory.CreateDirectory(backupFolder);
            plan.BackupPath = Path.Combine(backupFolder, "manifest-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff") + "Z.json");
            File.Copy(Path.Combine(project, "Packages/manifest.json"), plan.BackupPath, false);
            var urls = new string[plan.Packages.Length];
            for (int i = 0; i < urls.Length; i++) urls[i] = plan.Packages[i].Url;
            pending = plan;
            SessionState.SetString(PendingKey, JsonUtility.ToJson(plan));
            SetStatus("正在通过 Package Manager 批量安装主 SDK 和数数官方包，等待下载、解析和编译完成……", false);
            try { request = Client.AddAndRemove(urls, plan.RemovePackages); }
            catch (Exception error)
            {
                Fail("[BuglyInstaller][AddAndRemove] 无法启动批量安装：" + error.Message + "\n如 Package Manager 正在执行其他任务，请等待完成后重试。安装前依赖备份：" + plan.BackupPath);
                throw new InvalidOperationException(Status, error);
            }
        }

        private static void Update()
        {
            if (pending == null) return;
            if (request != null)
            {
                if (!request.IsCompleted) return;
                if (request.Status == StatusCode.Failure)
                {
                    string detail = request.Error == null ? "Package Manager 未提供错误详情。" : "错误码=" + request.Error.errorCode + "；" + request.Error.message;
                    Fail("[BuglyInstaller][AddAndRemove] 安装失败：" + detail + "\n请检查 Git、网络、仓库访问权限以及安装器所用提交是否已推送。安装前依赖备份：" + pending.BackupPath);
                    return;
                }
                request = null;
                SetStatus("包解析完成，等待 Unity 编译并验证安装版本与来源……", false);
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            if (EditorUtility.scriptCompilationFailed)
            {
                Fail("[BuglyInstaller][Compile] 包安装后出现脚本编译错误，请先修复 Console 中的错误。不会自动重复安装。安装前依赖备份：" + pending.BackupPath);
                return;
            }
            string issues;
            try { issues = InstallerPlan.Verify(pending, PackageInfo.GetAllRegisteredPackages()); }
            catch (Exception error) { Fail("[BuglyInstaller][Verify] 无法读取安装结果：" + error.Message); return; }
            if (string.IsNullOrEmpty(issues))
            {
                pending = null; request = null;
                SessionState.EraseString(PendingKey);
                SetStatus("安装完成，两个包的版本与 Git 来源已验证。请将主 SDK 的 Runtime/Prefabs/BuglySDK.prefab 拖入启动场景并填写配置。", false);
                Debug.Log("[BuglyInstaller] Install verified: Android-integrated main SDK and official ThinkingData; legacy adapter absent.");
                return;
            }
            if (DateTime.UtcNow.Ticks > pending.DeadlineUtcTicks)
                Fail("[BuglyInstaller][Verify] 安装尚未通过验证：\n" + issues + "\n如下载或解析仍在进行，请等待 Package Manager 完成后重新打开安装器检查。安装前依赖备份：" + pending.BackupPath);
        }

        private static void Fail(string message)
        {
            pending = null; request = null;
            SessionState.EraseString(PendingKey);
            SetStatus(message, true);
            Debug.LogError(message);
        }

        internal static void ShowError(Exception error)
        {
            SetStatus(error.Message, true);
            Debug.LogException(error);
        }

        private static void SetStatus(string message, bool failed)
        {
            SessionState.SetString(StatusKey, message);
            SessionState.SetBool(FailedKey, failed);
            if (Changed != null) Changed();
        }
    }
}
