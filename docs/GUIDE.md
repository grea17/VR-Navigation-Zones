# Guide: Warning Zones and Transition Zones

This guide explains how the zone system works and how to use it in a new scene or with a new
artifact. It assumes you know the base project
([VR-Navigation-Pedestrian](https://github.com/Zeptogram/VR-Navigation-Pedestrian)): agents,
targets and artifacts are set up as described there. For the motivation and the results, see
the thesis in [`tesi.pdf`](tesi.pdf).

## 1. How it works

Each artifact can have two concentric zones:

- **Warning Zone (WZ)**, the outer zone. The agent keeps its RL policy, but its speed is multiplied by
  `warningSpeedMultiplier`, and it turns towards the interaction point at most `angularSpeed` degrees per second.
- **Transition Zone (TZ)**, the inner zone. The agent switches to the NavMesh, reaches the interaction point,
  uses the artifact, walks to the exit point and goes back to RL control.

The agent (`RLAgentPlanning.cs`) has three proximity states: `Far`, `Warning`, `Transition`.
The zones notify all agents through static events; each agent ignores the events that are not about itself.

```
Far ──enters WZ──▶ Warning ──enters TZ──▶ Transition ──leaves TZ──▶ Far
                    slows down             NavMesh to the
                    soft steering          interaction point,
                                           Use(), exit point
```

When the agent enters a WZ (`HandleWarningZoneEnter`), the zone is used only if:

1. the artifact is in the agent's **Assigned Artifacts** list;
2. the agent did not use the same artifact in the last `artifactCooldown` seconds (default 5);
3. the agent is not already approaching another artifact.

The agent then reserves the artifact (`Artifact.SetCurrentUser`). If another agent is using it, the agent
stops in the WZ and waits until the artifact is free (`WaitForArtifactFree`).

When the agent enters the TZ (`OnArtifactTriggerEntered`), it switches to NavMesh mode. Its Rigidbody
becomes kinematic, the `NavMeshAgent` is enabled and an `ArtifactNavigationHandler` is added at runtime.
The handler waits until the agent reaches the interaction point and then:

- starts the interaction (`ArtifactInteractionBehavior`), with the animation and `Use()`;
- releases the artifact;
- sends the agent to the exit point.

When the agent leaves the TZ, the NavMesh is turned off and the agent goes back to RL control, keeping its
current velocity.

## 2. Start from an existing scene

The easiest way to create a new scene is to **duplicate one of the experiment scenes** (`WZ1`, `WZ2`)
and change the environment. They already contain the objects the pipeline needs:

| Object / script | Purpose |
|---|---|
| `ConfigLoader` | Reads `Assets/PedPy/config.json` in `Awake`, loads the requested scene and applies the parameters to agents and zones. |
| `SimulationManager` | Counts the objects tagged `Agente`. When all of them have finished, it writes `Assets/PedPy/sim_done.txt` and exits Play mode. |
| `PedPySceneExporter` | Writes `SceneBlueprint.json` with the active objectives, targets and zones (used by the plots). Objectives and targets must be children of objects named `Objectives` and `Targets`. |
| Camera with `PedPyInitializer` | Renders the top view to `BackgroundMap.png` and deletes the trajectory files of the previous episode. |

<!-- Screenshot: Hierarchy of WZ1 with these objects. -->

The scene must be listed in **File → Build Settings**, because `ConfigLoader` loads it with `SceneManager.LoadScene`.

## 3. Add the zones to an artifact

Recommended hierarchy:

```
Totem4                 Artifact subclass (e.g. TotemArtifact) + ArtifactInteractionBehavior
├── WarningZone        WarningZoneTrigger (SphereCollider added automatically)
└── TransitionZone     ArtifactTrigger + SphereCollider (Is Trigger)
```

The scripts find each other with `GetComponentInChildren` / `GetComponentInParent`: the zones must be
**children of the artifact** (or on the artifact itself).

The interaction point and the exit point are not separate scripts. They are two fields of
`ArtifactTrigger`, **Destination Transform** and **Exit Destination**, set in the Inspector.
Each field references a position in the scene, for example an empty GameObject placed where the
agent should stop.

### WarningZoneTrigger

| Field | Meaning |
|---|---|
| Target Artifact | Optional: if empty, the artifact in the parent is used. |
| Warning Radius | Radius in meters. Overwritten at runtime by `wzRadius` from `config.json`. |
| Center Target | Optional: Transform to center the zone on (e.g. the interaction point). |
| Gizmo settings | Colors of the sphere drawn in the Scene view. |

The script adds and configures its own `SphereCollider` (trigger, radius) and sets its own scale to
`1 / parent scale`, so the radius is always in meters. **Put it on its own child object**: on the
artifact itself it would change the artifact's scale. The compensation uses the X scale of the
parent, so the parent should have a uniform scale.

<!-- Screenshot: Inspector of WarningZoneTrigger. -->

### ArtifactTrigger (Transition Zone)

| Field | Meaning |
|---|---|
| Target Artifact | Optional: if empty, the artifact in the parent is used. |
| Destination Transform | Interaction point. It is also the target of the soft steering in the WZ. |
| Exit Destination | Point the agent walks to after the interaction. If empty, the agent goes back to RL control immediately. |
| Stopping Distance | `NavMeshAgent` stopping distance near the interaction point. |
| Center Target | Optional: Transform to center the zone on. |

Unlike the WZ, the TZ **does not configure its collider completely**:

- tick **Is Trigger** on the `SphereCollider` by hand;
- keep the object at scale 1, because `ConfigLoader` writes `tzRadius` directly into the collider radius.

<!-- Screenshot: Inspector of ArtifactTrigger and its SphereCollider. -->

### ArtifactInteractionBehavior

| Field | Meaning |
|---|---|
| Interaction Duration | Seconds of the interaction. It is subtracted from the time spent in the TZ in the metrics. |
| Call Use After Animation | If on, `Use()` is called at the end of the animation, otherwise at the start. |
| Animation Trigger / Play Animation | Animator trigger played during the interaction. |
| One Time Use Per Agent | If on, each agent can use the artifact only once. |
| Look At settings | Optional rotation towards a target during the interaction. |

## 4. Configure the agent

The ML-Agent (`RLAgentPlanning`) needs:

- the tag **`Agente`**: zones and `SimulationManager` recognize agents by tag;
- the artifact in the **Assigned Artifacts** list. Only assigned artifacts trigger the zones and are counted in the metrics;
- a **`NavMeshAgent`** component, and a **baked NavMesh** covering the area around the artifacts.
  The script enables NavMesh mode in the TZ and disables it when the agent leaves.

Parameters in the Inspector:

| Field | Default | Meaning |
|---|---|---|
| Angular Speed | 120 | Maximum turning speed (°/s) of the soft steering in the WZ. Set by `config.json`. |
| Warning Speed Multiplier | 0.85 | Speed factor in the WZ. Set by `config.json`. |
| Transition Speed Multiplier | 0.6 | Speed factor in the TZ, only used when the agent is in the TZ without NavMesh navigation. |
| Enable Anticipatory Slowdown | on | Turns the speed factors on/off. |
| Enable Soft Steering | on | Turns the steering in the WZ on/off. |
| Artifact Cooldown | 5 | Seconds before the same artifact can be used again. |
| Campione, Pos Obiettivo, Config Number | | Written by `ConfigLoader`, only copied into the result files. |

An episode longer than 120 s (`_maxEpisodeDuration` in the code) is ended and logged with all metrics at 0.

## 5. Add a scene or a parameter to the pipeline

- **Scene choice.** `set_config.py` chooses the scene from the `Pos. Obiettivo` column:
  `"Dentro WZ"` → `WZ1`, anything else → `WZ2`. For a new scene, change this line or add a column to the grid.
- **Excel grid.** The `Test Completo` sheet needs the columns `Configurazione`, `angularSpeed (°/s)`,
  `warningSpeed Multiplier`, `WZ Radius (m)`, `TZ Radius (m)`, `Pos. Obiettivo`, `Campione`
  (with the line breaks used in `data/steering_test_final.xlsx`). `fill_excel.py` adds the result columns.
- **New parameter.** Add it in four places:
  1. the grid;
  2. `set_config.py`, which writes it to `config.json`;
  3. the `ConfigData` class in `ConfigLoader.cs`, which applies it;
  4. if it must appear in the results, the rows written by `LogZoneStatsTotal` in `RLAgentPlanning.cs`.
- **Plots.** `plot_trajectories_with_zones` in `pedPy.py` uses limits fixed for the current room
  (`set_xlim(-1100, 800)`, `set_ylim(-350, 1450)`, in Unity meters × 50) and a background covering ±35 m.
  Adjust them for a different environment.

## 6. Output files

All files are written in `Assets/PedPy`. Distances are in meters, times in seconds.

**`ZoneStatsArtifact.txt`**, one row per episode, values summed over the assigned artifacts:

| Column | Meaning |
|---|---|
| `tempoWZ` | Time from entering the WZ to entering the TZ (or to leaving the WZ, if the TZ was never reached). |
| `navMesh` | Fraction (0–1) of assigned artifacts for which NavMesh mode was activated. |
| `distanzaWZ`, `distanzaTZ` | Distance walked while in the Warning / Transition state. |
| `tempoTZ` | Time in the TZ minus the interaction duration. |
| `tempoTot` | Duration of the episode. |

The first columns repeat the parameters of the configuration. The header also lists a `timeout` column,
but no value is written for it: a timed-out episode is recognizable because all metrics are 0.

**`ArtifactStatsDetail.txt`**, one row per assigned artifact, with the same metrics per artifact
(`navMesh` is 0 or 1, negative times are clamped to 0).

**Other files:**

- `PedPyStats.txt`: the trajectory of the episode;
- `SceneBlueprint.json`: the scene layout;
- `traiettorie/configurazione<N>.png`: the trajectory plot. Repeated runs get `.1`, `.2`, ... suffixes.

## 7. Troubleshooting and known limitations

| Problem | What to check |
|---|---|
| The agent ignores a zone | Tag `Agente`, artifact in Assigned Artifacts, cooldown, agent already approaching another artifact. |
| The agent stops inside the WZ | The artifact is reserved by another agent: it waits until the artifact is free. |
| The agent never switches to NavMesh | `NavMeshAgent` on the agent, baked NavMesh, TZ collider set as trigger. |
| `master_run.py` waits forever | `SimulationManager` in the scene; every object tagged `Agente` must finish; launch the script from `Assets/PedPy` with the Unity Editor open. |
| The scene is not loaded | Scene missing from Build Settings, or wrong `sceneName` in `config.json`. |

Known limitations:

- **One agent per episode.** The metrics were designed for one agent per episode, as in the experiments of the thesis.
  With several agents:
  - the TZ exit time is recorded for every agent that is approaching an artifact, not only for the agent that leaves;
  - an agent that waits for a busy artifact does not record its WZ entry time.
- **`fill_excel.py` and the header line.** `fill_excel.py` reads `ArtifactStatsDetail.txt` as a file without a header line.
  If the file is created from scratch, Unity writes a header line first. Remove it before running `fill_excel.py`.
