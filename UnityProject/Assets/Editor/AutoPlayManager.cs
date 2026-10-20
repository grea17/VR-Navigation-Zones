#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public static class AutoPlayManager
{
    static AutoPlayManager()
    {
        // Questo fa sì che Unity controlli costantemente questa funzione
        EditorApplication.update += Update;
    }

    static void Update()
    {
        // Ottieni il percorso esatto per Assets/PedPy/start_play.txt
        string path = Path.Combine(Application.dataPath, "PedPy", "start_play.txt");

        if (!EditorApplication.isPlaying && File.Exists(path))
        {
            File.Delete(path); // Cancellalo per non premerlo all'infinito
            EditorApplication.isPlaying = true; // PREME PLAY!
        }
    }
}
#endif