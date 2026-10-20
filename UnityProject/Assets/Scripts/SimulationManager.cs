using UnityEngine;
using System.IO;

public class SimulationManager : MonoBehaviour
{
    public static SimulationManager Instance;

    private int _totalAgents;
    private int _finishedAgents;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Count the agents at the start of the simulation
        _totalAgents = GameObject.FindGameObjectsWithTag("Agente").Length;
        _finishedAgents = 0;
        Debug.Log($"[SimulationManager] Registered agents: {_totalAgents}");
    }

    public void OnAgentFinished()
    {
        _finishedAgents++;
        Debug.Log($"[SimulationManager] {_finishedAgents}/{_totalAgents} agents finished");

        if (_finishedAgents >= _totalAgents)
            EndSimulation();
    }

    private void EndSimulation()
    {
        Debug.Log("[SimulationManager] All agents finished, ending simulation");

        string donePath = Path.Combine(Application.dataPath, "PedPy", "sim_done.txt");
        File.WriteAllText(donePath, "done");

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}