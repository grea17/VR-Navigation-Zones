using System;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[RequireComponent(typeof(ObjectiveInteractionHandler))
]
[RequireComponent(typeof(ObjectiveObserver))
]
[RequireComponent(typeof(RLPlanningAnimationManager))
]
public class RLAgentPlanning : Agent, IAgentRLPlanning, IAgentOrder // order logic can be removed
{
    // ===== ZONE TRACKING =====
    [Header("Zone Tracking")]
    [SerializeField] public int campione = 1;
    [SerializeField] public string posObiettivo = "Dentro WZ";
    private Dictionary<Artifact, float> _wzEntryTimes = new Dictionary<Artifact, float>();
    private Dictionary<Artifact, float> _tzEntryTimes = new Dictionary<Artifact, float>();
    private Dictionary<Artifact, float> _distancesWZ = new Dictionary<Artifact, float>();
    private Dictionary<Artifact, float> _distancesTZ = new Dictionary<Artifact, float>();
    private Dictionary<Artifact, bool> _navMeshActivations = new Dictionary<Artifact, bool>();
    private Dictionary<Artifact, float> _wzExitTimes = new Dictionary<Artifact, float>();
    private Dictionary<Artifact, float> _tzExitTimes = new Dictionary<Artifact, float>();
    //private Dictionary<Artifact, float> _interactionStartTimes = new Dictionary<Artifact, float>();
    //private Dictionary<Artifact, float> _interactionTimes = new Dictionary<Artifact, float>();
    private Vector3 _lastTrackedPosition;
    private bool _episodeStarted = false;
    private float _episodeStartTime;
    private float _maxEpisodeDuration = 120f; // 2 minuti
    private bool _timedOut = false;

    // Artifacts Selection
    [SerializeField] private List<Artifact> _assignedArtifacts = new List<Artifact>();
    //private HashSet<Artifact> _loggedArtifacts = new HashSet<Artifact>();
    public List<Artifact> assignedArtifacts => _assignedArtifacts;
    public int configNumber = 0;

    // ===== ARTIFACT ZONE AWARENESS =====
    public enum ArtifactProximity
    {
        Far,
        Warning,
        Transition
    }
    
    private Coroutine _waitForArtifactCoroutine;
    //private bool isTrackingNavMesh = false;
    
    public ArtifactProximity currentProximity = ArtifactProximity.Far;
    private Artifact approachingArtifact;
    private Transform approachingTarget;

    private Artifact lastUsedArtifact;
    private float artifactCooldownTimer = 0f;    
    [Range(0f, 1f)] public float warningSpeedMultiplier = 0.85f;
    [Range(0f, 1f)] public float transitionSpeedMultiplier = 0.6f;
    public bool enableAnticipatorySlowdown = true;

    [Header("Steering Settings")]   
    [Tooltip("Enable soft steering towards artifact in warning zone")]
    public bool enableSoftSteering = true;
    
    [Tooltip("Velocità angolare in gradi/secondo")]
    [Range(0f, 360f)] public float angularSpeed = 120f;

    [Tooltip("Seconds before agent can reuse same artifact")]
    public float artifactCooldown = 5f; 
    private Coroutine steeringCoroutine;
    bool debugging = false;

    // Artifact Manager - Handles artifact interactions 
    private ArtifactAgentManager artifactManager;

    // Order Manager - Handles order logic (OPTIONAL)
    private OrderAgentManager orderManager;

    // Order tracking properties (OrderAgentManager)
    public int? MyOrderId => orderManager?.MyOrderId;
    public bool IsMyOrderReady => orderManager?.IsMyOrderReady ?? false;
    public bool orderAgent = true;

    // Artifact Navigation Properties
    private List<ArtifactTrigger> subscribedTriggers = new List<ArtifactTrigger>();
    private ArtifactNavigationData? currentNavigationData;

    private Action OnNavigationComplete;


    /// <summary>
    /// True if the agent is using NavMesh for movement.
    /// </summary>
    [System.NonSerialized]
    private bool isUsingNavMesh = false;


    /// <summary>
    /// Crossings count for the agent.
    /// </summary>
    public int numberOfCrossings = 0;

    /// <summary>
    /// True if the agent is currently walking (moving).
    /// </summary>
    private bool walking = true;

    /// <summary>
    /// Public accessor for the agent's Rigidbody component.
    /// </summary>
    public Rigidbody RigidBody => rigidBody;

    private HashSet<int> insideTargets = new HashSet<int>();

    /// <summary>
    /// Reference to constants.
    /// </summary>
    public IAgentConstants constants { get; private set; }

    /// <summary>
    /// The group to which the agent belongs.
    /// </summary>
    public Group group;

    /// <summary>
    /// Minimum and maximum speed for the agent.
    /// </summary>
    private Vector2 minMaxSpeed;

    /// <summary>
    /// Current speed of the agent.
    /// </summary>
    private float currentSpeed;
    /// <summary>
    /// Current angle of the agent.
    /// </summary>
    private float newAngle;

    /// <summary>
    /// True if the agent is fleeing.
    /// </summary>
    private bool fleeing = false;

    /// <summary>
    /// Initial position of the agent.
    /// </summary>
    [NonSerialized] public Vector3 startPosition;
    /// <summary>
    /// Initial rotation of the agent.
    /// </summary>
    [NonSerialized] public Quaternion startRotation;

    /// <summary>
    /// Rigidbody component reference.
    /// </summary>
    private Rigidbody rigidBody;

    /// <summary>
    /// Animator component reference.
    /// </summary>
    private Animator animator;
    private RLPlanningAnimationManager animationManager;

    /// <summary>
    /// Range for randomizing speed.
    /// </summary>
    private Vector2 speedMaxRange;

    /// <summary>
    /// List of targets taken by the agent.
    /// </summary>
    private List<GameObject> _targetsTaken = new List<GameObject>();

    /// <summary>
    /// Public property for targets taken by the agent.
    /// </summary>
    public List<GameObject> targetsTaken => _targetsTaken;

    /// <summary>
    /// Reference to the agent's sensors manager.
    /// </summary>
    private AgentPlanningSensorsManager agentSensorsManager;
    /// <summary>
    /// Reference to the agent's gizmos drawer.
    /// </summary>
    private AgentPlanningGizmosDrawer agentGizmosDrawer;
    /// <summary>
    /// Reference to the agent's observer.
    /// </summary>
    private AgentPlanningObserver agentObserver;
    /// <summary>
    /// Reference to the objective observer.
    /// </summary>
    private ObjectiveObserver objectiveObserver;

    /// <summary>
    /// Observations for walls and targets.
    /// </summary>
    private List<float> wallsAndTargetsObservations;
    /// <summary>
    /// Observations for walls and agents.
    /// </summary>
    private List<float> wallsAndAgentsObservations;
    /// <summary>
    /// Observations for walls and objectives.
    /// </summary>
    private List<float> wallsAndObjectivesObservations;

    /// <summary>
    /// List of walls and agents detected.
    /// </summary>
    private List<(GizmosTagPlanning, Vector3)> wallsAndAgents = new List<(GizmosTagPlanning, Vector3)>();
    /// <summary>
    /// List of walls and targets detected.
    /// </summary>
    private List<(GizmosTagPlanning, Vector3)> wallsAndTargets = new List<(GizmosTagPlanning, Vector3)>();
    /// <summary>
    /// List of walls and objectives detected.
    /// </summary>
    private List<(GizmosTagPlanning, Vector3)> wallsAndObjectives = new List<(GizmosTagPlanning, Vector3)>();

    /// <summary>
    /// Event triggered when the agent is terminated.
    /// </summary>
    public event Action<float, EnvironmentPlanning> agentTerminated;
    /// <summary>
    /// Event triggered to reset the agent.
    /// </summary>
    public event Action<RLAgentPlanning> resetAgent;

    /// <summary>
    /// Unique identifier for the agent.
    /// </summary>
    private string uniqueID;
    /// <summary>
    /// Number of steps in the environment.
    /// </summary>
    /// TODO: CHECK IF NECESSARY HERE
    //[NonSerialized] public int envStep = -1;
    public int envStep;
    /// <summary>
    /// Steps left in the episode.
    /// </summary>
    private int stepLeft;

    /// <summary>
    /// Number of iterations performed by the agent.
    /// </summary>
    private int numberOfIteraction;
    /// <summary>
    /// Cumulative reward collected by the agent.
    /// </summary>
    private float cumulativeReward;
    /// <summary>
    /// If true, the agent's position is locked.
    /// </summary>
    public bool lockPosition;
    /// <summary>
    /// Initial time of the episode.
    /// </summary>
    private int tempoIniziale;
    /// <summary>
    /// Reference to the NavMeshObstacle component.
    /// </summary>
    private NavMeshObstacle navMeshObstacle;

