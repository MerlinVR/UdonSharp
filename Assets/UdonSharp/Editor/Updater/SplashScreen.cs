
#if UNITY_EDITOR

using UnityEditor;

namespace UdonSharp.Updater
{
    public class SplashScreen : EditorWindow
    {
        [MenuItem("Udon Sharp/Splash Screen", priority = 5)]
        static void Init()
        {
            GetWindow<SplashScreen>(true, "UdonSharp");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Hello I am a placeholder :D");
        }
    }
}

#endif
