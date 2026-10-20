using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class SceneData {
    public List<ElementData> Items = new List<ElementData>();
}

[System.Serializable]
public class ElementData {
    public string name;
    public string type;
    public float x;
    public float y;
    public float size_x;
    public float size_y;
    public float rotation;
    public float radius;
}

public class PedPySceneExporter : MonoBehaviour {
    void Start() {
        ExportBlueprint();
    }

    public void ExportBlueprint() {
        SceneData data = new SceneData();
        GameObject objectivesGroup = GameObject.Find("Objectives");
        GameObject targetsGroup = GameObject.Find("Targets");

        if (objectivesGroup) AddFromGroup(objectivesGroup, "Objective", data);
        if (targetsGroup) AddFromGroup(targetsGroup, "Target", data);

        ExportWarningZones(data);

        ExportTransitionZones(data);

        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(Application.dataPath, "PedPy/SceneBlueprint.json");
        File.WriteAllText(path, json);
        Debug.Log($"Blueprint exported: {data.Items.Count} active elements with position, size and rotation");
    }

    void AddFromGroup(GameObject group, string type, SceneData data)
    {
        foreach (Transform child in group.transform)
        {
            // Export only GameObjects that are active in the scene
            if (!child.gameObject.activeInHierarchy)
            {
                Debug.Log($"[Skipped] {child.name} (inactive)");
                continue;
            }

            data.Items.Add(new ElementData
            {
                name = child.name,
                type = type,
                x = child.position.x,
                y = child.position.z,
                size_x = child.lossyScale.x,
                size_y = child.lossyScale.z,
                rotation = child.eulerAngles.y,
                radius = 0
            });
        }
    }

    void ExportWarningZones(SceneData data) {
        WarningZoneTrigger[] warningZones = FindObjectsOfType<WarningZoneTrigger>();

        Debug.Log($"[Export] Found {warningZones.Length} Warning Zones");

        foreach (var wz in warningZones) {
            if (!wz.gameObject.activeInHierarchy) {
                Debug.Log($"[Skipped] WarningZone {wz.name} (inactive)");
                continue;
            }

            SphereCollider collider = wz.GetComponent<SphereCollider>();
            float radius = collider != null ? collider.radius : 7f;

            data.Items.Add(new ElementData {
                name = wz.gameObject.name + " (WZ)",
                type = "WarningZone",
                x = wz.transform.position.x,
                y = wz.transform.position.z,
                size_x = radius * 2, // Diameter, for compatibility
                size_y = radius * 2,
                rotation = 0, // Circular zones: rotation is irrelevant
                radius = radius
            });
        }
    }

    void ExportTransitionZones(SceneData data) {
        ArtifactTrigger[] triggers = FindObjectsOfType<ArtifactTrigger>();

        Debug.Log($"[Export] Found {triggers.Length} Artifact Triggers");

        foreach (var trigger in triggers) {
            if (!trigger.gameObject.activeInHierarchy) {
                Debug.Log($"[Skipped] ArtifactTrigger {trigger.name} (inactive)");
                continue;
            }

            SphereCollider collider = trigger.GetComponent<SphereCollider>();
            float radius = collider != null ? collider.radius : 2f;

            data.Items.Add(new ElementData {
                name = trigger.gameObject.name + " (TZ)",
                type = "TransitionZone",
                x = trigger.transform.position.x,
                y = trigger.transform.position.z,
                size_x = radius * 2,
                size_y = radius * 2,
                rotation = 0,
                radius = radius
            });
        }
    }
}
