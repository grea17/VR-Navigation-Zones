using UnityEngine;
using System.IO;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class ConfigLoader : MonoBehaviour
{
    [Header("Config file path (relativo ad Application.dataPath/PedPy/)")]
    [SerializeField] private string configFileName = "config.json";

    void Awake()
    {
        string path = Path.Combine(Application.dataPath, "PedPy", configFileName);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[ConfigLoader] config.json non trovato in {path}");
            return;
        }

        string json = File.ReadAllText(path);
        ConfigData config = JsonUtility.FromJson<ConfigData>(json);

        Debug.Log($"[ConfigLoader] Carico config #{config.configNumber} — scena: {config.sceneName}");

        // Se la scena attualmente aperta NON è quella richiesta dal JSON, caricala!
        if (SceneManager.GetActiveScene().name != config.sceneName)
        {
            Debug.Log($"[ConfigLoader] Cambio scena in automatico verso: {config.sceneName}");
            SceneManager.LoadScene(config.sceneName);
            return; // Interrompe l'Awake qui, verrà rieseguito nella nuova scena
        }

        // Se siamo già nella scena giusta, applica i parametri
        ApplyToAgent(config);
        ApplyToWarningZones(config);
        ApplyToTransitionZones(config);
    }

    private void ApplyToAgent(ConfigData config)
    {
        RLAgentPlanning[] agents = FindObjectsOfType<RLAgentPlanning>();
        foreach (var agent in agents)
        {
            agent.angularSpeed           = config.angularSpeed;
            agent.warningSpeedMultiplier = config.warningSpeedMultiplier;
            agent.campione               = config.campione;
            agent.posObiettivo = config.posObiettivo;
            agent.configNumber = config.configNumber;
            Debug.Log($"[ConfigLoader] configNumber impostato: {agent.configNumber}");
            Debug.Log($"[ConfigLoader] Agente {agent.name}: angularSpeed={config.angularSpeed}, " +
                      $"warningSpeedMultiplier={config.warningSpeedMultiplier}, campione={config.campione}");
        }
    }

    private void ApplyToWarningZones(ConfigData config)
    {
        // Trova tutti i WarningZoneTrigger nella scena
        WarningZoneTrigger[] zones = FindObjectsOfType<WarningZoneTrigger>();
        foreach (var zone in zones)
        {
            zone.SetWarningRadius(config.wzRadius);
            Debug.Log($"[ConfigLoader] WZ {zone.name}: radius={config.wzRadius}");
        }
    }

    private void ApplyToTransitionZones(ConfigData config)
    {
        // Trova tutti gli ArtifactTrigger nella scena
        ArtifactTrigger[] triggers = FindObjectsOfType<ArtifactTrigger>();
        foreach (var trigger in triggers)
        {
            SphereCollider sc = trigger.GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.radius = config.tzRadius;
                Debug.Log($"[ConfigLoader] TZ {trigger.name}: radius={config.tzRadius}");
            }
        }
    }

    [System.Serializable]
    private class ConfigData
    {
        public int    configNumber;
        public string sceneName;
        public float  angularSpeed;
        public float  warningSpeedMultiplier;
        public float  wzRadius;
        public float  tzRadius;
        public string posObiettivo;
        public int    campione;
    }
}