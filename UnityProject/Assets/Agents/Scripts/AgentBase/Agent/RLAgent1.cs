using System;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.AI;

[RequireComponent(typeof(RLAgentAnimationManager))]
public class RLAgent1 : Agent, IAgentRLBase
{
    [Serializable]
    public struct action
    {
        public GameObject goalLocation;
        public List<AgentAnimationAction> animations; // usa il tipo condiviso
    }

    public action[] goalAction;

    // Artifacts
    [Header("Artifacts")]
    [SerializeField] private List<Artifact> _assignedArtifacts = new List<Artifact>();
    public List<Artifact> assignedArtifacts => _assignedArtifacts;

    private bool walking = true;
    private int nextTargetCount = 0;

    public IAgentConstants constants { get; private set; }

    public Group group;

    private NavMeshObstacle navMeshObstacle;

    private Vector2 minMaxSpeed;

    private float currentSpeed;

    private bool isPlayingAnimationSequence = false;

    private float newAngle;

    private bool fleeing = false;

    /// <summary>
    /// True if the agent is using NavMesh for movement.
    /// </summary>
    [System.NonSerialized]
    private bool isUsingNavMesh = false;

    [NonSerialized] public Vector3 startPosition;
    [NonSerialized] public Quaternion startRotation;

    private Rigidbody rigidBody;

    private Animator animator;
    private RLAgentAnimationManager animationManager;

    private Vector2 speedMaxRange;

    private List<GameObject> _targetsTaken = new List<GameObject>();

    public List<GameObject> targetsTaken => _targetsTaken;

    private AgentSensorsManager1 agentSensorsManager;
    private AgentGizmosDrawer1 agentGizmosDrawer;
    private AgentObserver1 agentObserver;

    private List<float> wallsAndTargetsObservations;
    private List<float> wallsAndAgentsObservations;

    private List<(GizmosTag, Vector3)> wallsAndAgents = new List<(GizmosTag, Vector3)>();
    private List<(GizmosTag, Vector3)> wallsAndTargets = new List<(GizmosTag, Vector3)>();

    public event Action<float, Environment> agentTerminated;
    public event Action<RLAgent1> resetAgent;

    private string uniqueID;
    public int envStep;
    private int stepLeft;

    private int numberOfIteraction;
    private float cumulativeReward;
    public bool lockPosition;
    private int tempoIniziale;

    [NonSerialized] public Environment env;

    float entryValue;
    float exitValue;
    [NonSerialized] public string envID;

    private float lastYRotation;

    public override void Initialize()
    {
        animator = GetComponent<Animator>();
        animationManager = GetComponent<RLAgentAnimationManager>();
        agentSensorsManager = GetComponent<AgentSensorsManager1>();
        agentGizmosDrawer = GetComponent<AgentGizmosDrawer1>();
        agentObserver = GetComponent<AgentObserver1>();
        rigidBody = GetComponent<Rigidbody>();
        navMeshObstacle = GetComponent<NavMeshObstacle>();
        startPosition = transform.position;
        startRotation = transform.rotation;

        if (animationManager == null) Debug.LogError("[RLAgent1] RLAgentAnimationManager mancante!");
        if (agentSensorsManager == null) Debug.LogError("[RLAgent1] AgentSensorsManager1 mancante!");
        if (agentGizmosDrawer == null) Debug.LogError("[RLAgent1] AgentGizmosDrawer1 mancante!");
        if (agentObserver == null) Debug.LogError("[RLAgent1] AgentObserver1 mancante!");
        if (rigidBody == null) Debug.LogError("[RLAgent1] Rigidbody mancante!");

        // Constants — devono essere pronte prima di CollectObservations
        constants = new ConstantsBase();
        if (agentSensorsManager != null) agentSensorsManager.constants = this.constants;
        if (agentObserver != null) agentObserver.constants = this.constants;
        if (agentGizmosDrawer != null) agentGizmosDrawer.constants = this.constants;
        minMaxSpeed = constants.minMaxSpeed;
        speedMaxRange = constants.speedMaxRange;

        if (agentSensorsManager != null) agentSensorsManager.UpdateTargetSensorVision(group);
    }

