using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class AIControlAgents1 : MonoBehaviour
{
    public action[] goalAction;
    private int currentTargetIndex = 0;
    private NavMeshAgent agent;
    private Animator animator;
    private int uniqueID;
    private bool reversed = false;
    private bool fleeing = false;
    private Transform fleeLocation;
    private bool isRecording = true;

    // Struct defining the path that the agent need to follow and all animation that need to be performed along it
    [Serializable]
    public struct action
    {
        public GameObject goalLocation;
        public float wait;
        public String animationName;
    }

    void Start()
    {
        uniqueID = Guid.NewGuid().ToString().GetHashCode();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        CompleteStop();
        if (goalAction.Length > 0)
        {
            Resume();
            InvokeRepeating("WritePedpyStats", 0, 0.3f);
            animator.SetTrigger("isWalking");
            agent.SetDestination(goalAction[currentTargetIndex].goalLocation.transform.position);
        }
    }

    private void Update()
    {
        animator.SetFloat("Speed", agent.velocity.magnitude/2);
    }

    // Start the evacuation for the agent by making it follow the evac point
    public void Flee(Transform EvacPoint)
    {
        fleeing = true;
        Resume();
        animator.SetTrigger("isWalking");
        fleeLocation = EvacPoint;
        agent.SetDestination(EvacPoint.position);
        agent.isStopped = false;
        agent.stoppingDistance = 2;
    }

    // Stops the agents completely so it can be still for certain animations
    // (e.g. when the agent start sitting in a chair outside the navmesh)
    public void CompleteStop()
    {
        agent.enabled = false;
        Collider[] colliders = GetComponents<Collider>();
        foreach(Collider c in colliders)
        {
            if (!c.isTrigger)
            {
                c.enabled = false;
            }
        }
    }

    // Reactivate the agent after CompleteStop is called
    public void Resume()
    {
        agent.enabled = true;
        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider c in colliders)
        {
            if (!c.isTrigger)
            {
                c.enabled = true;
            }
        }
    }

    private void WritePedpyStats()
    {
        if (!isRecording) return; // Se isRecording è false, smette di scrivere riga su riga

        StatsWriter.WritePedPyStats(
            transform.position.x,
            transform.position.y,
            transform.position.z,
            uniqueID);
    }

    IEnumerator MoveToNextTargetWithWait(float wait)
    {
        for (float timer = wait; timer >= 0; timer -= Time.deltaTime)
        {
            if (fleeing)
            {
                timer = 0;
            }
            yield return null;
        }
        if (!fleeing)
        {
            MoveToNextTarget();
        }
        else
        {
            agent.isStopped = false;
            animator.SetTrigger("isWalking");
            agent.angularSpeed = 180;
            agent.SetDestination(fleeLocation.position);
            agent.stoppingDistance = 2;
        }
    }

    private void MoveToNextTarget()
    {
        agent.isStopped = false;
        animator.SetTrigger("isWalking");
        agent.angularSpeed = 180;
        agent.SetDestination(goalAction[currentTargetIndex].goalLocation.transform.position);
    }
    
    // When the agent reach the next target in the action list wait for the set time and perform the animation corresponding to the reached target
    // and set the agent to reach the following target in the list.
    private void OnTriggerEnter(Collider other)
{
    // Controllo se l'oggetto toccato è un waypoint o un target valido
    if (other.CompareTag("waypoint") || other.CompareTag("Target") || other.CompareTag("Obiettivo"))
    {
        // Controllo se il target toccato è quello che l'agente sta effettivamente cercando
        if (currentTargetIndex >= 0 && currentTargetIndex < goalAction.Length && 
            other.gameObject.Equals(goalAction[currentTargetIndex].goalLocation))
        {
            // --- NUOVA LOGICA PER PEDPY ---
            // Se non siamo in modalità 'reversed' e l'indice attuale è l'ultimo della lista,
            // significa che abbiamo raggiunto il traguardo finale.
            if (!reversed && currentTargetIndex == goalAction.Length - 1)
            {
                isRecording = false; // Ferma la scrittura dei dati
                Debug.Log($"[PEDPY] {gameObject.name} ha raggiunto l'obiettivo finale. Tempo fermato.");
            }
            // ------------------------------

            agent.isStopped = true;

            // Gestione Animazioni
            if (goalAction[currentTargetIndex].animationName != "")
            {
                animator.SetTrigger(goalAction[currentTargetIndex].animationName);
            }
            else
            {
                animator.SetTrigger("isIdle");
            }

            agent.angularSpeed = 0;
            float wait = goalAction[currentTargetIndex].wait;

            // Calcolo del prossimo target (logica originale)
            if (reversed) currentTargetIndex--;
            else currentTargetIndex++;

            // Gestione inversione percorso (Ping-Pong)
            if (currentTargetIndex == goalAction.Length || currentTargetIndex == -1)
            {
                reversed = !reversed;
                if (reversed) currentTargetIndex -= 2;
                else currentTargetIndex += 2;
                
                // Opzionale: se vuoi riprendere a registrare quando torna indietro, 
                // potresti rimettere isRecording = true qui, ma per la tua analisi 
                // probabilmente è meglio lasciarlo fermo.
            }

            StartCoroutine(MoveToNextTargetWithWait(wait));
        }
    }
    else if (other.CompareTag("evac"))
    {
        isRecording = false; // Fermiamo il tempo anche in caso di evacuazione raggiunta
        animator.SetTrigger("isIdle");
        agent.isStopped = true;
    }
}
}


