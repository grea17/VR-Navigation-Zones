/* ArtifactNavigationHandler.cs
    This script is given to the agent when it enters the trigger zone of an artifact.
    It handles the navigation to the artifact, interaction behavior, and exit navigation.
*/
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.VirtualTexturing;
using System.Collections;
using System;

public class ArtifactNavigationHandler : MonoBehaviour
{
    private NavMeshAgent navAgent;
    private Artifact targetArtifact;
    private Transform artifactDestination;
    private Transform exitDestination;

    private ArtifactNavigationData navigationData;
    private Action onComplete;
    private Action onInteractionComplete;
    

    [Header("Navigation Settings")]
    [SerializeField] private float reachedDistance = 0.5f;
    [SerializeField] private float interactionDelay = 0.2f;

    //[SerializeField] private bool debugging = true;


    
    private bool isNavigatingToArtifact = false;
    private bool hasInteractedWithArtifact = false;

    // For external access
    public bool IsNavigatingToArtifact => isNavigatingToArtifact;

    void Start()
    {
        navAgent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (navAgent == null || !navAgent.enabled) return;

        if (isNavigatingToArtifact && !hasInteractedWithArtifact)
        {
            CheckArtifactReached();
        }
    }

    /// <summary>
    /// Starts navigation to the artifact. (Similar to a Constructor)
    /// </summary>

    
    public void StartNavigation(Artifact artifact, Transform artifactDest, Transform exitDest)
    {

        targetArtifact = artifact;
        artifactDestination = artifactDest;
        exitDestination = exitDest;

        isNavigatingToArtifact = true;
        hasInteractedWithArtifact = false;

        Debug.Log($"[ArtifactNavigationHandler] Started navigation to artifact {artifact.ArtifactName}");
    }



/*
    /// <summary>
    /// Start navigation to artifact with interaction callback
    /// </summary>
    public void StartNavigation(ArtifactNavigationData? data, System.Action onNavigationComplete, System.Action onInteractionComplete)
    {
        navigationData = (ArtifactNavigationData)data;
        onComplete = onNavigationComplete;
        this.onInteractionComplete = onInteractionComplete; //

        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            Debug.LogError("[ArtifactNavigationHandler] NavMeshAgent not found");
            return;
        }

        isNavigatingToArtifact = true;

        if (debugging)
            Debug.Log($"[ArtifactNavigationHandler] Starting navigation for {gameObject.name}");

        StartCoroutine(NavigationCoroutine());
    }

    private IEnumerator NavigationCoroutine()
    {
        // Wait until close to destination
        while (isNavigatingToArtifact && navAgent.enabled && navAgent.remainingDistance > navAgent.stoppingDistance + 0.1f)
        {
            if (debugging && Time.frameCount % 50 == 0)
                Debug.Log($"[ArtifactNavigationHandler] Distance to destination: {navAgent.remainingDistance:F2}m");

            yield return null;
        }

        if (!isNavigatingToArtifact)
        {
            if (debugging)
                Debug.Log("[ArtifactNavigationHandler] Navigation cancelled");
            yield break;
        }

        if (debugging)
            Debug.Log($"[ArtifactNavigationHandler] Reached destination, starting interaction");

        // Stop navigation
        if (navAgent.enabled)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        // Start interaction
        ArtifactInteractionBehavior interactionBehavior = navigationData.artifact.GetComponentInChildren<ArtifactInteractionBehavior>();

        if (interactionBehavior != null)
        {
            // ✅ MODIFICA: Passa il callback che stoppa tracking NavMesh
            interactionBehavior.StartInteraction(gameObject, onInteractionComplete);

            // Wait for interaction to complete
            float waitTime = interactionBehavior.CallUseAfterAnimation ? interactionBehavior.InteractionDuration : 0f;

            if (debugging)
                Debug.Log($"[ArtifactNavigationHandler] Waiting {waitTime}s for interaction");

            yield return new WaitForSeconds(waitTime);
        }
        else
        {
            if (debugging)
                Debug.LogWarning("[ArtifactNavigationHandler] No ArtifactInteractionBehavior found");
        }

        // Call the artifact's Use() method if needed
        if (interactionBehavior == null || !interactionBehavior.CallUseAfterAnimation)
        {
            if (debugging)
                Debug.Log($"[ArtifactNavigationHandler] Calling Use() on {navigationData.artifact.ArtifactName}");

            int agentId = gameObject.GetInstanceID();    

            navigationData.artifact.Use(agentId);
        }

        // Complete navigation
        CompleteNavigation();
    }
    
    private void CompleteNavigation()
    {
        isNavigatingToArtifact = false;
        
        if (debugging)
            Debug.Log($"[ArtifactNavigationHandler] Navigation completed for {gameObject.name}");
        
        onComplete?.Invoke();
    }
*/

/*
    public void CancelNavigation()
    {
        if (debugging)
            Debug.Log($"[ArtifactNavigationHandler] Cancelling navigation for {gameObject.name}");

        isNavigatingToArtifact = false;
        StopAllCoroutines();

        if (navAgent != null && navAgent.enabled)
        {
            if (navAgent.isOnNavMesh)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
            }
            else if (debugging)
            {
                Debug.LogWarning("[ArtifactNavigationHandler] Agent not on NavMesh, skipping Stop/ResetPath");
            }
        }
    }
*/
    /// <summary>
    /// Checks if the agent has reached the artifact.
    /// This is called periodically in Update.
    /// </summary>
    private void CheckArtifactReached()
    {
        // This blocks if startDestination is not called
        if (artifactDestination == null) return;

        float distanceToArtifact = Vector3.Distance(transform.position, artifactDestination.position);

        if (distanceToArtifact <= reachedDistance || (!navAgent.pathPending && navAgent.remainingDistance < 0.5f))
        {
            Debug.Log($"[ArtifactNavigationHandler] Reached: distanza reale {distanceToArtifact:F2} m, " +
                        $"hasPath {navAgent.hasPath}, pathPending {navAgent.pathPending}, remainingDistance {navAgent.remainingDistance:F2}");
            OnArtifactReached();
        }
    }