    /// <summary>
    /// Reference to the environment.
    /// </summary>
    [NonSerialized] public EnvironmentPlanning env;

    /// <summary>
    /// Value used for entry direction calculation.
    /// </summary>
    float entryValue;
    /// <summary>
    /// Value used for exit direction calculation.
    /// </summary>
    float exitValue;
    /// <summary>
    /// EnvironmentPlanning identifier.
    /// </summary>
    [NonSerialized] public string envID;

    private float lastYRotation;

    /// <summary>
    /// True if the task is completed.
    /// </summary>
    [NonSerialized] public bool taskCompleted = false;
    /// <summary>
    /// True if the environment is ready.
    /// </summary>
    //private bool isEnvReady = false;

    /// <summary>
    /// Reference to the objective interaction handler.
    /// </summary>
    private ObjectiveInteractionHandler objectiveHandler;

    // Variabili reflection per screenshot curriculum
#if UNITY_EDITOR
    private Curriculum curriculum;
    private Testing testing;
    private System.Reflection.FieldInfo videoRecorderField;
    private System.Reflection.FieldInfo waitingExtraEpisodesField;
#endif

    /**
     * \class ProxemicRange
     * \brief Helper class for proxemic reward ranges.
     */
    private class ProxemicRange
    {
        public float Start { get; set; }
        public float End { get; set; }
        public float Reward { get; set; }
        public string StatsTag { get; set; }
        public float RaysNumber { get; set; }
    }

    /// <summary>
    /// List of proxemic ranges for agent proximity rewards.
    /// </summary>
    private List<ProxemicRange> ProxemicRanges;

    /// <summary>
    /// Array of front crossings for the agent.
    /// </summary>
    public float[] crossings;

    /// <summary>
    /// Maximum number of intermediate targets that can be handled by the agent.
    /// </summary>
    public const int MaxIntermediateTargets = 15;

