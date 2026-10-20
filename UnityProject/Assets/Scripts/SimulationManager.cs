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
        // Conta gli agenti all'avvio della simulazione
        //_totalAgents = FindObjectsOfType<RLAgentPlanning>().Length;
        _totalAgents = GameObject.FindGameObjectsWithTag("Agente").Length;
        _finishedAgents = 0;
        Debug.Log($"[SimulationManager] Agenti registrati: {_totalAgents}");
    }

    public void OnAgentFinished()
    {
        _finishedAgents++;
        Debug.Log($"[SimulationManager] {_finishedAgents}/{_totalAgents} agenti terminati");

        if (_finishedAgents >= _totalAgents)
            EndSimulation();
    }

    private void EndSimulation()
    {
        Debug.Log("[SimulationManager] Tutti gli agenti terminati — fine simulazione");

        string donePath = Path.Combine(Application.dataPath, "PedPy", "sim_done.txt");
        File.WriteAllText(donePath, "done");

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}