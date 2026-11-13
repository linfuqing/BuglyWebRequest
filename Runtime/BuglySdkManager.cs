using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ZeroGame.Networking
{
    [DisallowMultipleComponent]
    public sealed class BuglySdkManager : MonoBehaviour
    {
        private static BuglySdkManager instance;
        [FormerlySerializedAs("settings")]
        public BuglySdkSettings Settings;
        [Obsolete("Use Settings instead.")]
        public BuglySdkSettings settings { get { return Settings; } set { Settings = value; } }
        private BuglySdkSettings applied;
        public bool HasConsent { get; private set; }
        public bool IsReady { get; private set; }
        public string LastError { get; private set; }

        public static BuglySdkManager Instance
        {
            get
            {
                if (instance == null) instance = FindObjectOfType<BuglySdkManager>();
                if (instance == null) instance = new GameObject("Bugly SDK Manager").AddComponent<BuglySdkManager>();
                return instance;
            }
        }
        public static BuglySdkSettings CurrentSettings
        {
            get { return instance != null && instance.applied != null ? instance.applied :
                instance != null && instance.Settings != null ? instance.Settings : Resources.Load<BuglySdkSettings>(BuglySdkSettings.ResourceName); }
        }
        internal static RequestOptions CreateRequestOptions()
        {
            var config = CurrentSettings;
            return config == null ? new RequestOptions() : config.CreateRequestOptions();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnStartup()
        {
            var config = Resources.Load<BuglySdkSettings>(BuglySdkSettings.ResourceName);
            if (config == null || !config.InitializeOnStartup) return;
            var manager = Instance;
            manager.Settings = config;
            // The host obtained consent before download; no consent record is persisted here.
            manager.InitializeAfterConsent();
            BuglySdk.LogInfo("[BuglyWebRequest] Startup initialization: ready=" + manager.IsReady +
                "; Bugly=" + BuglySdk.IsInitialized + "; ThinkingData=" + BuglySdk.ThinkingDataReady);
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            if (Settings == null) Settings = Resources.Load<BuglySdkSettings>(BuglySdkSettings.ResourceName);
            DontDestroyOnLoad(gameObject);
        }

        // UnityEvent entry point. This method itself represents the host's consent decision.
        public void AcceptPrivacyAndInitialize() { InitializeAfterConsent(); }
        public bool InitializeAfterConsent()
        {
            if (IsReady) return true;
            HasConsent = true;
            var config = applied != null ? applied : Settings != null ? Settings : CurrentSettings;
            string error = "";
            if (config == null || !config.Validate(out error))
            {
                LastError = config == null ? "未找到 SDK 配置资源。" : error;
                Debug.LogError("[BuglyWebRequest] " + LastError); return false;
            }
            if (applied == null) { applied = Instantiate(config); applied.hideFlags = HideFlags.DontSave; }
            IsReady = BuglySdk.InitializeAfterConsent(applied);
            LastError = IsReady ? "" : "部分 SDK 初始化失败，请检查对应日志；请求功能仍可使用。";
            return IsReady;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            if (applied != null) Destroy(applied);
        }
    }
}
