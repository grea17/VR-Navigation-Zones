/* SocialForceAvoidance.cs
Implements Helbing's Social Force Model for agent-to-agent avoidance.
*/
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class SocialForceAvoidance : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Radius to detect nearby agents")]
    [SerializeField] private float detectionRadius = 3f;

    [Tooltip("Layer mask for detecting other agents")]
    [SerializeField] private LayerMask agentLayer;

    [Header("Force Parameters")]
    [Tooltip("Strength of repulsive force (2-5 recommended)")]
    [Range(0f, 10f)]
    [SerializeField] private float repulsionStrength = 3f;

    [Tooltip("Range at which repulsion activates (1.5-2.5 recommended)")]
    [Range(0.5f, 5f)]
    [SerializeField] private float repulsionRange = 2f;

    [Tooltip("Enable avoidance system")]
    [SerializeField] private bool enableAvoidance = true;

    [Header("Performance")]
    [Tooltip("Update frequency (lower = better performance)")]
    [Range(1, 10)]
    [SerializeField] private int updateEveryNFrames = 2;

    [Header("Debug")]
    [SerializeField] private bool debugging = false;
    [SerializeField] private bool showGizmos = true;

    // References
    private Rigidbody rigidBody;
    private RLAgentPlanning agentPlanning;

    // Runtime data
    private List<Transform> nearbyAgents = new List<Transform>();
    private Vector3 avoidanceForce = Vector3.zero;
    private int frameCount = 0;

    void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        agentPlanning = GetComponent<RLAgentPlanning>();

        // Auto-detect agent layer if not set
        if (agentLayer == 0)
        {
            agentLayer = LayerMask.GetMask("Default");
        }
    }

    void FixedUpdate()
    {
        if (!enableAvoidance) return;

        // Update only every N frames for performance
        frameCount++;
        if (frameCount >= updateEveryNFrames)
        {
            frameCount = 0;
            DetectNearbyAgents();
            CalculateAvoidanceForce();
        }

        ApplyAvoidanceForce();
    }

    /// <summary>
    /// Detects nearby agents using OverlapSphere
    /// </summary>
    private void DetectNearbyAgents()
    {
        nearbyAgents.Clear();

        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, agentLayer);

        foreach (Collider col in colliders)
        {
            // Skip self
            if (col.gameObject == gameObject)
                continue;

            // Only consider objects with agent tag
            if (col.CompareTag("Agente"))
            {
                nearbyAgents.Add(col.transform);
            }
        }

        if (debugging && nearbyAgents.Count > 0)
            Debug.Log($"[SocialForce] {name} detected {nearbyAgents.Count} nearby agents");
    }

    /// <summary>
    /// Calculates repulsive force using Helbing's Social Force Model
    /// </summary>
    private void CalculateAvoidanceForce()
    {
        avoidanceForce = Vector3.zero;

        foreach (Transform otherAgent in nearbyAgents)
        {
            if (otherAgent == null) continue;

            Vector3 directionAway = transform.position - otherAgent.position;
            directionAway.y = 0; // Only horizontal avoidance

            float distance = directionAway.magnitude;

            // Only apply force if within repulsion range
            if (distance < repulsionRange && distance > 0.1f)
            {
                // Helbing's exponential repulsion formula
                // F = A * exp((r - d) / B)
                // A = strength, r = personal space, d = distance, B = decay rate

                float personalSpace = 1.0f; // Desired minimum distance
                float decayRate = 0.3f;

                float repulsionMagnitude = repulsionStrength * Mathf.Exp((personalSpace - distance) / decayRate);

                Vector3 repulsion = directionAway.normalized * repulsionMagnitude;
                avoidanceForce += repulsion;

                if (debugging)
                    Debug.Log($"[SocialForce] Repulsion from {otherAgent.name}: magnitude={repulsionMagnitude:F2}, distance={distance:F2}");
            }
        }
    }

    /// <summary>
    /// Applies the calculated avoidance force to the rigidbody
    /// </summary>
    private void ApplyAvoidanceForce()
    {
        if (avoidanceForce.magnitude < 0.01f) return;

        // Check if agent is using NavMesh (don't apply force during NavMesh navigation)
        if (agentPlanning != null && IsUsingNavMesh())
        {
            if (debugging)
                Debug.Log($"[SocialForce] Skipping avoidance - agent using NavMesh");
            return;
        }

        // Apply force
        rigidBody.AddForce(avoidanceForce, ForceMode.Force);

        if (debugging)
            Debug.Log($"[SocialForce] Applied avoidance force: {avoidanceForce.magnitude:F2}");
    }

    /// <summary>
    /// Checks if agent is currently using NavMesh navigation
    /// </summary>
    private bool IsUsingNavMesh()
    {
        // Check if NavMeshAgent is enabled
        UnityEngine.AI.NavMeshAgent navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        return navAgent != null && navAgent.enabled;
    }

    /// <summary>
    /// Public method to temporarily disable avoidance
    /// </summary>
    public void SetAvoidanceEnabled(bool enabled)
    {
        enableAvoidance = enabled;

        if (!enabled)
        {
            avoidanceForce = Vector3.zero;
        }
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Draw detection radius
        Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Draw repulsion range
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, repulsionRange);

        // Draw lines to nearby agents
        if (Application.isPlaying && nearbyAgents != null)
        {
            foreach (Transform agent in nearbyAgents)
            {
                if (agent == null) continue;

                float distance = Vector3.Distance(transform.position, agent.position);

                // Color based on distance: red = close, yellow = medium
                Color lineColor = distance < 1.5f ? Color.red : Color.yellow;
                lineColor.a = 0.5f;

                Gizmos.color = lineColor;
                Gizmos.DrawLine(transform.position + Vector3.up, agent.position + Vector3.up);
            }
        }

        // Draw avoidance force vector
        if (Application.isPlaying && avoidanceForce.magnitude > 0.1f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position + Vector3.up, avoidanceForce.normalized * 2f);
        }
    }
}