    private void Start()
    {
        // Questa logica va in Start() perché dipende da invisibleTargets
        // che viene inizializzato in Awake() di AgentSensorsManager1
        if (goalAction.Length >= 1 && goalAction[0].goalLocation != null)
        {
            if (animationManager != null) animationManager.SetWalking(true);
            if (agentSensorsManager != null && agentSensorsManager.invisibleTargets != null)
                agentSensorsManager.invisibleTargets.Remove(goalAction[0].goalLocation);
        }
    }

    private void Update()
    {
        // No animations if the agent is playing an animation sequence
        if (isPlayingAnimationSequence) return;

        float speed = GetCurrentSpeed();
        animationManager.UpdateSpeed(speed / 10);

        // Idle/Walking
        if (speed < 0.25f)
        {
            Debug.Log("Animation State: Idle");
            animationManager.SetWalking(false);

            float currentYRotation = transform.eulerAngles.y;
            float deltaY = Mathf.DeltaAngle(lastYRotation, currentYRotation);
            float angularSpeed = Mathf.Abs(deltaY) / Time.deltaTime;

            // Turn da fermo (rotazione minima) - usa turn speed dinamica
            if (Mathf.Abs(deltaY) > 10f)
            {
                Debug.Log("Animation State: Turn");
                float normalizedTurnSpeed = Mathf.Clamp(angularSpeed / 90f, 0.5f, 1.0f);
                if (deltaY > 0)
                    animationManager.PlayTurn(true, normalizedTurnSpeed); // TurnRight
                else
                    animationManager.PlayTurn(false, normalizedTurnSpeed); // TurnLeft
            }
        }
        else
        {
            Debug.Log("Animation State: Walking");
            // Se sta camminando, ferma immediatamente le animazioni di turn
            animationManager.StopTurn();
            animationManager.SetWalking(true);
        }

        lastYRotation = transform.eulerAngles.y;
    }

    public void SetAnimationSequenceMode(bool isPlaying)
    {
        isPlayingAnimationSequence = isPlaying;
        Debug.Log($"Animation sequence mode set to: {isPlaying}");
    }

    public override void OnEpisodeBegin()
    {
        stepLeft = envStep;
        uniqueID = Guid.NewGuid().ToString();
        rigidBody.velocity = Vector3.zero;
        tempoIniziale = (int)Time.time;
        currentSpeed = 0;
        numberOfIteraction = 0;
        minMaxSpeed.y = RandomGaussian(speedMaxRange.x, speedMaxRange.y);
        resetAgent?.Invoke(this);

        // Reset sequenza objectives: rende visibile solo il primo
        Transform objectivesParent = GameObject.Find("Objectives")?.transform;
        if (objectivesParent != null && agentSensorsManager != null)
        {
            for (int i = 0; i < objectivesParent.childCount; i++)
            {
                GameObject obj = objectivesParent.GetChild(i).gameObject;
                if (i == 0)
                    agentSensorsManager.invisibleTargets.Remove(obj);  // primo visibile
                else if (!agentSensorsManager.invisibleTargets.Contains(obj))
                    agentSensorsManager.invisibleTargets.Add(obj);     // altri nascosti
            }
        }
    }

