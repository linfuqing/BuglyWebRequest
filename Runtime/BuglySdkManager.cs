using System;
using UnityEngine;

namespace ZeroGame.Networking
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    [AddComponentMenu("Bugly WebRequest/SDK Configuration")]
    public sealed class BuglySdkManager : MonoBehaviour
    {
        private static BuglySdkManager instance;
        public BuglySdkSettings Settings = new BuglySdkSettings();
        private BuglySdkSettings applied;
        public bool HasConsent { get; private set; }
        public bool IsReady { get; private set; }
        public string LastError { get; private set; }

        public static BuglySdkManager Instance
        {
            get
            {
                if (instance == null) instance = FindObjectOfType<BuglySdkManager>();
                if (instance == null)
                    throw new InvalidOperationException("请先将 SDK 的 Runtime/Prefabs/BuglyWebRequest.prefab 拖入启动场景。");
                return instance;
            }
        }

        public static BuglySdkSettings CurrentSettings
        {
            get
            {
                var manager = instance != null ? instance : FindObjectOfType<BuglySdkManager>();
                return manager == null ? null : manager.applied ?? manager.Settings;
            }
        }

        internal static RequestOptions CreateRequestOptions()
        {
            var config = CurrentSettings;
            return config == null ? new RequestOptions() : config.CreateRequestOptions();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            if (Settings == null) Settings = new BuglySdkSettings();
            // A nested prefab must not preserve the host's unrelated scene hierarchy.
            if (transform.parent != null) transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (instance != this || Settings == null || !Settings.InitializeOnStartup) return;
            InitializeAfterConsent();
            BuglySdk.LogInfo("[BuglyWebRequest] Prefab startup initialization: ready=" + IsReady +
                "; Bugly=" + BuglySdk.IsInitialized + "; ThinkingData=" + BuglySdk.ThinkingDataReady);
        }

        public void AcceptPrivacyAndInitialize() { InitializeAfterConsent(); }

        public bool InitializeAfterConsent()
        {
            if (instance != this) return Instance.InitializeAfterConsent();
            if (IsReady) return true;
            var config = applied ?? Settings;
            string error = "";
            if (config == null || !config.Validate(out error))
            {
                LastError = config == null ? "请在场景中的 Bugly SDK 组件填写配置。" : error;
                Debug.LogError("[BuglyWebRequest] " + LastError); return false;
            }
            HasConsent = true;
            if (applied == null) applied = config.Snapshot();
            IsReady = BuglySdk.InitializeAfterConsent(applied);
            LastError = IsReady ? "" : BuglySdk.LastInitializationError;
            return IsReady;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            applied = null;
        }
    }
}