    /**
     * \brief Called when the script instance is being loaded.
     * Initializes the agent's components and sets its initial position and rotation.
     */
    private void Awake()
    {
        // Init all components
        animator = GetComponent<Animator>();
        animationManager = GetComponent<RLPlanningAnimationManager>();
        agentSensorsManager = GetComponent<AgentPlanningSensorsManager>();
        agentGizmosDrawer = GetComponent<AgentPlanningGizmosDrawer>();
        agentObserver = GetComponent<AgentPlanningObserver>();
        objectiveObserver = GetComponent<ObjectiveObserver>();
        objectiveHandler = GetComponent<ObjectiveInteractionHandler>();
        rigidBody = GetComponent<Rigidbody>();
        navMeshObstacle = GetComponent<NavMeshObstacle>();
        // Artifacts
        artifactManager = GetComponent<ArtifactAgentManager>();
        if (orderAgent)
            orderManager = GetComponent<OrderAgentManager>();

        // Constants
        constants = new ConstantsPlanning();
        agentSensorsManager.constants = this.constants;
        agentObserver.constants = this.constants;
        agentGizmosDrawer.constants = this.constants;
        minMaxSpeed = constants.minMaxSpeed;
        speedMaxRange = constants.speedMaxRange;

        ProxemicRanges = new List<ProxemicRange>
        {
            new ProxemicRange { Start = constants.proxemic_medium_distance, End = constants.proxemic_large_distance, Reward = constants.proxemic_large_agent_reward, StatsTag = "Large", RaysNumber = constants.proxemic_large_ray },
            new ProxemicRange { Start = constants.proxemic_small_distance, End = constants.proxemic_medium_distance, Reward = constants.proxemic_medium_agent_reward, StatsTag = "Medium", RaysNumber = constants.proxemic_medium_ray },
            new ProxemicRange { Start = 0, End = constants.proxemic_small_distance, Reward = constants.proxemic_small_agent_reward, StatsTag = "Small", RaysNumber = constants.proxemic_small_ray }
        };

        // Check if all required components are present
        if (artifactManager == null)
            Debug.LogWarning($"ArtifactAgentManager component missing on {gameObject.name}");
        // Check if NavMeshObstacle component is present
        if (navMeshObstacle == null)
            Debug.LogWarning($"NavMeshObstacle component missing on {gameObject.name}");
        if (orderManager == null && orderAgent)
            Debug.LogWarning($"OrderAgentManager component missing on {gameObject.name}");
        if (agentSensorsManager == null)
            Debug.LogError($"AgentSensorsManager component missing on {gameObject.name}");
        if (agentObserver == null)
            Debug.LogError($"AgentObserver component missing on {gameObject.name}");
        if (objectiveObserver == null)
            Debug.LogError($"ObjectiveObserver component missing on {gameObject.name}");
        if (objectiveHandler == null)
            Debug.LogError($"ObjectiveInteractionHandler component missing on {gameObject.name}");
        if (agentGizmosDrawer == null)
            Debug.LogError($"AgentGizmosDrawer component missing on {gameObject.name}");
        if (agentSensorsManager != null)
        {
            try
            {
                agentSensorsManager.UpdateTargetSensorVision(group);
                agentSensorsManager.UpdateObjectiveSensorVision(group);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error updating sensor vision for {gameObject.name}: {e.Message}");
            }
        }

        startPosition = transform.position;
        startRotation = transform.rotation;

        // Default walking state
        if (animationManager != null)
        {
            animationManager.SetWalking(true);
        }

#if UNITY_EDITOR
        curriculum = FindObjectOfType<Curriculum>();
        if (curriculum != null)
        {
            videoRecorderField = curriculum.GetType().GetField("videoRecorder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            waitingExtraEpisodesField = curriculum.GetType().GetField("waitingExtraEpisodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }
        testing = FindObjectOfType<Testing>();
#endif

        switch (group)
        {
            case Group.First:
                GetComponentInChildren<SkinnedMeshRenderer>().material.color = Color.red;
                break;
            case Group.Second:
                GetComponentInChildren<SkinnedMeshRenderer>().material.color = Color.cyan;
                break;
            case Group.Third:
                GetComponentInChildren<SkinnedMeshRenderer>().material.color = Color.green;
                break;
            case Group.Fourth:
                GetComponentInChildren<SkinnedMeshRenderer>().material.color = Color.yellow;
                break;
            default:
                break;
        }
    }

    void Start()
    {
        ArtifactTrigger.OnAgentTriggerEntered += OnArtifactTriggerEntered;
        ArtifactTrigger.OnAgentTriggerExited += OnArtifactTriggerExited;

        WarningZoneTrigger.OnAgentEnterWarning += HandleWarningZoneEnter;
        WarningZoneTrigger.OnAgentExitWarning += HandleWarningZoneExit; 
    }

    private void Update()
    {
        // forzare la fine dell'episodio se supera la durata massima, utilizzato per raccolta dati
        if (_episodeStarted && Time.time - _episodeStartTime > _maxEpisodeDuration)
        {
            _timedOut = true;
            Finished();
        return;
        }
        // If the objective handler is executing animations, skip the update
        if (objectiveHandler != null && objectiveHandler.IsExecutingObjectiveAnimations())
        {
            return; // No basic movement or animation updates while executing animations
        }
        
        if (approachingArtifact != null)
        {
            if (currentProximity == ArtifactProximity.Warning)
            {
                if (!_distancesWZ.ContainsKey(approachingArtifact)) _distancesWZ[approachingArtifact] = 0f;
                _distancesWZ[approachingArtifact] += Vector3.Distance(transform.position, _lastTrackedPosition);
            }
            else if (currentProximity == ArtifactProximity.Transition)
            {
                if (!_distancesTZ.ContainsKey(approachingArtifact)) _distancesTZ[approachingArtifact] = 0f;
                _distancesTZ[approachingArtifact] += Vector3.Distance(transform.position, _lastTrackedPosition);
            }
        }
        _lastTrackedPosition = transform.position;
        UpdateAnimations();
    }

    /// <summary>
    /// Updates agent animations based on current movement
    /// </summary>
    private void UpdateAnimations()
    {
        // Get Speed for Animations
        float speed = GetCurrentSpeed();

        animationManager.UpdateSpeed(speed / 10);

        // Idle/Walking
        if (speed < 0.25f)
        {
            //Debug.Log("Animation State: Idle");
            animationManager.SetWalking(false);

            float currentYRotation = transform.eulerAngles.y;
            float deltaY = Mathf.DeltaAngle(lastYRotation, currentYRotation);
            float angularSpeed = Mathf.Abs(deltaY) / Time.deltaTime;

            // Turn - only if not already turning and significant rotation
            if (Mathf.Abs(deltaY) > 10f && !IsTurnAnimationPlaying())
            {
                Debug.Log("Animation State: Turn");
                float normalizedTurnSpeed = Mathf.Clamp(angularSpeed / 90f, 0.5f, 1.0f);
                if (deltaY > 0)
                    animationManager.PlayTurn(true, normalizedTurnSpeed); // TurnRight
                else
                    animationManager.PlayTurn(false, normalizedTurnSpeed); // TurnLeft
            }
            else if (Mathf.Abs(deltaY) < 2f && IsTurnAnimationPlaying())
            {
                // Stop turn if rotation is minimal
                animationManager.StopTurn();
                animationManager.SetIdle(true);
            }
        }
        else
        {
            //Debug.Log("Animation State: Walking");
            // Stop any turn animations when walking
            if (IsTurnAnimationPlaying())
            {
                animationManager.StopTurn();
            }
            animationManager.SetWalking(true);
        }

        lastYRotation = transform.eulerAngles.y;
    }

    /// <summary>
    /// Gets the current speed from either Rigidbody or NavMeshAgent
    /// </summary>
    /// <returns>Current movement speed</returns>
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
    /// Checks if a turn animation is currently playing
    /// </summary>
    /// <returns>True if turn animation is active</returns>
    private bool IsTurnAnimationPlaying()
    {
        if (animator == null) return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName("TurnLeft") || stateInfo.IsName("TurnRight");
    }

    /// <summary>
    /// Gets the color associated with this agent's group.
    /// </summary>
    /// <returns>The agent's group color</returns>
    public Color GetAgentColor()
    {
        switch (group)
        {
            case Group.First:
                return Color.red;
            case Group.Second:
                return Color.cyan;
            case Group.Third:
                return Color.green;
            case Group.Fourth:
                return Color.yellow;
            default:
                return Color.white; // Colore di default per gruppi non riconosciuti
        }
    }

    /**
     * \brief Called at the beginning of each episode.
     * Resets the agent's state.
     */
    public override void OnEpisodeBegin()
    {
        _wzEntryTimes.Clear();
        _tzEntryTimes.Clear();
        _distancesWZ.Clear();
        _distancesTZ.Clear();
        _navMeshActivations.Clear();
        _episodeStartTime = 0f;
        _lastTrackedPosition = transform.position;
        _tzExitTimes.Clear();
        _wzExitTimes.Clear();
        _timedOut = false;
        _episodeStarted = false;
        _episodeStartTime = Time.time;
        _episodeStarted = true;

        foreach (var artifact in _assignedArtifacts)
        {
            artifact.ResetAccess();
            artifact.GetComponentInChildren<WarningZoneTrigger>()?.RemoveAgent(gameObject);
        }
        stepLeft = envStep;
        uniqueID = Guid.NewGuid().ToString();
        rigidBody.velocity = Vector3.zero;
        tempoIniziale = (int)Time.time;
        currentSpeed = 0;
        numberOfIteraction = 0;
        InitCrossings(numberOfCrossings);
        // minMaxSpeed.y = RandomGaussian(speedMaxRange.x, speedMaxRange.y); // This line randomizes the maximum speed

        // Reset order state
        if (orderAgent)
            orderManager?.ResetOrderState();

        //artifactsUsedThisEpisode.Clear();
        lastUsedArtifact = null;
        artifactCooldownTimer = 0f;
        currentProximity = ArtifactProximity.Far;
        approachingArtifact = null;
        approachingTarget = null;
        if (_waitForArtifactCoroutine != null)
        {
            StopCoroutine(_waitForArtifactCoroutine);
            _waitForArtifactCoroutine = null;
        }
        foreach (var a in _assignedArtifacts)
            a.ResetAccess(); // solo se sei il primo agente, altrimenti sposta nell'env controller

        if (steeringCoroutine != null)
        {
            StopCoroutine(steeringCoroutine);
            steeringCoroutine = null;
        }
        //_loggedArtifacts.Clear();
        
        //isTrackingNavMesh = false;

        resetAgent?.Invoke(this);
    }

    /**
     * \brief Makes the agent listen for environment readiness.
     * \param action Reference to the action to subscribe to.
     */
    public void MakeListenEnvReady(ref Action<RLAgentPlanning> action)
    {
        action += (agent) =>
        {
            if (agent == this)
            {
                if (env != null)
                {
                    numberOfCrossings = env.GetNumIntermediateTargets();
                    InitCrossings(numberOfCrossings);
                    Debug.Log($"Agent {gameObject.name}: initialized {numberOfCrossings} crossings");
                }
                else
                {
                    Debug.LogError($"Agent {gameObject.name}: EnvironmentPlanning is null");
                }

                if (objectiveHandler != null)
                {
                    objectiveHandler.InitializeObjectivesFromEnvironment();
                    Debug.Log($"Agent {gameObject.name} initialized objectives via ObjectiveInteractionHandler");
                }
                else
                {
                    Debug.LogWarning($"Agent {gameObject.name} does not have an ObjectiveInteractionHandler component");
                }
            }
        };

        print("Agent " + gameObject.name + " is listening to the environment");
    }

    /**
     * \brief Collects observations from the environment to feed to the neural network.
     * \param vectorSensor The vector sensor for observations.
     */
    public override void CollectObservations(VectorSensor vectorSensor)
    {
        Dictionary<AgentPlanningSensorsManager.Sensore, RaycastHit[]> sensorsResults = agentSensorsManager.ComputeSensorResults();

        wallsAndAgents = agentObserver.WallsAndAgentsGizmos;
        wallsAndTargets = agentObserver.WallsAndTargetsGizmos;
        wallsAndObjectives = agentObserver.WallsAndObjectivesGizmos;

        (wallsAndTargetsObservations, wallsAndAgentsObservations, wallsAndObjectivesObservations) = agentObserver.ComputeObservations(sensorsResults);

        float normalizedSpeed = (currentSpeed - minMaxSpeed.x) / (minMaxSpeed.y - minMaxSpeed.x);
        agentGizmosDrawer.SetObservationsResults(wallsAndTargets, wallsAndAgents, wallsAndObjectives);

        vectorSensor.AddObservation(wallsAndTargetsObservations);
        vectorSensor.AddObservation(wallsAndAgentsObservations);
        vectorSensor.AddObservation(wallsAndObjectivesObservations);
        vectorSensor.AddObservation(normalizedSpeed);
        vectorSensor.AddObservation(objectiveObserver.GetObjectivesObservation());
        //vectorSensor.AddObservation(GetCrossings());
        //vectorSensor.AddObservation(taskCompleted ? 1.0f : 0.0f); //TODO: check se ha senso

        rewardsWallsAndTargetsObservations(wallsAndTargets);
        rewardsWallsAndAgentsObservations(wallsAndAgents);
    }

    /**
     * \brief Applies the actions received from the neural network.
     * \param vectorAction Array of actions.
     */
    [Obsolete]
    public override void OnActionReceived(float[] vectorAction)
    {
        if (walking && !isUsingNavMesh)
        {
            //float realSpeed = rigidBody.velocity.magnitude;
            float actionSpeed;
            float actionAngle;

            if (constants.discrete)
            {
                actionSpeed = vectorAction[0];
                actionSpeed = (actionSpeed - 5f) / 5f;
                actionAngle = vectorAction[1];
                actionAngle = (actionAngle - 5f) / 5f;
            }
            else
            {
                actionSpeed = Mathf.Clamp(vectorAction[0], -1f, 1f);
                actionAngle = Mathf.Clamp(vectorAction[1], -1f, 1f);
            }

            if (enableAnticipatorySlowdown)
            {
                //
                actionSpeed = ApplyProximitySpeedModifier(actionSpeed);
            }

            // Non applicare movimenti se le animazioni degli obiettivi sono in corso
            if (!lockPosition && !(objectiveHandler != null && objectiveHandler.IsExecutingObjectiveAnimations()))
            {
                AngleChange(actionAngle);
                SpeedChange(actionSpeed);
            }
        }

        if (walking)
        {
            int agentID = gameObject.GetInstanceID();
            numberOfIteraction++;
            StatsWriter.WriteAgentStats(
                transform.position.x,
                transform.position.z,
                group,
                currentSpeed,
                GetCurrentSpeed(),
                transform.rotation.eulerAngles.y,
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

    /**
     * \brief Computes the steps and applies step rewards.
     */
    public void ComputeSteps()
    {
#if UNITY_EDITOR
        // Screenshot logic TRAINING 
        if (curriculum != null && videoRecorderField != null && waitingExtraEpisodesField != null)
        {
            var videoRecorder = videoRecorderField.GetValue(curriculum) as EnvironmentVideoRecorder;
            bool waitingExtraEpisodes = (bool)waitingExtraEpisodesField.GetValue(curriculum);
            if (waitingExtraEpisodes && videoRecorder != null && videoRecorder.IsRecording)
            {
                videoRecorder.TakeScreenshot();
            }
        }
        // Screenshot logic TESTING

        if (testing != null)
        {
            testing.OnAgentStep();
        }
#endif
        AddReward(constants.step_reward);
        stepLeft--;
        if (!taskCompleted)
        {
            AddReward(constants.incomplete_task_step_reward * objectiveHandler.GetRemainingObjectivesPercentage()); /// inomplete_task_step_reward is moltiplied by the percentage of objectives not completed
        }
        if (stepLeft <= 0)
        {
            AddReward(constants.step_finished_reward);
            print("finished_step");
            Finished();
        }
    }

    /**
     * \brief Ends the episode and resets the agent.
     */
    public void Finished()
    {
        LogZoneStatsTotal();
        LogArtifactStatsDetail();
        // If i want to change behaviour on end, edit here. (And in EnvPlanning the HandleAgentTerminated method)
        cumulativeReward = GetCumulativeReward();
        gameObject.SetActive(false);
        StatsWriter.WriteEnvStats(group, (int)(Time.time - tempoIniziale));
        targetsTaken.Clear();
        transform.position = startPosition;
        transform.rotation = startRotation;
        EndEpisode();
        agentTerminated?.Invoke(cumulativeReward, env);
        
        // Segnala a Python che ha finito e ferma il Play!
        //string donePath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "PedPy", "sim_done.txt");
        //System.IO.File.WriteAllText(donePath, "finito");

        // Notifica il manager
        SimulationManager.Instance?.OnAgentFinished();
        
    }

    /**
     * \brief Generates a random value with a Gaussian distribution.
     * \param minValue Minimum value.
     * \param maxValue Maximum value.
     * \return Random Gaussian value.
     */
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

    /**
     * \brief Triggered when the agent enters a collider.
     * \param other The collider entered.
     */
    private void OnTriggerEnter(Collider other)
    {
        GameObject triggerObject = other.gameObject;

        entryValue = Vector3.Dot(transform.forward, triggerObject.transform.forward);
        if (triggerObject.CompareTag("Target"))
        {
            Target target = triggerObject.GetComponent<Target>();

            if (IsFinalTarget(target))
            {
                if (taskCompleted)
                {
                    Debug.Log("Final target and all objectives completed!");
                    AddReward(constants.finale_target_all_objectives_completed_reward);
                    Finished();
                }
                else
                {
                    Debug.Log("Final target reached but not all objectives completed!");
                    AddReward(constants.finale_target_incomplete_objectives_reward);
                    Finished();
                }
            }
            else if (IsIntermediateTarget(target))
            {
                insideTargets.Add(target.id);
            }
        }
        else if (fleeing && other.gameObject.name.Contains("Flee"))
        {
            Target target = triggerObject.GetComponent<Target>();
            entryValue = Vector3.Dot(transform.forward, triggerObject.transform.forward);
            if ((target.group == group || target.group == Group.Generic) && target.targetType == TargetType.Final)
            {
                if (taskCompleted)
                {
                    AddReward(constants.finale_target_all_objectives_completed_reward);
                }
                else
                {
                    AddReward(constants.finale_target_incomplete_objectives_reward);
                }
                print("Final target");
                Finished();
            }
        }
    }

    /**
     * \brief Triggered when the agent exits a collider.
     * \param other The collider exited.
     */
    private void OnTriggerExit(Collider other)
    {
        GameObject triggerObject = other.gameObject;

        if (objectiveHandler != null && objectiveHandler.IsValidObjective(triggerObject))
        {
            objectiveHandler.HandleObjectiveTrigger(triggerObject);
        }
        else if (triggerObject.CompareTag("Target"))
        {
            Target target = triggerObject.GetComponent<Target>();
            if (IsIntermediateTarget(target))
            {
                if (insideTargets.Contains(target.id))
                {
                    HandleIntermediateTarget(triggerObject);
                    insideTargets.Remove(target.id);
                }
            }
        }
    }

    public void flee()
    {
        //animationManager.SetRunning();
        GameObject[] fleeTargets = GameObject.FindGameObjectsWithTag("Target");
        fleeing = true;


        var sensors = gameObject.GetComponent<AgentSensorsManager>();
        // TODO: Fix if it's necessary for experiments
        foreach (GameObject target in fleeTargets)
        {
            if (target.name.Contains("Flee"))
            {
                //sensors.invisibleTargets.Remove(target);
            }
            else
            {
                //if (!sensors.invisibleTargets.Contains(target))
                //sensors.invisibleTargets.Add(target);
            }
        }
    }

    /**
     * \brief Returns whether the task is completed.
     * \return True if task is completed.
     */
    public bool IsTaskCompleted()
    {
        return taskCompleted;
    }

    public List<GameObject> GetObjective()
    {
        return objectiveHandler.objectives;
    }

    /**
     * \brief Checks if a direction is valid based on the array returned by DetermineVisualizationDirection.
     * \param Array of directions to check
     * \return True if the direction is valid, false otherwise.
    */
    public bool CheckForValidDirection(float[] direction)
    {
        if (direction == null || direction.Length == 0)
        {
            Debug.LogWarning("Direction array is null or empty in CheckForValidDirection");
            return false;
        }

        float[] objectives = objectiveObserver.GetObjectivesObservation();
        int lastIndex = direction.Length - 1;

        if (!taskCompleted)
        {
            // Check if the direction matches any of the objectives
            for (int i = 0; i < direction.Length - 1; i++)
            {
                if (direction[i] == 1 && direction[i] == objectives[i])
                {
                    //Debug.Log($"Valid direction found at index {i}");
                    return true;
                }
            }
        }
        else
        {
            // Task is completed, check only the last index
            if (direction[lastIndex] == 1 && direction[lastIndex] == objectives[lastIndex])
            {
                //Debug.Log("Valid direction for final target");
                return true;
            }
        }

        //Debug.Log("No valid direction found");
        return false;
    }

    /**
     * \brief Determines the visualization direction based on the target object.
     * 
     * This method calculates the direction from the agent to the target and checks if it aligns with the target's forward direction.
     * It returns an array of float values representing the compatibility of the direction with the original system.
     * 
     * If the DirectionsObjectives component is not found on the target object, an error is logged and an empty array is returned.
     * 
     * @param targetObject The target GameObject to evaluate.
     * @return An array of float values representing the direction compatibility.
     */
    public float[] DetermineVisualizationDirection(GameObject targetObject)
    {
        Vector3 agentToTarget = targetObject.transform.position - transform.position;
        agentToTarget.y = 0;

        Vector3 targetForward = targetObject.transform.forward;
        targetForward.y = 0;

        float alignment = Vector3.Dot(targetForward, agentToTarget.normalized);

        int passDirection = (alignment > 0) ? 0 : 1;

        DirectionsObjectives directions = targetObject.GetComponent<DirectionsObjectives>();
        if (directions == null)
        {
            Debug.LogError("DirectionsObjectives component not found on " + targetObject.name);
            return new float[0];
        }

        return directions.getDirections(passDirection);
    }

    /**
     * \brief Determines the passing direction based on the trigger object.
     * 
     * This method calculates the passing direction based on the entry value and retrieves the direction objectives from the DirectionsObjectives component.
     * If the DirectionsObjectives component is not found, an error is logged and an empty array is returned.
     * 
     * @param triggerObject The GameObject that triggered the event.
     * @return An array of float values representing the passing direction.
     */
    public float[] DeterminePassingDirection(GameObject triggerObject)
    {
        int passDirection = (entryValue > 0) ? 0 : 1;
        DirectionsObjectives directions = triggerObject.GetComponent<DirectionsObjectives>();
        if (directions == null)
        {
            Debug.LogError("DirectionsObjectives component not found on " + triggerObject.name);
            return new float[0];
        }

        float[] directionObjectives = directions.getDirections(passDirection);
        return directionObjectives;
    }
    /**
     * \brief Provides manual control for the agent (for debugging).
     * \param actionsOut Output actions array.
     */
    [Obsolete]
    public override void Heuristic(float[] actionsOut)
    {
        // move agent by keyboard
        var continuousActionsOut = actionsOut;
        continuousActionsOut[0] = Input.GetAxis("Vertical");
        continuousActionsOut[1] = Input.GetAxis("Horizontal");
    }
    /**
     * \brief Changes the agent's speed.
     * \param deltaSpeed The change in speed.
     */
    public void SpeedChange(float deltaSpeed)
    {
        currentSpeed += minMaxSpeed.y * deltaSpeed / 2f;
        currentSpeed = Mathf.Clamp(currentSpeed, minMaxSpeed.x, minMaxSpeed.y);
        Vector3 velocityChange = (transform.forward * currentSpeed * 5) - rigidBody.velocity;
        // Vector3 velocityChange = (transform.forward * currentSpeed * GetProximitySpeedFactor() * 5) - rigidBody.velocity;
        rigidBody.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    /**
     * \brief Changes the agent's angle.
     * \param deltaAngle The change in angle.
     */
    public void AngleChange(float deltaAngle)
    {
        newAngle = Mathf.Round((deltaAngle * constants.angleRange) + transform.rotation.eulerAngles.y);
        newAngle %= 360;
        if (newAngle < 0) { newAngle += 360f; }
        transform.eulerAngles = new Vector3(0, newAngle, 0);
    }

    /**
     * \brief Checks if the target is a final target for this agent.
     * \param target The target to check.
     * \return True if it is a final target.
     */
    private bool IsFinalTarget(Target target)
    {
        return target.targetType == TargetType.Final && (target.group == group || target.group == Group.Generic);
    }

    /**
     * \brief Checks if the target is an intermediate target for this agent.
     * \param target The target to check.
     * \return True if it is an intermediate target.
     */
    private bool IsIntermediateTarget(Target target)
    {
        return target.targetType == TargetType.Intermediate &&
            (target.group == group || target.group == Group.Generic);
    }

    /**
     * \brief Applies rewards based on proximity to walls and targets.
     * \param wallsAndTargets List of detected walls and targets.
     */
    public void rewardsWallsAndTargetsObservations(List<(GizmosTagPlanning, Vector3)> wallsAndTargets)
    {
        bool proxemic_small_wall = false;

        for (int i = 0; i < wallsAndTargets.Count; i++)
        {
            (GizmosTagPlanning wallsAndTargetTag, Vector3 wallsAndTargetVector) = wallsAndTargets[i];
            float agentAndWallsAndTargetDistance = Vector3.Distance(transform.position + Vector3.up, wallsAndTargetVector);
            if ((agentAndWallsAndTargetDistance < constants.proxemic_small_distance + constants.rayOffset) &&
                (wallsAndTargetTag == GizmosTagPlanning.Wall))
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
        }

        if (proxemic_small_wall)
        {
            AddReward(constants.proxemic_small_wall_reward);
            print("proxemic_small_wall_reward");
        }
    }

    /**
     * \brief Checks if the ray index is within the limit.
     * \param rayIndex Index of the ray.
     * \param proxemicRaysNumber Number of proxemic rays.
     * \return True if within the limit.
     */
    private bool IsRayWithinTheLimit(int rayIndex, float proxemicRaysNumber)
    {
        return rayIndex < (proxemicRaysNumber * 2) + 1;
    }

    /**
     * \brief Checks if the distance is within the proxemic range and tag matches.
     * \param distance Distance to check.
     * \param start Start of the range.
     * \param end End of the range.
     * \param RaysNumber Number of rays.
     * \param tag Tag to check.
     * \param expectedTag Expected tag.
     * \return True if within range and tag matches.
     */
    private bool IsWithinProxemicRange(float distance, float start, float end, float RaysNumber, GizmosTagPlanning tag, GizmosTagPlanning expectedTag)
    {
        return (distance >= start + constants.rayOffset &&
            distance < end + constants.rayOffset &&
            tag == expectedTag);
    }

    /**
     * \brief Checks proxemic ranges and applies rewards.
     * \param distance Distance to the object.
     * \param tag Tag of the object.
     * \param uniqueID Unique agent ID.
     * \param id Ray index.
     */
    private void CheckProxemicRanges(float distance, GizmosTagPlanning tag, string uniqueID, int id)
    {
        foreach (var range in ProxemicRanges)
        {
            if (IsWithinProxemicRange(distance, range.Start, range.End, range.RaysNumber, tag, GizmosTagPlanning.Agent)
                && IsRayWithinTheLimit(id, range.RaysNumber))
            {
                StatsWriter.WriteAgentCollision(
                    transform.position.x,
                    transform.position.z,
                    "Agent",
                    range.StatsTag,
                    uniqueID
                );
                AddReward(range.Reward);
                print($"{range.StatsTag} reward applied");
                break; // Exit the loop if a match is found
            }
        }
    }

    /**
     * \brief Applies rewards based on proximity to walls and agents.
     * \param wallsAndAgents List of detected walls and agents.
     */
    public void rewardsWallsAndAgentsObservations(List<(GizmosTagPlanning, Vector3)> wallsAndAgents)
    {
        int id = 0;
        foreach (var (tag, position) in wallsAndAgents)
        {
            float distance = Vector3.Distance(transform.position + Vector3.up, position);
            CheckProxemicRanges(distance, tag, uniqueID, id++);
        }
    }

    /**
     * \brief Handles logic when passing through an intermediate target.
     * \param triggerObject The target object.
     */
    private void HandleIntermediateTarget(GameObject triggerObject)
    {
        exitValue = Vector3.Dot(transform.forward, triggerObject.transform.forward);
        float crossingValue = entryValue * exitValue;

        if (crossingValue >= 0)
        {
            targetsTaken.Add(triggerObject);
            Target target = triggerObject.GetComponent<Target>();
            if (crossings[target.id] > 0)
            {
                AddReward(constants.target_alredy_crossed_reward * crossings[target.id]);
                Debug.Log("Target already crossed");
            }
            IncrementCrossing(target);

            float[] directionObjectives = DeterminePassingDirection(triggerObject);

            if (directionObjectives != null && directionObjectives.Length > 0)
            {
                if (CheckForValidDirection(directionObjectives))
                {
                    //Debug.Log($"Correct direction taken (entryValue: {entryValue})");
                    //AddReward(constants.correct_direction_reward);
                }
                else
                {
                    //Debug.Log($"Wrong direction taken (entryValue: {entryValue})");
                    AddReward(constants.wrong_direction_reward);
                }
            }
            if (targetsTaken.Contains(triggerObject))
            {
                AddReward(constants.already_taken_target_reward);
                Debug.Log("Target already taken");
            }
        }
        else
        {
            AddReward(constants.target_taken_incorrectly_reward);
            Debug.Log("Target taken incorrectly");
        }
    }

    // Method to initialize the crossing arrays (call when you know the number of intermediate targets)
    public void InitCrossings(int numIntermediateTargets)
    {
        crossings = new float[MaxIntermediateTargets];
        for (int i = 0; i < MaxIntermediateTargets; i++)
        {
            if (i < numIntermediateTargets)
                crossings[i] = 0;
            else
                crossings[i] = -1f;
        }
        Debug.Log($"Agent {gameObject.name}: inizializzato array crossings con {numIntermediateTargets} target intermedi");
    }

    // Method to increment only if the index is valid (not a sentinel cell)
    public void IncrementCrossing(Target target)
    {
        if (target.targetType == TargetType.Intermediate && target.id >= 0 && target.id < crossings.Length && crossings[target.id] != -1f)
        {
            crossings[target.id]++;
        }
    }

    // Getters for the neural network (arrays always have fixed size)
    public float[] GetCrossings() => crossings;

    public float GetCrossings(int index)
    {
        if (index >= 0 && index < crossings.Length)
        {
            return crossings[index];
        }
        else
        {
            return -1f;
        }
    }


    public void SetWalking(bool value)
    {
        walking = value;
    }


    /// <summary>
    /// Returns the agent's Rigidbody component.
    /// </summary>
    /// <returns>The Rigidbody component</returns>
    public Rigidbody GetRigidBody()
    {
        return rigidBody;
    }


    // NavMesh Control Methods

    /// <summary>
    /// Enables NavMesh navigation mode
    /// </summary>
    public void EnableNavMeshMode()
    {
        isUsingNavMesh = true;
        if (approachingArtifact != null)
            _navMeshActivations[approachingArtifact] = true;    
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

     // Artifact Navigation Methods

    /// <summary>
    /// Handles the trigger enter event
    /// </summary>
    public void OnArtifactTriggerEntered(GameObject agentObject, ArtifactNavigationData navigationData)
    {
        // If this event is for this agent
        if (agentObject != gameObject)
            return;


        currentProximity = ArtifactProximity.Transition;
        Debug.Log($"[TZ] {name} entered TZ, approachingArtifact: {approachingArtifact?.ArtifactName ?? "NULL"}");
        if (approachingArtifact != null)
            _tzEntryTimes[approachingArtifact] = Time.time;
        _lastTrackedPosition = transform.position;
        //isTrackingNavMesh = true;
    
        if (debugging)
            Debug.Log($"[NavMeshTime] {name} START NavMesh for {navigationData.artifact.ArtifactName}");

        // Mark artifact as used
        lastUsedArtifact = navigationData.artifact;
        artifactCooldownTimer = Time.time + artifactCooldown;
        //artifactsUsedThisEpisode.Add(navigationData.artifact);
    
        if (debugging)
            Debug.Log($"[RLAgent] {name} marked {navigationData.artifact.ArtifactName} as used - cooldown until {artifactCooldownTimer:F1}");

        // Check if assigned to this artifact
        if (!assignedArtifacts.Contains(navigationData.artifact))
        {
            if (debugging)
                Debug.Log($"[RLAgentPlanning] Agent {gameObject.name} not assigned to artifact {navigationData.artifact.ArtifactName}");
            return;
        }

        // Check if already navigating
        ArtifactTrigger sourceTrigger = FindTriggerForArtifact(navigationData.artifact);
        if (sourceTrigger != null && sourceTrigger.IsAgentInNavigation(gameObject))
        {
            if (debugging)
                Debug.Log($"[RLAgentPlanning] Agent {gameObject.name} already in navigation - ignoring trigger");
            return;
        }

        // Else start navigation
        StartArtifactNavigation(navigationData, sourceTrigger);
    }

    /// <summary>
    /// Handles the trigger exit event
    /// </summary>
    public void OnArtifactTriggerExited(GameObject agentObject)
    {
        Artifact exitedArtifact = approachingArtifact;
        if (exitedArtifact != null)
            _tzExitTimes[exitedArtifact] = Time.time;

        // Check if this event is for this agent
        if (agentObject != gameObject)
            return;

        if (currentProximity == ArtifactProximity.Transition)
        {
            currentProximity = ArtifactProximity.Far;
        }

        // Check if there is an active handler
        ArtifactNavigationHandler handler = GetComponent<ArtifactNavigationHandler>();
        if (handler != null && handler.IsNavigatingToArtifact)
        {
            // Don't interrupt if still navigating to artifact
            if (debugging)
                Debug.Log($"[RLAgentPlanning] Agent {gameObject.name} exited trigger but still navigating to artifact");
            return;
        }

        // Return to RL control
        StopArtifactNavigation();
    }

    /// <summary>
    /// Finds the trigger associated with a specific artifact
    /// </summary>
    private ArtifactTrigger FindTriggerForArtifact(Artifact artifact)
    {
        foreach (var trigger in subscribedTriggers)
        {
            if (trigger != null && trigger.GetComponent<Artifact>() == artifact)
            {
                return trigger;
            }
        }
        return null;
    }

    /// <summary>
    /// Starts the artifact navigation process
    /// </summary>
    private void StartArtifactNavigation(ArtifactNavigationData navigationData, ArtifactTrigger sourceTrigger)
    {
        
        NavMeshAgent navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            Debug.LogWarning($"[RLAgentPlanning] Agent {gameObject.name} missing NavMeshAgent component");
            return;
        }

        if (debugging)
            Debug.Log($"[RLAgentPlanning] Starting navigation to artifact {navigationData.artifact.ArtifactName}");

        // Navigation Data
        currentNavigationData = navigationData;

        // Add agent to navigation in trigger
        if (sourceTrigger != null)
        {
            sourceTrigger.AddAgentToNavigation(gameObject);
        }

        // Capture current velocity
        Vector3 currentVelocity = rigidBody.velocity;

        // Enable NavMesh mode
        EnableNavMeshMode();

        // Configure NavMeshAgent
        navAgent.stoppingDistance = navigationData.stoppingDistance;
        navAgent.enabled = true;

        // Apply velocity
        Vector3 agentVelocity = Vector3.ClampMagnitude(currentVelocity, navAgent.speed);
        navAgent.velocity = agentVelocity;

        // Set destination
        if (navAgent.isOnNavMesh)
        {
            navAgent.SetDestination(navigationData.destination.position);
        }
        else
        {
            Debug.LogWarning($"[RLAgentPlanning] Agent {gameObject.name} is not on NavMesh");
        }

        // Setup navigation handler
        SetupNavigationHandler();
    }

    /// <summary>
    /// Stops the artifact navigation
    /// </summary>
    private void StopArtifactNavigation()
    {
        NavMeshAgent navAgent = GetComponent<NavMeshAgent>();
        if (navAgent != null)
        {
            if (debugging)
                Debug.Log($"[RLAgentPlanning] Stopping navigation for {gameObject.name}");

            // Remove agent from navigation in trigger
            if (currentNavigationData.HasValue)
            {
                ArtifactTrigger sourceTrigger = FindTriggerForArtifact(currentNavigationData.Value.artifact);
                if (sourceTrigger != null)
                {
                    sourceTrigger.RemoveAgentFromNavigation(gameObject);
                }
            }

            // NavMesh velocity
            Vector3 navMeshVelocity = navAgent.velocity;

            // Disable NavMesh
            navAgent.enabled = false;
            DisableNavMeshMode();

            // Apply velocity to Rigidbody
            Vector3 agentVelocity = Vector3.ClampMagnitude(navMeshVelocity, navAgent.speed);
            rigidBody.velocity = agentVelocity;

            // Cleanup handler
            ArtifactNavigationHandler handler = GetComponent<ArtifactNavigationHandler>();
            if (handler != null)
            {
                Debug.Log($"[RLAgentPlanning] Destroying ArtifactNavigationHandler on {gameObject.name}");
                Destroy(handler);
            }

        }

        currentNavigationData = null;
    }

    /// <summary>
    /// Sets up the ArtifactNavigationHandler component 
    /// </summary>
    private void SetupNavigationHandler()
    {
        if (!currentNavigationData.HasValue)
            return;

        // Check if a handler already exists
        ArtifactNavigationHandler handler = GetComponent<ArtifactNavigationHandler>();
        if (handler == null)
        {
            handler = gameObject.AddComponent<ArtifactNavigationHandler>();
        
            if (debugging)
                Debug.Log($"[RLAgentPlanning] Added new ArtifactNavigationHandler to {gameObject.name}");
        }
        else if (debugging)
        {
            Debug.Log($"[RLAgentPlanning] ArtifactNavigationHandler already exists on {gameObject.name}");
        }

        // Estrai il valore dalla nullable struct
        ArtifactNavigationData navData = currentNavigationData.Value;
    
        handler.StartNavigation(navData.artifact, navData.destination, navData.exitDestination);
    }

    
   private void HandleWarningZoneEnter(GameObject agent, Artifact artifact)
    {
        
        Debug.Log($"[HandleWarningZoneEnter] Called on {name}, agent param: {agent.name}, artifact: {artifact.ArtifactName}");
        if (agent != gameObject) return;

        Debug.Log($"[HandleWarningZoneEnter] {name} passed agent check - proceeding with checks");

        // Check 1: È assegnato?
        if (!assignedArtifacts.Contains(artifact))
        { 
            if (debugging)
                Debug.Log($"[RLAgent] {name} ignoring {artifact.ArtifactName} - not assigned");
            return;
        }

        // Check 2: Cooldown attivo?
        if (lastUsedArtifact == artifact && Time.time < artifactCooldownTimer)
        {
            if (debugging)
                Debug.Log($"[RLAgent] {name} ignoring {artifact.ArtifactName} - cooldown ({artifactCooldownTimer - Time.time:F1}s)");
            return;
        }

        /* Check 3: Già usato questo episodio?
        if (artifactsUsedThisEpisode.Contains(artifact))
        {
            if (debugging)
                Debug.Log($"[RLAgent] {name} ignoring {artifact.ArtifactName} - already used this episode");
            return;
        }
*/
        // Check 4: Già approaching un altro artifact?
        if (approachingArtifact != null && approachingArtifact != artifact)
        {
            if (debugging)
                Debug.Log($"[RLAgent] {name} already approaching {approachingArtifact.ArtifactName} - ignoring {artifact.ArtifactName}");
            return;
        }
        Debug.Log($"[HandleWarningZoneEnter] {name} passed ALL checks - IsInUseByOtherAgent: {artifact.IsInUseByOtherAgent(gameObject)}, currentUser: {artifact.GetCurrentUser()?.name ?? "none"}");

        // Prenota subito — prima di qualsiasi altra operazione
        if (!artifact.SetCurrentUser(gameObject))
        {
            // Artefatto occupato → setta stato e aspetta
            currentProximity = ArtifactProximity.Warning;
            approachingArtifact = artifact;

            ArtifactTrigger waitTrigger = artifact.GetComponentInChildren<ArtifactTrigger>();
            approachingTarget = waitTrigger?.destinationTransform ?? artifact.transform;

            Debug.Log($"[RLAgent] {name} WAITING in Warning Zone for {artifact.ArtifactName}");
            lockPosition = true;
            rigidBody.velocity = Vector3.zero;
            rigidBody.isKinematic = true;
            _waitForArtifactCoroutine = StartCoroutine(WaitForArtifactFree(artifact));
            return;
        }
        _wzEntryTimes[artifact] = Time.time;
        _distancesWZ[artifact] = 0f;
        _distancesTZ[artifact] = 0f;
        _navMeshActivations[artifact] = false;
        _lastTrackedPosition = transform.position;

        currentProximity = ArtifactProximity.Warning;
        approachingArtifact = artifact;

        ArtifactTrigger trigger = artifact.GetComponentInChildren<ArtifactTrigger>();
        if (trigger != null && trigger.destinationTransform != null)
        {
            approachingTarget = trigger.destinationTransform;

            if (debugging)
                Debug.Log($"[RLAgent] {name} steering toward interaction point: {trigger.destinationTransform.name}");
        }
        else
        {
            // Fallback: se non c'è destination, usa centro artifact
            approachingTarget = artifact.transform;

            if (debugging)
                Debug.LogWarning($"[RLAgent] {name} no destination found for {artifact.ArtifactName}, using artifact center");
        }


        if (enableSoftSteering)
        {
            steeringCoroutine = StartCoroutine(SoftSteerTowardsArtifact());
        }

        Debug.Log($"[RLAgent] {name} entering WARNING zone of {artifact.ArtifactName}");
    }
    
    private void HandleWarningZoneExit(GameObject agent, Artifact artifact)
    {
        if (agent != gameObject) return;
    
        Debug.Log($"[RLAgent] {name} HandleWarningZoneExit called for {artifact.ArtifactName}, currentProximity: {currentProximity}, approachingArtifact: {approachingArtifact?.ArtifactName ?? "none"}");

        if (_waitForArtifactCoroutine != null)
        {
            StopCoroutine(_waitForArtifactCoroutine);
            _waitForArtifactCoroutine = null;
            lockPosition = false;
            rigidBody.isKinematic = false;
        }

        if (steeringCoroutine != null)
        {
            StopCoroutine(steeringCoroutine);
            steeringCoroutine = null;
        }
        
        if (approachingArtifact != null)
        {
            _wzExitTimes[approachingArtifact] = Time.time;
            Debug.Log($"[WZExit] {name} wzExit saved for {approachingArtifact.ArtifactName}: {Time.time}");
        }
        else
        {
            Debug.Log($"[WZExit] {name} approachingArtifact è NULL, wzExit non salvato");
        }

        approachingArtifact = null;
        approachingTarget = null;

        if (currentProximity != ArtifactProximity.Transition)
            currentProximity = ArtifactProximity.Far;
    }

    public void OnArtifactInteractionComplete(Artifact artifact)
    {
        //_interactionEndTimes[artifact] = Time.time;
    }

    private float ApplyProximitySpeedModifier(float baseSpeed)
    {
        switch (currentProximity)
        {
            case ArtifactProximity.Warning:
                return baseSpeed * warningSpeedMultiplier;
            case ArtifactProximity.Transition:
                return baseSpeed * transitionSpeedMultiplier;
            default:
                return baseSpeed;
        }
    }


    /*
    
    private float GetProximitySpeedFactor()
    {
        if (!enableAnticipatorySlowdown ||
            currentProximity != ArtifactProximity.Warning ||
            approachingArtifact == null)
            return 1f;

        WarningZoneTrigger wz = approachingArtifact.GetComponentInChildren<WarningZoneTrigger>();
        ArtifactTrigger tz = approachingArtifact.GetComponentInChildren<ArtifactTrigger>();
        if (wz == null) return warningSpeedMultiplier;

        // Centro della Warning Zone in coordinate mondo, sul piano dell'agente
        SphereCollider wzCollider = wz.GetComponent<SphereCollider>();
        Vector3 center = wz.transform.TransformPoint(wzCollider.center);
        center.y = transform.position.y;

        float wzRadius = wz.WarningRadius;
        float tzRadius = 0f;
        if (tz != null)
        {
            SphereCollider tzCollider = tz.GetComponent<SphereCollider>();
            if (tzCollider != null)
                tzRadius = tzCollider.radius * tz.transform.lossyScale.x;
        }

        float distance = Vector3.Distance(transform.position, center);

        // t = 0 al bordo della Warning Zone, t = 1 al bordo della Transition Zone
        float t = Mathf.Clamp01((wzRadius - distance) / Mathf.Max(0.01f, wzRadius - tzRadius));
        return Mathf.Lerp(1f, warningSpeedMultiplier, t);
    }
    */

    void OnDrawGizmos()
    {
        Color proximityColor = currentProximity == ArtifactProximity.Warning ? Color.yellow :
                               currentProximity == ArtifactProximity.Transition ? Color.cyan : Color.green;

        Gizmos.color = proximityColor;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        if (approachingArtifact != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, approachingArtifact.transform.position);
        }
    }

    private System.Collections.IEnumerator SoftSteerTowardsArtifact()
    {

        while (currentProximity == ArtifactProximity.Warning && approachingTarget != null)
        {
            Vector3 directionToTarget = (approachingTarget.position - transform.position).normalized;
            directionToTarget.y = 0; // Solo steering orizzontale

            if (directionToTarget.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    angularSpeed * Time.deltaTime
                );
            }

            yield return null;
        }

        steeringCoroutine = null;
    }

    /// <summary>
    /// Blocca l'agente nella Warning Zone finché l'artefatto non è libero,
    /// poi riprende il flusso normale.
    /// </summary>
    private System.Collections.IEnumerator WaitForArtifactFree(Artifact artifact)
    {

        float logTimer = 0f;
        while (artifact.IsInUseByOtherAgent(gameObject))
        {
            logTimer += Time.deltaTime;
            if (logTimer >= 1f)
            {
                Debug.Log($"[RLAgent] {name} still waiting - isKinematic: {rigidBody.isKinematic}, velocity: {rigidBody.velocity}, lockPosition: {lockPosition}");
                logTimer = 0f;
            }
            yield return null;
        }

        lockPosition = false;
        rigidBody.isKinematic = false;
        _waitForArtifactCoroutine = null;

        // Prenota l'artefatto
        artifact.SetCurrentUser(gameObject);

        Debug.Log($"[RLAgent] {name} resuming toward {artifact.ArtifactName}");

        // L'agente è già nella Warning Zone con approachingArtifact e approachingTarget settati.
        // Basta riavviare lo steering — la Transition Zone farà il resto quando l'agente ci arriva.
        if (enableSoftSteering && steeringCoroutine == null)
        {
            steeringCoroutine = StartCoroutine(SoftSteerTowardsArtifact());
        }
    }
    /*
    public void LogZoneStats(Artifact artifact)
    {
        float wzEntry = _wzEntryTimes.ContainsKey(artifact) ? _wzEntryTimes[artifact] : 0f;
        float tzEntry = _tzEntryTimes.ContainsKey(artifact) ? _tzEntryTimes[artifact] : 0f;
        float distWZ = _distancesWZ.ContainsKey(artifact) ? _distancesWZ[artifact] : 0f;
        float distTZ = _distancesTZ.ContainsKey(artifact) ? _distancesTZ[artifact] : 0f;
        bool navMesh = _navMeshActivations.ContainsKey(artifact) && _navMeshActivations[artifact];

        float tempoWZ = (wzEntry > 0f && tzEntry > 0f) ? tzEntry - wzEntry : 0f;

        float interactionDuration = artifact.GetComponentInChildren<ArtifactInteractionBehavior>()
                                        ?.InteractionDuration ?? 0f;
        float tzExit = _tzExitTimes.ContainsKey(artifact) ? _tzExitTimes[artifact] : 0f;
        float tempoTZ = (tzEntry > 0f && tzExit > 0f) ? (tzExit - tzEntry) - interactionDuration : 0f;
        float tempoTot = _episodeStartTime > 0f ? Time.time - _episodeStartTime : 0f;

        float wzRadius = artifact.GetComponentInChildren<WarningZoneTrigger>()?.WarningRadius ?? 0f;
        float tzRadius = artifact.GetComponentInChildren<ArtifactTrigger>()
                        ?.GetComponent<SphereCollider>()?.radius ?? 0f;

        string header = "agente\tartifatto\tangularSpeed\twarningSpeedMultiplier\twzRadius\ttzRadius\tposObiettivo\tcampione\ttempoWZ\tnavMesh\tdistanzaWZ\tdistanzaTZ\ttempoTZ\ttempoTot";
        string line = FormattableString.Invariant(
            $"{name}\t{artifact.ArtifactName}\t{angularSpeed}\t{warningSpeedMultiplier}\t{wzRadius}\t{tzRadius}\t{posObiettivo}\t{campione}\t{tempoWZ:F2}\t{(navMesh ? 1 : 0)}\t{distWZ:F2}\t{distTZ:F2}\t{tempoTZ:F2}\t{tempoTot:F2}");

        string path = System.IO.Path.Combine(Application.dataPath, "PedPy/ZoneStatsArtifact.txt");
        string dir = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }
        if (!System.IO.File.Exists(path))
        {
            System.IO.File.WriteAllText(path, header + "\n");
        }

        System.IO.File.AppendAllText(path, line + "\n");
    }
    */
    public void LogZoneStatsTotal()
    {
        float totWZ = 0f, totTZ = 0f, totDistWZ = 0f, totDistTZ = 0f;
        //bool navMesh = false;
        // Contatore per le attivazioni NavMesh solo tra gli artefatti assegnati
        int navMeshActivationsCount = 0;

        foreach (var artifact in _assignedArtifacts)
        {
            float wzEntry = _wzEntryTimes.ContainsKey(artifact) ? _wzEntryTimes[artifact] : 0f;
            float wzExit = _wzExitTimes.ContainsKey(artifact) ? _wzExitTimes[artifact] : 0f;
            float tzEntry = _tzEntryTimes.ContainsKey(artifact) ? _tzEntryTimes[artifact] : 0f;
            float tzExit = _tzExitTimes.ContainsKey(artifact) ? _tzExitTimes[artifact] : 0f;

            // tempoWZ: usa tzEntry se disponibile, altrimenti wzExit
            float tempoWZ = wzEntry > 0f ? (tzEntry > 0f ? tzEntry - wzEntry : (wzExit > 0f ? wzExit - wzEntry : 0f)) : 0f;
            float interactionDuration = artifact.GetComponentInChildren<ArtifactInteractionBehavior>()
                                            ?.InteractionDuration ?? 0f;

            totWZ += tempoWZ; // usa il tempoWZ già calcolato sopra con wzExit come fallback
            totTZ += (tzEntry > 0f && tzExit > 0f) ? (tzExit - tzEntry) - interactionDuration : 0f;
            totDistWZ += _distancesWZ.ContainsKey(artifact) ? _distancesWZ[artifact] : 0f;
            totDistTZ += _distancesTZ.ContainsKey(artifact) ? _distancesTZ[artifact] : 0f;
            //navMesh |= _navMeshActivations.ContainsKey(artifact) && _navMeshActivations[artifact];
            // Incrementa se la NavMesh è stata usata per questo specifico artefatto
            if (_navMeshActivations.ContainsKey(artifact) && _navMeshActivations[artifact])
            {
                navMeshActivationsCount++;
            }
        }

        float navMeshPercentage = _assignedArtifacts.Count > 0 ? (float)navMeshActivationsCount / _assignedArtifacts.Count : 0f;
        
        Debug.Log($"[ZoneStats] wzEntryTimes count: {_wzEntryTimes.Count}, tzEntryTimes count: {_tzEntryTimes.Count}");
        foreach (var kv in _wzEntryTimes)
            Debug.Log($"  artifact: {kv.Key.ArtifactName}, wzEntry: {kv.Value}, tzEntry: {(_tzEntryTimes.ContainsKey(kv.Key) ? _tzEntryTimes[kv.Key].ToString() : "MANCANTE")}");
            

        Debug.Log($"[ZoneStats] Time.time={Time.time}, _episodeStartTime={_episodeStartTime}");
        float tempoTot = _episodeStarted ? Time.time - _episodeStartTime : 0f;

        if (_assignedArtifacts == null || _assignedArtifacts.Count == 0)
        {
            Debug.LogWarning("[ZoneStats] _assignedArtifacts vuoto, skip log.");
            return;
        }

        // Prendi wzRadius e tzRadius dal primo artefatto assegnato
        Artifact first = _assignedArtifacts[0];
        float wzRadius = first.GetComponentInChildren<WarningZoneTrigger>()?.WarningRadius ?? 0f;
        float tzRadius = first.GetComponentInChildren<ArtifactTrigger>()
                            ?.GetComponent<SphereCollider>()?.radius ?? 0f;

        // Dichiarazione path PRIMA del check timeout
        string path = System.IO.Path.Combine(Application.dataPath, "PedPy/ZoneStatsArtifact.txt");
        string dir = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        string header = "agente\tconfigNumber\tangularSpeed\twarningSpeedMultiplier\twzRadius\ttzRadius\tposObiettivo\tcampione\ttempoWZ\tnavMesh\tdistanzaWZ\tdistanzaTZ\ttempoTZ\ttempoTot\ttimeout";
        if (!System.IO.File.Exists(path))
            System.IO.File.WriteAllText(path, header + "\n");

        // Ora il check timeout
        if (_timedOut)
        {
            string timeoutLine = FormattableString.Invariant($"{name}\t{configNumber}\t{angularSpeed}\t{warningSpeedMultiplier}\t{wzRadius}\t{tzRadius}\t{posObiettivo}\t{campione}\t0\t0\t0\t0\t0\t0"); 
            System.IO.File.AppendAllText(path, timeoutLine + "\n");
            return;
        }

        // Riga normale: timeout=0
        string line = FormattableString.Invariant($"{name}\t{configNumber}\t{angularSpeed}\t{warningSpeedMultiplier}\t{wzRadius}\t{tzRadius}\t{posObiettivo}\t{campione}\t{totWZ:F2}\t{navMeshPercentage:F2}\t{totDistWZ:F2}\t{totDistTZ:F2}\t{totTZ:F2}\t{tempoTot:F2}");
        System.IO.File.AppendAllText(path, line + "\n");
    }

    public void LogArtifactStatsDetail()
    {
        string path = System.IO.Path.Combine(Application.dataPath, "PedPy/ArtifactStatsDetail.txt");
        string header = "configNumber\tagente\tartifactName\ttempoWZ\ttempoTZ\tdistWZ\tdistTZ\tnavMesh";

        if (!System.IO.File.Exists(path))
            System.IO.File.WriteAllText(path, header + "\n");

        if (_timedOut)
        {
            foreach (var artifact in _assignedArtifacts)
            {
                string timeoutLine = $"{configNumber}\t{name}\t{artifact.ArtifactName}\t0\t0\t0\t0\t0";
                System.IO.File.AppendAllText(path, timeoutLine + "\n");
            }
            return;
        }

        foreach (var artifact in _assignedArtifacts)
        {
            float wzEntry = _wzEntryTimes.ContainsKey(artifact)  ? _wzEntryTimes[artifact]  : 0f;
            float wzExit  = _wzExitTimes.ContainsKey(artifact)   ? _wzExitTimes[artifact]   : 0f;
            float tzEntry = _tzEntryTimes.ContainsKey(artifact)  ? _tzEntryTimes[artifact]  : 0f;
            float tzExit  = _tzExitTimes.ContainsKey(artifact)   ? _tzExitTimes[artifact]   : 0f;
            float distWZ  = _distancesWZ.ContainsKey(artifact)   ? _distancesWZ[artifact]   : 0f;
            float distTZ  = _distancesTZ.ContainsKey(artifact)   ? _distancesTZ[artifact]   : 0f;
            bool  nav     = _navMeshActivations.ContainsKey(artifact) && _navMeshActivations[artifact];

            float tempoWZ = wzEntry > 0f
                ? Mathf.Max(0f, tzEntry > 0f ? tzEntry - wzEntry : (wzExit > 0f ? wzExit - wzEntry : 0f))
                : 0f;

            float interactionDuration = artifact.GetComponentInChildren<ArtifactInteractionBehavior>()
                                            ?.InteractionDuration ?? 0f;

            float tempoTZ = (tzEntry > 0f && tzExit > 0f)
                ? Mathf.Max(0f, (tzExit - tzEntry) - interactionDuration)
                : 0f;

            string line = FormattableString.Invariant(
                $"{configNumber}\t{name}\t{artifact.ArtifactName}\t{tempoWZ:F2}\t{tempoTZ:F2}\t{distWZ:F2}\t{distTZ:F2}\t{(nav ? 1 : 0)}");

            System.IO.File.AppendAllText(path, line + "\n");
        }
    }
}