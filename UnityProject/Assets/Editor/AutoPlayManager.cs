#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public static class AutoPlayManager
{
    static AutoPlayManager()
    {
        // Run Update on every editor tick
        EditorApplication.update += Update;
    }

    static void Update()
    {
        // Trigger file written by master_run.py: Assets/PedPy/start_play.txt
        string path = Path.Combine(Application.dataPath, "PedPy", "start_play.txt");

        if (!EditorApplication.isPlaying && File.Exists(path))
        {
            File.Delete(path); // Delete it so Play is pressed only once
            EditorApplication.isPlaying = true; // Enter Play mode
        }
    }
}
#endif