    /// <summary>
    /// Called when the agent reaches the artifact.
    /// Handles interaction behavior and exit navigation.
    /// </summary>
    private void OnArtifactReached()
    {
        Debug.Log($"[ArtifactNavigationHandler] Reached artifact {targetArtifact.ArtifactName}");

        isNavigatingToArtifact = false;
        hasInteractedWithArtifact = true;


        // Check for interaction behavior on the artifact
        ArtifactInteractionBehavior interactionBehavior = targetArtifact.GetComponentInChildren<ArtifactInteractionBehavior>();
        if (interactionBehavior != null && interactionBehavior.CallUseAfterAnimation)
        {
            // Animation (Interaction) and then use the artifact
            interactionBehavior.StartInteraction(gameObject, () =>
            {
                if (this == null) return;
                onInteractionComplete?.Invoke();
                // Once the animation is done, handle the artifact use
                HandleArtifactUse();
                Invoke(nameof(StartExitNavigation), interactionDelay);
            });
        }
        else if (interactionBehavior != null && !interactionBehavior.CallUseAfterAnimation)
        { 
            HandleArtifactUse();
            interactionBehavior.StartInteraction(gameObject, () =>
            {              
                Invoke(nameof(StartExitNavigation), interactionDelay);
            });
        }
        else
        {
            onInteractionComplete?.Invoke();
            // No animation
            HandleArtifactUse();
            Invoke(nameof(StartExitNavigation), interactionDelay);
        }
    }

    // Effective usage of the artifact
    private void HandleArtifactUse()
    {
        // Let the agent handle the interaction based on artifact type
        ArtifactAgentManager artifactAgent = GetComponent<ArtifactAgentManager>();
        if (artifactAgent != null)
        {
            // CALL TO THE ARTIFACT AGENT MANAGER HERE
            artifactAgent.HandleArtifactInteraction(targetArtifact);
        }
        else
        {
            // Fallback to generic use
            int agentId = gameObject.GetInstanceID();
            targetArtifact.Use(agentId, "navigation_reached", gameObject);
        }
    }

    // For leaving the artifact area
    private void StartExitNavigation()
    {
        RLAgentPlanning agent = GetComponent<RLAgentPlanning>();
        if (agent != null)
        {
            agent.OnArtifactInteractionComplete(targetArtifact);
            targetArtifact.ReleaseUser(agent.gameObject);
        }

        if (exitDestination == null)
        {
            Debug.LogWarning($"[ArtifactNavigationHandler] No exit destination set for {gameObject.name}, returning to RL control immediately");
            return;
        }

        Debug.Log($"[ArtifactNavigationHandler] Starting navigation to exit");

        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.SetDestination(exitDestination.position);
        }
    }
    

    /*
    private void OnDisable()
    {
        CancelNavigation();
    }
*/
    void OnDestroy()
    {
        // Clean up any pending invokes
        CancelInvoke();
    }
}