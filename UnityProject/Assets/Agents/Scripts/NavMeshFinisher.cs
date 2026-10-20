using UnityEngine;
using UnityEngine.AI;
using System.IO;

[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshEpisodeEndChecker : MonoBehaviour
{
    public NavMeshAgent agent;

    [Header("Episode end settings")]
    public Transform targetFinale; // Final target object
    public float raggioArrivo = 1.0f; // Distance at which the episode ends

    private bool episodioFinito = false;

    void Start()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (episodioFinito) return;

        if (targetFinale != null)
        {
            // 2D distance (X and Z only) between the agent and the final target
            float distX = transform.position.x - targetFinale.position.x;
            float distZ = transform.position.z - targetFinale.position.z;
            float distanza2D = Mathf.Sqrt(distX * distX + distZ * distZ);

            // End the episode when the agent is within the arrival radius
            if (distanza2D <= raggioArrivo)
            {
                TerminaEpisodio();
            }
        }
        else
        {
            Debug.LogWarning("NavMeshEpisodeEndChecker: 'Target Finale' is not set in the Inspector");
        }
    }

    private void TerminaEpisodio()
    {
        episodioFinito = true;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        Debug.Log("NavMesh episode ended: final target reached.");

        // 1. Notify the simulation manager
        if (SimulationManager.Instance != null)
        {
            SimulationManager.Instance.OnAgentFinished();
        }

        // 2. Write the file that unblocks the Python pipeline
        File.WriteAllText(@"C:\Users\gererea\VR_Navigation\VR_Navigation\Assets\PedPy\sim_done.txt", "done");

        // 3. Exit Play mode in the Editor
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}