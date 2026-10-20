/* WarningZoneTrigger.cs
This class provides early warning to agents approaching an artifact, enabling anticipatory behavior
and smooth transitions before entering the main artifact trigger zone.
Features assignment checking, cooldown management, and overlap zone handling.
*/
using System;
using UnityEngine;
using System.Collections.Generic;

public class WarningZoneTrigger : MonoBehaviour
{
    private HashSet<GameObject> agentsInWarningZone = new HashSet<GameObject>();
    [SerializeField] private bool debugging = false;
    [SerializeField] private Artifact targetArtifact;

    [Header("Zone Configuration")]
    [SerializeField] private float warningRadius = 7f;

    [Header("Visual Settings")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.92f, 0.016f, 0.4f);
    [SerializeField] private Color wireColor = new Color(1f, 0.5f, 0f, 1f);
    [SerializeField] private bool showSolidSphere = true;
    [SerializeField] private bool showWireSphere = true;
    [Header("Zone Center")]
    [SerializeField] private Transform centerTarget;

    // Events for agent entering and exiting the warning zone
    public static event Action<GameObject, Artifact> OnAgentEnterWarning;
    public static event Action<GameObject, Artifact> OnAgentExitWarning;

    private Artifact GetTargetArtifact()
    {
        if (targetArtifact == null)
        {
            targetArtifact = GetComponentInParent<Artifact>();
            if (targetArtifact == null)
                targetArtifact = GetComponent<Artifact>();
        }
        return targetArtifact;
    }

    private void Reset()
    {
        SetupCollider();
    }

    void OnValidate()
    {
        SetupCollider();
    }

    private void Start()
    {
        SetupCollider();
    }

    private void SetupCollider()
    {
        float parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
        transform.localScale = Vector3.one / parentScale;
    
        SphereCollider collider = GetComponent<SphereCollider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<SphereCollider>();
        }

        collider.isTrigger = true;
        collider.radius = warningRadius;

        if (centerTarget != null)
        {
            SphereCollider targetCollider = centerTarget.GetComponent<SphereCollider>();
    
            Vector3 worldCenter = targetCollider != null
                ? centerTarget.TransformPoint(targetCollider.center)
                : centerTarget.position;
    
            Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
            collider.center = localCenter;
        }
    
        if (debugging)
        {
            Artifact artifact = GetTargetArtifact();
            Debug.Log($"[WarningZoneTrigger] Setup complete for {artifact?.ArtifactName ?? gameObject.name}");
            Debug.Log($"[WarningZoneTrigger] Local scale: {transform.localScale}, World scale: {transform.lossyScale}");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[WarningZone] Something entered: {other.name}, tag: {other.tag}");
        if (other.CompareTag("Agente"))
        {
            RLAgentPlanning agent = other.GetComponentInParent<RLAgentPlanning>();
            if (agent == null) return;

            if (agentsInWarningZone.Contains(agent.gameObject)) return;
            agentsInWarningZone.Add(agent.gameObject);

            Artifact artifact = GetTargetArtifact();
            if (artifact != null)
            {
                if (debugging)
                    Debug.Log($"[WarningZoneTrigger] Agent {other.name} entering warning zone of '{artifact.ArtifactName}'");

                // Emit the event - the agent will handle assignment check, cooldown, etc.
                OnAgentEnterWarning?.Invoke(other.gameObject, artifact);
            }
            else
            {
                if (debugging)
                    Debug.LogWarning($"[WarningZoneTrigger] No target artifact found for {gameObject.name}");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Agente"))
        {
            RLAgentPlanning agent = other.GetComponentInParent<RLAgentPlanning>();
            if (agent == null) return;
            SphereCollider sc = GetComponent<SphereCollider>();
            bool stillInside = false;
            Collider[] hits = Physics.OverlapSphere(transform.position, sc.radius);
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<RLAgentPlanning>()?.gameObject == agent.gameObject)
                {
                    stillInside = true;
                    break;
                }
            }

            if (!stillInside)
            {
                agentsInWarningZone.Remove(agent.gameObject);
                Artifact artifact = GetTargetArtifact();
                if (artifact != null)
                    OnAgentExitWarning?.Invoke(agent.gameObject, artifact);
            }
        }
    }

    void OnDrawGizmos()
    {
        // Usa il center del collider se disponibile, altrimenti transform.position
        SphereCollider col = GetComponent<SphereCollider>();
        Vector3 center = col != null
            ? transform.TransformPoint(col.center)  // converte local → world
            : transform.position;

        if (showSolidSphere)
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(center, warningRadius);
        }

        if (showWireSphere)
        {
            Gizmos.color = wireColor;
            Gizmos.DrawWireSphere(center, warningRadius);
        }
    }
    public float WarningRadius => warningRadius;

    public void RemoveAgent(GameObject agent)
    {
        agentsInWarningZone.Remove(agent);
    }   

    public void SetWarningRadius(float radius)
    {
        warningRadius = radius;
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc != null) sc.radius = radius;
    }
    
}