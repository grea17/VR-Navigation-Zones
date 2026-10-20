/*
using UnityEngine;
using UnityEngine.AI;
using System.IO;

[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshEpisodeEndChecker : MonoBehaviour
{
    public NavMeshAgent agent;
    private bool episodioFinito = false;

    void Start()
    {
        // Se dimentichi di assegnarlo nell'Inspector, lo prende in automatico
        if (agent == null) agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (episodioFinito) return;

        // Attenzione: remainingDistance a volte restituisce Infinity se il target è fuori dalla mesh.
        // Se l'agente non si ferma mai, il problema sarà qui.
        if (!agent.pathPending && 
            agent.remainingDistance <= agent.stoppingDistance && 
            (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f))
        {
            episodioFinito = true;
            agent.isStopped = true;

            Debug.Log("🎯 Episodio NavMesh terminato: target raggiunto.");

            // 1. Chiude correttamente la simulazione lato Unity
            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.OnAgentFinished();
            }

            // 2. Genera il file per sbloccare Python
            File.WriteAllText("sim_done.txt", "done");

            // 3. Ferma automaticamente la modalità Play nell'Editor
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }
    }
}
*/

using UnityEngine;
using UnityEngine.AI;
using System.IO;

[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshEpisodeEndChecker : MonoBehaviour
{
    public NavMeshAgent agent;
    
    [Header("Impostazioni Fine Episodio")]
    public Transform targetFinale; // L'oggetto traguardo
    public float raggioArrivo = 1.0f; // Distanza per considerare l'episodio finito

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
            // Calcola la distanza 2D (solo assi X e Z) tra l'agente e il target finale
            float distX = transform.position.x - targetFinale.position.x;
            float distZ = transform.position.z - targetFinale.position.z;
            float distanza2D = Mathf.Sqrt(distX * distX + distZ * distZ);

            // Se l'agente entra nel raggio del target finale, chiudi l'episodio
            if (distanza2D <= raggioArrivo)
            {
                TerminaEpisodio();
            }
        }
        else
        {
            Debug.LogWarning("NavMeshEpisodeEndChecker: Manca il 'Target Finale' nell'Inspector!");
        }
    }

    private void TerminaEpisodio()
    {
        episodioFinito = true;
        
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        Debug.Log("🎯 Episodio NavMesh terminato: target FINALE raggiunto.");

        // 1. Chiude correttamente la simulazione lato Unity
        if (SimulationManager.Instance != null)
        {
            SimulationManager.Instance.OnAgentFinished();
        }

        // 2. Genera il file per sbloccare Python
        File.WriteAllText(@"C:\Users\gererea\VR_Navigation\VR_Navigation\Assets\PedPy\sim_done.txt", "done");

        // 3. Ferma automaticamente la modalità Play nell'Editor
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}