using UnityEngine;
using System.IO;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class ConfigLoader : MonoBehaviour
{
    [Header("Config file path (relative to Application.dataPath/PedPy/)")]
    [SerializeField] private string configFileName = "config.json";

    void Awake()
    {
        string path = Path.Combine(Application.dataPath, "PedPy", configFileName);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[ConfigLoader] config.json not found in {path}");
            return;
        }

        string json = File.ReadAllText(path);
        ConfigData config = JsonUtility.FromJson<ConfigData>(json);

        Debug.Log($"[ConfigLoader] Loading config #{config.configNumber}, scene: {config.sceneName}");

        // If the active scene is not the one requested in the JSON, load it
        if (SceneManager.GetActiveScene().name != config.sceneName)
        {
            Debug.Log($"[ConfigLoader] Switching to scene: {config.sceneName}");
            SceneManager.LoadScene(config.sceneName);
            return; // Awake runs again in the new scene
        }

        // Already in the right scene: apply the parameters
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
            Debug.Log($"[ConfigLoader] configNumber set: {agent.configNumber}");
            Debug.Log($"[ConfigLoader] Agent {agent.name}: angularSpeed={config.angularSpeed}, " +
                      $"warningSpeedMultiplier={config.warningSpeedMultiplier}, campione={config.campione}");
        }
    }

    private void ApplyToWarningZones(ConfigData config)
    {
        // Find all WarningZoneTriggers in the scene
        WarningZoneTrigger[] zones = FindObjectsOfType<WarningZoneTrigger>();
        foreach (var zone in zones)
        {
            zone.SetWarningRadius(config.wzRadius);
            Debug.Log($"[ConfigLoader] WZ {zone.name}: radius={config.wzRadius}");
        }
    }

    private void ApplyToTransitionZones(ConfigData config)
    {
        // Find all ArtifactTriggers (transition zones) in the scene
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