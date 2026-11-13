using UnityEditor;
using UnityEngine;

namespace ZeroGame.Networking.Editor
{
    public static class SetupMenu
    {
        [MenuItem("Tools/Bugly WebRequest/Allow HTTP in Development Builds")]
        private static void EnableDevelopmentHttp()
        {
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            EditorUserBuildSettings.development = true;
            Debug.Log("[BuglyWebRequest] HTTP allowed for Development Builds only. Rebuild Android to apply.");
        }
    }
}