    public override void CollectObservations(VectorSensor vectorSensor)
    {
        // Null check — se mancano componenti esci subito con log
        if (agentSensorsManager == null) { Debug.LogError("[RLAgent1] agentSensorsManager NULL in CollectObservations!"); return; }
        if (agentObserver == null)       { Debug.LogError("[RLAgent1] agentObserver NULL in CollectObservations!"); return; }
        if (agentGizmosDrawer == null)   { Debug.LogError("[RLAgent1] agentGizmosDrawer NULL in CollectObservations!"); return; }
        if (constants == null)           { Debug.LogError("[RLAgent1] constants NULL in CollectObservations!"); return; }
        // Ottieni i risultati dei sensori
        Dictionary<AgentSensorsManager1.Sensore, RaycastHit[]> sensorsResults = agentSensorsManager.ComputeSensorResults();

        // Aggiorna le osservazioni degli oggetti
        wallsAndTargets = agentObserver.WallsAndTargetsGizmos;
        wallsAndAgents = agentObserver.WallsAndAgentsGizmos;

        // Calcola le osservazioni per i muri e gli agenti
        (wallsAndTargetsObservations, wallsAndAgentsObservations) = agentObserver.ComputeObservations(sensorsResults);

        // Calcola la velocità normalizzata
        float normalizedSpeed = (currentSpeed - minMaxSpeed.x) / (minMaxSpeed.y - minMaxSpeed.x);

        // Setta i risultati delle osservazioni per il disegno delle gizmos
        agentGizmosDrawer.SetObservationsResults(wallsAndTargets, wallsAndAgents);

        // Aggiungi le osservazioni effettive
        vectorSensor.AddObservation(wallsAndTargetsObservations);
        vectorSensor.AddObservation(wallsAndAgentsObservations);
        vectorSensor.AddObservation(normalizedSpeed);

        // Aggiungi ricompense per le osservazioni
        rewardsWallsAndTargetsObservations(wallsAndTargets);
        rewardsWallsAndAgentsObservations(wallsAndAgents);

        /*// Aggiungi padding se necessario 
        int observationsCount = wallsAndTargetsObservations.Count + wallsAndAgentsObservations.Count + 1; // +1 per normalizedSpeed
        int requiredObservations = 185;

        // Aggiungi padding se il numero di osservazioni è inferiore alla dimensione richiesta
        for (int i = 0; i < requiredObservations - observationsCount; i++)
        {
            vectorSensor.AddObservation(0f); // Aggiungi valore neutro per il padding
        }*/
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (walking && !isUsingNavMesh)
        {
            float actionSpeed;
            float actionAngle;
            if (constants.discrete)
            {
                actionSpeed = actions.DiscreteActions[0];
                actionSpeed = (actionSpeed - 5f) / 5f;
                actionAngle = actions.DiscreteActions[1];
                actionAngle = (actionAngle - 5f) / 5f;
            }
            else
            {
                actionSpeed = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
                actionAngle = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
            }
            if (!lockPosition)
            {
                AngleChange(actionAngle);
                SpeedChange(actionSpeed);
            }

            numberOfIteraction++;
            StatsWriter.WriteAgentStats(
                transform.position.x,
                transform.position.z,
                group,
                currentSpeed,
                GetCurrentSpeed(),
                (actionAngle * constants.angleRange),
                envID,
                uniqueID,
                numberOfIteraction
            );
            StatsWriter.WritePedPyStats(
                transform.position.x,
                transform.position.y,
                transform.position.z,
                uniqueID.GetHashCode());
            ComputeSteps();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var cont = actionsOut.ContinuousActions;
        cont[0] = Input.GetAxis("Vertical");
        cont[1] = Input.GetAxis("Horizontal");
    }

    public void ComputeSteps()
    {
        AddReward(constants.step_reward);
        stepLeft--;
        if (stepLeft <= 0)
        {
            AddReward(constants.step_finished_reward);
            print("finished_step");
            Finished();
        }
    }

    public void Finished()
    {
        cumulativeReward = GetCumulativeReward();
        gameObject.SetActive(false);
        StatsWriter.WriteEnvStats(group, (int)(Time.time - tempoIniziale));
        targetsTaken.Clear();
        transform.position = startPosition;
        transform.rotation = startRotation;
        EndEpisode();
        agentTerminated?.Invoke(cumulativeReward, env);

    }

    //returns a number derived by a gaussian
    public static float RandomGaussian(float minValue = 0.0f, float maxValue = 1.0f)
    {
        float u, v, S;
        do
        {
            u = 2.0f * UnityEngine.Random.value - 1.0f;
            v = 2.0f * UnityEngine.Random.value - 1.0f;
            S = u * u + v * v;
        }
        while (S >= 1.0f);

        float std = u * Mathf.Sqrt(-2.0f * Mathf.Log(S) / S);

        float mean = (minValue + maxValue) / 2.0f;
        float sigma = (maxValue - mean) / 3.0f;
        return Mathf.Clamp(std * sigma + mean, minValue, maxValue);
    }

    public void MoveToNextTargetWithDelay(float delay)
    {
        animationManager.MoveToNextTargetWithDelay(delay);
    }

    public Rigidbody GetRigidBody() => rigidBody;

    public void MoveToNextTarget()
    {
        var sensors = gameObject.GetComponent<AgentSensorsManager1>();

        // Aggiungi il target corrente a invisibleTargets (se non già presente)
        if (goalAction.Length > nextTargetCount && goalAction[nextTargetCount].goalLocation != null)
            if (!sensors.invisibleTargets.Contains(goalAction[nextTargetCount].goalLocation))
                sensors.invisibleTargets.Add(goalAction[nextTargetCount].goalLocation);

        if (goalAction.Length >= nextTargetCount + 2 && goalAction[nextTargetCount + 1].goalLocation != null)
        {
            animationManager.SetWalking(true);
            nextTargetCount++;
            // Rimuovi il nuovo target dalla lista invisibleTargets (se presente)
            sensors.invisibleTargets.Remove(goalAction[nextTargetCount].goalLocation);
        }
        else
        {
            animationManager.SetWalking(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        GameObject reachedObject = other.gameObject;

        // Gestione Objectives (waypoint sequenziali con tag "Obiettivo")
        if (reachedObject.CompareTag("Obiettivo"))
        {
            Transform objectivesParent = GameObject.Find("Objectives")?.transform;
            if (objectivesParent != null)
            {
                for (int i = 0; i < objectivesParent.childCount; i++)
                {
                    GameObject obj = objectivesParent.GetChild(i).gameObject;
                    if (reachedObject == obj && !_targetsTaken.Contains(obj))
                    {
                        // Registra e premia
                        _targetsTaken.Add(obj);
                        AddReward(constants.new_target_reward);
                        Debug.Log($"[RLAgent] Objective raggiunto: {obj.name}");

                        // Nascondi questo, mostra il prossimo
                        agentSensorsManager.invisibleTargets.Add(obj);
                        if (i + 1 < objectivesParent.childCount)
                        {
                            GameObject next = objectivesParent.GetChild(i + 1).gameObject;
                            agentSensorsManager.invisibleTargets.Remove(next);
                            Debug.Log($"[RLAgent] Prossimo objective visibile: {next.name}");
                        }
                        break;
                    }
                }
            }
            return; // non proseguire con la logica dei Target normali
        }

        GameObject reachedTarget = other.gameObject;
        if (!fleeing && goalAction.Length > nextTargetCount && other.gameObject == goalAction[nextTargetCount].goalLocation)
        {
            Target target = reachedTarget.GetComponent<Target>();
            entryValue = Vector3.Dot(transform.forward, reachedTarget.transform.forward);

            if (goalAction[nextTargetCount].animations != null && goalAction[nextTargetCount].animations.Count > 0)
            {
                animationManager.SetWalking(false);
                StartCoroutine(animationManager.PlayAnimationsSequence(goalAction[nextTargetCount].animations, this));
            }
            else
            {
                animationManager.SetWalking(true);
                MoveToNextTargetWithDelay(0);
            }

            // final target
            if ((target.group == group || target.group == Group.Generic) && target.targetType == TargetType.Final)
            {
                AddReward(constants.finale_target_reward);
                print("Final target");
                Finished();
            }
        }
        else if (fleeing && other.gameObject.name.Contains("Flee"))
        {
            Target target = reachedTarget.GetComponent<Target>();
            entryValue = Vector3.Dot(transform.forward, reachedTarget.transform.forward);
            if ((target.group == group || target.group == Group.Generic) && target.targetType == TargetType.Final)
            {
                AddReward(constants.finale_target_reward);
                print("Final target");
                Finished();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Se ho NavMeshAgent per ora lo ignoro
        NavMeshAgent agent = other.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            return;
        }

        // If there is no target, exit
        Target target = other.GetComponent<Target>();

        if (target == null)
        {
            return;
        }

        // If there is a target
        GameObject reachedTarget = other.gameObject;
        exitValue = Vector3.Dot(transform.forward, reachedTarget.transform.forward);
        float resultValue = entryValue * exitValue;

        if ((target.group == Group.Generic || target.group == group) && target.targetType == TargetType.Intermediate)
        {
            if (!targetsTaken.Contains(reachedTarget))
            {
                if (resultValue >= 0)
                {
                    targetsTaken.Add(reachedTarget);
                    AddReward(constants.new_target_reward);
                    print("Target intermedio: " + reachedTarget.name);
                }
                else
                {
                    AddReward(constants.target_taken_incorrectly_reward);
                    print("Target intermedio preso in modo scorretto: " + reachedTarget.name);
                }
            }
            else
            {
                AddReward(constants.already_taken_target_reward);
                print("Already_taken_target_reward: " + reachedTarget.name);
            }
        }
    }

    public void flee()
    {
        //animationManager.SetRunning();
        GameObject[] fleeTargets = GameObject.FindGameObjectsWithTag("Target");
        fleeing = true;
        gameObject.GetComponent<AgentSensorsManager1>().invisibleTargets.Add(goalAction[nextTargetCount - 1].goalLocation);
        foreach (GameObject target in fleeTargets)
        {
            if (target.name.Contains("Flee"))
            {
                gameObject.GetComponent<AgentSensorsManager1>().invisibleTargets.Remove(target);
            }
        }
    }

    public void SpeedChange(float deltaSpeed)
    {
        currentSpeed += (minMaxSpeed.y * deltaSpeed / 2f);
        currentSpeed = Mathf.Clamp(currentSpeed, minMaxSpeed.x, minMaxSpeed.y);
        Vector3 velocityChange = (transform.forward * currentSpeed * 5) - rigidBody.velocity;
        rigidBody.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    public void AngleChange(float deltaAngle)
    {
        newAngle = Mathf.Round((deltaAngle * constants.angleRange) + transform.rotation.eulerAngles.y);
        newAngle %= 360;
        if (newAngle < 0) { newAngle += 360f; }
        transform.eulerAngles = new Vector3(0, newAngle, 0);
    }

    public void rewardsWallsAndTargetsObservations(List<(GizmosTag, Vector3)> wallsAndTargets)
    {
        bool target = false;
        bool proxemic_small_wall = false;
        bool seeingObjective = false;

        foreach (var entry in wallsAndTargets)
        {
        }
        for (int i = 0; i < wallsAndTargets.Count; i++)
        {
            (GizmosTag wallsAndTargetTag, Vector3 wallsAndTargetVector) = wallsAndTargets[i];
            float agentAndWallsAndTargetDistance = Vector3.Distance(transform.position + Vector3.up, wallsAndTargetVector);
            if ((agentAndWallsAndTargetDistance < constants.proxemic_small_distance + constants.rayOffset) &&
                (wallsAndTargetTag == GizmosTag.Wall))
            {
                StatsWriter.WriteAgentCollision(
                   transform.position.x,
                   transform.position.z,
                   "Wall",
                   "Small",
                   uniqueID
                );
                proxemic_small_wall = true;
            }
            if (wallsAndTargetTag == GizmosTag.NewTarget)
            {
                target = true;
            }
            if (wallsAndTargetTag == GizmosTag.Obiettivo)
            {
                seeingObjective = true;
            }
        }
        // Penalizza solo se non vede né Target né Objective
        if (!target && !seeingObjective)
        {
            AddReward(constants.not_watching_target_reward);
            print("not_watching_target_reward");
        }
        if (proxemic_small_wall)
        {
            AddReward(constants.proxemic_small_wall_reward);
            print("proxemic_small_wall_reward");
        }
    }

    public void rewardsWallsAndAgentsObservations(List<(GizmosTag, Vector3)> wallsAndAgents)
    {
        bool proxemic_large_agent = false;
        bool proxemic_medium_agent = false;
        bool proxemic_small_agent = false;

        for (int i = 0; i < wallsAndAgents.Count; i++)
        {
            (GizmosTag wallsAndAgentsTag, Vector3 wallsAndAgentsVector) = wallsAndAgents[i];
            float agentAndWallsAndAgentsDistance = Vector3.Distance(transform.position + Vector3.up, wallsAndAgentsVector);

            if ((constants.proxemic_large_distance + constants.rayOffset >= agentAndWallsAndAgentsDistance)
                && (constants.proxemic_medium_distance + constants.rayOffset < agentAndWallsAndAgentsDistance) &&
                (wallsAndAgentsTag == GizmosTag.Agent) && (i < (constants.proxemic_large_ray * 2) + 1))
            {
                StatsWriter.WriteAgentCollision(
                   transform.position.x,
                   transform.position.z,
                   "Agent",
                   "Large",
                   uniqueID
                );
                proxemic_large_agent = true;
            }
            else if ((constants.proxemic_medium_distance + constants.rayOffset >= agentAndWallsAndAgentsDistance)
               && (constants.proxemic_small_distance + constants.rayOffset < agentAndWallsAndAgentsDistance) &&
               (wallsAndAgentsTag == GizmosTag.Agent) && (i < (constants.proxemic_medium_ray * 2) + 1))
            {
                StatsWriter.WriteAgentCollision(
                   transform.position.x,
                   transform.position.z,
                   "Agent",
                   "Medium",
                   uniqueID
                );
                proxemic_medium_agent = true;
            }
            else if ((constants.proxemic_small_distance + constants.rayOffset >= agentAndWallsAndAgentsDistance) &&
                (wallsAndAgentsTag == GizmosTag.Agent))
            {
                StatsWriter.WriteAgentCollision(
                   transform.position.x,
                   transform.position.z,
                   "Agent",
                   "Small",
                   uniqueID
                );
                proxemic_small_agent = true;
            }
        }
        if (proxemic_small_agent)
        {
            AddReward(constants.proxemic_small_agent_reward);
            print("proxemic_small_agent_reward");
        }
        else if (proxemic_medium_agent)
        {
            AddReward(constants.proxemic_medium_agent_reward);
            print("proxemic_medium_agent_reward");
        }
        else if (proxemic_large_agent)
        {
            AddReward(constants.proxemic_large_agent_reward);
            print("proxemic_large_agent_reward");
        }
    }

    public void SetWalking(bool value)
    {
        walking = value;
    }

    private float GetCurrentSpeed()
    {
        if (isUsingNavMesh)
        {

            NavMeshAgent navAgent = GetComponent<NavMeshAgent>();
            if (navAgent != null && navAgent.enabled)
            {
                // For navmesh speed
                return navAgent.velocity.magnitude * 4f; // 4f seems ok for the current Agent speed
            }
        }

        // Otherwise use Rigidbody speed
        return rigidBody.velocity.magnitude;
    }


    /// <summary>
    /// Enables NavMesh navigation mode
    /// </summary>
    public void EnableNavMeshMode()
    {
        isUsingNavMesh = true;

        // Use Kinematic for NavMesh navigation
        if (rigidBody != null && !rigidBody.isKinematic)
        {
            rigidBody.isKinematic = true;
        }

        // Disable NavMeshObstacle when using NavMesh navigation
        if (navMeshObstacle != null && navMeshObstacle.enabled)
        {
            navMeshObstacle.enabled = false;
        }

        Debug.Log($"[RLAgentPlanning] NavMesh mode enabled for {gameObject.name}");
    }

    /// <summary>
    /// Disables NavMesh navigation mode and returns to RL control
    /// </summary>
    public void DisableNavMeshMode()
    {
        isUsingNavMesh = false;

        // Re-enable Rigidbody physics for RL control
        if (rigidBody != null && rigidBody.isKinematic)
        {
            rigidBody.isKinematic = false;
        }

        // Enable NavMeshObstacle with delay
        StartCoroutine(EnableNavMeshObstacleWithDelay(0.5f));
        Debug.Log($"[RLAgentPlanning] NavMesh mode disabled for {gameObject.name} - back to RL control");
    }

    /// <summary>
    /// Enables NavMeshObstacle after a delay
    /// </summary>
    public System.Collections.IEnumerator EnableNavMeshObstacleWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (navMeshObstacle != null && !navMeshObstacle.enabled)
        {
            navMeshObstacle.enabled = true;
            Debug.Log($"[RLAgentPlanning] NavMeshObstacle enabled for {gameObject.name}");
        }
    }
    
    // Add here other methods for handling navigation (like from RLAgentPlanning)
}