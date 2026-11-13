using System;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ZeroGame.Networking.Installer
{
    [Serializable]
    internal sealed class PackageTarget
    {
        public string Name;
        public string DisplayName;
        public string Version;
        public string Url;
        public string Repository;
        public string Commit;
    }

    [Serializable]
    internal sealed class InstallPlan
    {
        public PackageTarget[] Packages;
        public string[] RemovePackages = new string[0];
        public long DeadlineUtcTicks;
        public string BackupPath;
    }

    internal static class InstallerPlan
    {
        internal const string LegacyAdapterName = "cn.com.zerogame.bugly-android";
        internal const string InstallerName = "cn.com.zerogame.bugly-webrequest.installer";
        internal const string Repository = "https://github.com/linfuqing/BuglyWebRequest.git";
        internal const string ThinkingRepository = "https://github.com/ThinkingDataAnalytics/unity-sdk.git";

        internal static InstallPlan Create(PackageInfo installer)
        {
            if (installer == null)
                throw new InvalidOperationException("[BuglyInstaller][Plan] 无法找到安装器包。请通过 Package Manager 安装 Installer~，不要单独复制安装脚本。");
            string repository = Repository;
            string commit = "";
            if (installer.source == PackageSource.Git)
            {
                repository = RepositoryFromLocator(Locator(installer));
                commit = installer.git == null ? "" : installer.git.hash;
                if (string.IsNullOrEmpty(commit))
                    throw new InvalidOperationException("[BuglyInstaller][Plan] 安装器的 Git 提交信息尚未就绪；请等待 Package Manager 完成解析后重试。");
            }
            string revision = string.IsNullOrEmpty(commit) ? "" : "#" + commit;
            return new InstallPlan {
                DeadlineUtcTicks = DateTime.UtcNow.AddMinutes(10).Ticks,
                Packages = new[] {
                    new PackageTarget { Name = "com.thinkingdata.analytics", DisplayName = "数数官方 SDK", Version = "3.5.0",
                        Repository = ThinkingRepository, Commit = "", Url = ThinkingRepository + "#v3.5.0" },
                    new PackageTarget { Name = "cn.com.zerogame.bugly-webrequest", DisplayName = "Bugly WebRequest 主 SDK", Version = installer.version,
                        Repository = repository, Commit = commit, Url = repository + revision }
                }
            };
        }

        internal static PackageInfo Find(PackageInfo[] installed, string name)
        {
            foreach (var package in installed) if (package.name == name) return package;
            return null;
        }

        internal static void CheckConflicts(InstallPlan plan, PackageInfo[] installed, bool allowVersionReplacement)
        {
            var oldAdapter = Find(installed, LegacyAdapterName);
            if (oldAdapter != null && (oldAdapter.source == PackageSource.Embedded || oldAdapter.source == PackageSource.Local || oldAdapter.source == PackageSource.LocalTarball))
                throw new InvalidOperationException("[BuglyInstaller][Conflict] 旧 Android 独立适配包来自本地或内嵌源码：" + oldAdapter.resolvedPath +
                    "。Android 功能已合并到主 SDK，请先自行备份并移走旧包/移除本地引用，避免重复程序集；安装器不会删除本地源码。");
            foreach (var target in plan.Packages)
            {
                var current = Find(installed, target.Name);
                if (current == null) continue;
                if (current.source == PackageSource.Embedded || current.source == PackageSource.Local || current.source == PackageSource.LocalTarball)
                    throw new InvalidOperationException("[BuglyInstaller][Conflict] " + target.Name + " 当前来自本地或内嵌包：" + current.resolvedPath +
                        "。安装器不会删除或覆盖本地源码。请先自行备份并移走该包/移除其本地引用，再点击安装；如正在本地开发，可继续使用现有包而无需运行安装器。");
                if (!string.Equals(current.version, target.Version, StringComparison.Ordinal) && !allowVersionReplacement)
                    throw new InvalidOperationException("[BuglyInstaller][Conflict] " + target.DisplayName + " 已安装 " + current.version +
                        "，本次需要 " + target.Version + "。如确认替换，请勾选“允许替换已有的不同版本”后重新安装；该操作可能降级现有版本。");
            }
        }

        internal static string Verify(InstallPlan plan, PackageInfo[] installed)
        {
            var issues = new List<string>();
            if (Find(installed, LegacyAdapterName) != null) issues.Add("旧 Android 独立适配包仍存在，可能造成重复程序集；请移除 cn.com.zerogame.bugly-android。");
            foreach (var target in plan.Packages)
            {
                var package = Find(installed, target.Name);
                if (package == null) { issues.Add(target.DisplayName + " 尚未加载"); continue; }
                if (package.version != target.Version) { issues.Add(target.DisplayName + " 当前版本 " + package.version + "，需要 " + target.Version); continue; }
                if (package.source != PackageSource.Git) { issues.Add(target.DisplayName + " 来源为 " + package.source + "，需要 Git 来源"); continue; }
                if (!string.Equals(RepositoryFromLocator(Locator(package)), target.Repository, StringComparison.OrdinalIgnoreCase))
                { issues.Add(target.DisplayName + " 的仓库来源与安装计划不一致"); continue; }
                if (!string.IsNullOrEmpty(target.Commit) && (package.git == null || package.git.hash != target.Commit))
                    issues.Add(target.DisplayName + " 尚未切换到安装器对应的提交 " + target.Commit);
                if (package.errors != null && package.errors.Length != 0)
                    issues.Add(target.DisplayName + " 存在 Package Manager 错误，请查看 Console");
            }
            return string.Join("\n", issues.ToArray());
        }

        internal static string Locator(PackageInfo package)
        {
            string prefix = package.name + "@";
            if (string.IsNullOrEmpty(package.packageId) || !package.packageId.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException("[BuglyInstaller][Source] 无法读取包 " + package.name + " 的安装地址。");
            return package.packageId.Substring(prefix.Length);
        }

        internal static string RepositoryFromLocator(string locator)
        {
            if (string.IsNullOrWhiteSpace(locator)) throw new ArgumentException("Git 包地址不能为空。", "locator");
            int end = locator.IndexOfAny(new[] { '?', '#' });
            return end < 0 ? locator : locator.Substring(0, end);
        }
    }
}
