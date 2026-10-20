# VR-Navigation-Zones

Unity project and experiment pipeline of the bachelor thesis
**«Artefatti e Movimenti di NPC in Unity per scenari di VR realistici»**
(*Artifacts and NPC movement in Unity for realistic VR scenarios*).

> Geremy Rea, Università degli Studi di Milano-Bicocca, Computer Science.
> Supervisor: Prof.ssa Daniela Briola. Co-supervisor: Prof. Giuseppe Vizzari.

- Thesis: [`docs/tesi.pdf`](docs/tesi.pdf)
- Presentation: [`docs/presentazione_tesi.pdf`](docs/presentazione_tesi.pdf)
- Guide to the zone system (setup in new scenes, parameters, output files): [`docs/GUIDE.md`](docs/GUIDE.md)
- Online questionnaire: <https://geremyrea.github.io/studio-movimento/>

## Overview

The project extends [VR-Navigation-Pedestrian](https://github.com/Zeptogram/VR-Navigation-Pedestrian)
(Elio Gargiulo, 2025), where ML-Agents pedestrians move in a VR environment and interact with
*artifacts* (totems, monitors, benches...). In the original project an agent switches from its RL
policy to the NavMesh as soon as it enters an artifact's trigger, which makes the movement change
abruptly.

This thesis adds two concentric zones around each artifact:

| Zone | Script | Behaviour |
|---|---|---|
| **Warning Zone (WZ)** | `WarningZoneTrigger.cs` | The agent keeps its RL policy but slows down (`warningSpeedMultiplier`) and gradually turns towards the interaction point (`angularSpeed`, in °/s). |
| **Transition Zone (TZ)** | `ArtifactTrigger.cs` | The agent switches to the NavMesh to reach the interaction point, uses the artifact, then goes back to RL control. |

The agent logic is in `RLAgentPlanning.cs` (proximity states `Far` → `Warning` → `Transition`).
An automated pipeline runs the parameter grid, measures time and distance in each zone and
saves a trajectory plot for every run. See [`docs/GUIDE.md`](docs/GUIDE.md) to add the zones
to new artifacts or scenes.

## Repository structure

```
UnityProject/           Unity project (open this folder in Unity Hub)
  Assets/
    Agents/             agents, RLAgentPlanning.cs, ConfigLoader.cs
    Artifacts/          artifacts, WarningZoneTrigger.cs, ArtifactTrigger.cs, ArtifactNavigationHandler.cs
    Editor/             AutoPlayManager.cs (starts Play mode for the pipeline)
    PedPy/              pipeline scripts, Excel grid and outputs
    Scenes/             scenes
    Scripts/            SimulationManager.cs, PedPySceneExporter.cs, ...
  requirements_windows.txt, requirements_macos.yml   ML-Agents environment of the original project
analysis/               Jupyter notebook with the statistical analysis
data/                   results used in the thesis
docs/                   thesis and presentation (PDF), GUIDE.md
```

## Requirements

- **Unity 2022.3.62f2** (Unity Hub). The ML-Agents and XR packages are listed in `Packages/manifest.json`.
- **Git LFS**: 3D models, textures and libraries are stored with LFS.
- **Python 3.10** with PedPy for the pipeline:
  ```bash
  conda create --name pedpyvenv python=3.10
  conda activate pedpyvenv
  pip install pedpy==1.2.0 pandas openpyxl matplotlib seaborn pillow
  ```
  `ffmpeg` is only needed by `prepara_media.py` to compress the questionnaire videos.
- The ML-Agents environment (`requirements_windows.txt` / `requirements_macos.yml`) is only needed to train new models.

## Getting started

```bash
git lfs install
git clone https://github.com/grea17/VR-Navigation-Zones.git
```

Open `UnityProject` in Unity Hub (*Add → Add project from disk*). The first import rebuilds the
`Library` folder and may take a while.

## Running the experiments

The pipeline runs one configuration at a time, with the Unity Editor open on the project:

```bash
cd UnityProject/Assets/PedPy
python master_run.py            # from START_CONFIG to END_CONFIG
python master_run.py 250        # resume from configuration 250
```

For each configuration:

1. `set_config.py` reads the row from the Excel grid and writes `config.json`.
2. `master_run.py` creates `start_play.txt`; `AutoPlayManager.cs` sees it and enters Play mode.
3. `ConfigLoader.cs` loads the scene (`WZ1` objective inside the WZ, `WZ2` outside) and applies the parameters.
4. At the end of the episode `RLAgentPlanning.cs` appends the metrics to `ZoneStatsArtifact.txt`
   (one row per episode) and `ArtifactStatsDetail.txt` (one row per artifact).
   Episodes longer than 120 s are stopped and logged with all metrics at 0.
5. `SimulationManager.cs` writes `sim_done.txt` and exits Play mode.
6. `pedPy.py` saves the trajectory plot in `traiettorie/configurazione<N>.png`.

At the end (or after Ctrl+C) `fill_excel.py` copies the results into the Excel grid.

**Choosing the grid.** `START_CONFIG`/`END_CONFIG` in `master_run.py` and `EXCEL_PATH` in
`set_config.py` and `fill_excel.py` select the experiment:

| Experiment | Configurations | Excel file |
|---|---|---|
| Main grid | 1–1296 | `steering_test.xlsx` |
| Integrative test | 1297–1476 | `test_integrativo.xlsx` |

The scripts are currently set to the integrative test. The Excel file must be in `Assets/PedPy`:
`steering_test.xlsx` there is the empty main grid, while `test_integrativo.xlsx` is in `data/`.

## Experiment design

| Parameter | Values |
|---|---|
| `angularSpeed` (°/s) | 30, 60, 90, 120, 150, 180 |
| `warningSpeedMultiplier` | 0.5, 0.65, 0.8, 0.95 |
| WZ radius (m) | 3, 4, 5 |
| TZ radius (m) | WZ/4 × 1, 2, 3 |
| Objective position | inside the WZ (`WZ1`), outside the WZ (`WZ2`) |
| Repetitions | 3 |

6 × 4 × 3 × 3 × 2 = 432 configurations, × 3 repetitions = 1296 runs.

The integrative test repeats 9 configurations 20 times each (runs 1297–1476): the fastest
configurations of the main grid, all with the objective inside the WZ.

## Data and analysis

| File | Content |
|---|---|
| `data/steering_test_final.xlsx` | results of the 1296 runs (sheet `Test Completo`) |
| `data/test_integrativo.xlsx` | results of the integrative test |
| `data/Risposte questionario.xlsx` | anonymous answers to the questionnaire |
| `analysis/Analisi_Parametri_Simulazione_finale_3.ipynb` | statistical analysis (run it from `analysis/`) |

The questionnaire material is produced by three scripts in `Assets/PedPy`:
`confronto_distanza_tempo.py` (reruns the selected configurations and the NavMesh baseline),
`genera_overlay_coppie.py` (overlaid trajectory images) and `prepara_media.py` (renames and
compresses images and videos).

## Versions

- Tag [`tesi`](https://github.com/grea17/VR-Navigation-Zones/tree/tesi): the exact code and data used for the results of the thesis.
- `main`: same behaviour, with cleaned code (comments in English, unused code removed).

## Credits

- [VR-Navigation-Pedestrian](https://github.com/Zeptogram/VR-Navigation-Pedestrian) by Elio Gargiulo, the base project.
- [Unity ML-Agents](https://github.com/Unity-Technologies/ml-agents) and [PedPy](https://github.com/PedestrianDynamics/PedPy).
- Third-party assets (Meta/Oculus SDK, Brick Project Studio, Unity Standard Assets, character models) keep their own licenses.
