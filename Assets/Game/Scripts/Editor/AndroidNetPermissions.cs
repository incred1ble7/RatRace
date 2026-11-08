#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;

namespace MouseGame.EditorTools
{
    /// <summary>
    /// Adds the Wi‑Fi permissions LAN discovery needs on Android (multicast lock for broadcast packets).
    /// Runs automatically after Unity generates the Gradle project — no custom manifest to maintain.
    /// </summary>
    public class AndroidNetPermissions : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            string manifest = Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest)) return;
            string xml = File.ReadAllText(manifest);
            string[] perms =
            {
                "android.permission.INTERNET",
                "android.permission.ACCESS_NETWORK_STATE",
                "android.permission.ACCESS_WIFI_STATE",
                "android.permission.CHANGE_WIFI_MULTICAST_STATE",
            };
            string add = "";
            foreach (var p in perms)
                if (!xml.Contains("\"" + p + "\"")) add += "\n  <uses-permission android:name=\"" + p + "\" />";
            if (add.Length == 0) return;
            int i = xml.IndexOf("<application");
            if (i < 0) return;
            xml = xml.Insert(i, add.TrimStart('\n') + "\n  ");
            File.WriteAllText(manifest, xml);
        }
    }
}
#endif
