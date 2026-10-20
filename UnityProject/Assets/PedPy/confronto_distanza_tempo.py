"""
confronto_distanza_tempo.py

Reruns the 25 best configurations (ML-Agent, scene WZ1) and one NavMesh
baseline (scene WZ1_NV), extracting total time and distance and saving
a trajectory image for each episode, all in a separate folder
dedicated to this comparison.

It does NOT touch steering_test.xlsx / fill_excel.py: all data goes to
confronto_dist_tempo/, never to the main dataset.

It reuses the functions of pedPy.py (preprocess_file, load_trajectory,
compute_agent_metrics, plot_trajectories_with_zones).
"""

import os
import json
import time
import pathlib
import pandas as pd

OUTPUT_DIR = pathlib.Path("confronto_dist_tempo")
OUTPUT_DIR.mkdir(exist_ok=True)
TRAJ_IMG_DIR = OUTPUT_DIR / "traiettorie"
TRAJ_IMG_DIR.mkdir(exist_ok=True)

TRAJECTORY_FILE = "PedPyStats.txt"
SIM_DONE_FILE = "sim_done.txt"

# The 25 best configurations (ML-Agent, scene WZ1). The order defines
# the IDs: the first is "mlagent_01", the second "mlagent_02", and so on.
CONFIGURAZIONI_TOP24 = [
    dict(angularSpeed=150, warningSpeedMultiplier=0.65, wzRadius=5, tzRadius=1.25, posObiettivo="Dentro WZ"),
    dict(angularSpeed=150, warningSpeedMultiplier=0.8, wzRadius=5, tzRadius=1.25, posObiettivo="Dentro WZ"),
    dict(angularSpeed=90, warningSpeedMultiplier=0.65, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=120, warningSpeedMultiplier=0.8, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=150, warningSpeedMultiplier=0.5, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=90, warningSpeedMultiplier=0.95, wzRadius=5, tzRadius=1.25, posObiettivo="Dentro WZ"),
    dict(angularSpeed=30, warningSpeedMultiplier=0.5, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=90, warningSpeedMultiplier=0.8, wzRadius=5, tzRadius=1.25, posObiettivo="Dentro WZ"),
    dict(angularSpeed=90, warningSpeedMultiplier=0.65, wzRadius=5, tzRadius=1.25, posObiettivo="Dentro WZ"),
    dict(angularSpeed=180, warningSpeedMultiplier=0.5, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=150, warningSpeedMultiplier=0.8, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=30, warningSpeedMultiplier=0.8, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=120, warningSpeedMultiplier=0.5, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=120, warningSpeedMultiplier=0.65, wzRadius=5, tzRadius=1.25, posObiettivo="Dentro WZ"),
    dict(angularSpeed=120, warningSpeedMultiplier=0.65, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=90, warningSpeedMultiplier=0.5, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=180, warningSpeedMultiplier=0.8, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=30, warningSpeedMultiplier=0.65, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=120, warningSpeedMultiplier=0.95, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=150, warningSpeedMultiplier=0.65, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=60, warningSpeedMultiplier=0.65, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=60, warningSpeedMultiplier=0.65, wzRadius=5, tzRadius=1.25, posObiettivo="Dentro WZ"),
    dict(angularSpeed=180, warningSpeedMultiplier=0.95, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
    dict(angularSpeed=120, warningSpeedMultiplier=0.5, wzRadius=5, tzRadius=1.25, posObiettivo="Dentro WZ"),
    dict(angularSpeed=90, warningSpeedMultiplier=0.8, wzRadius=5, tzRadius=3.75, posObiettivo="Dentro WZ"),
]


def pulizia_iniziale():
    """Removes leftovers of previously interrupted runs."""
    if pathlib.Path(SIM_DONE_FILE).exists():
        pathlib.Path(SIM_DONE_FILE).unlink()
        print(f"Removed leftover: {SIM_DONE_FILE}")


def scrivi_config(cfg: dict, config_id: str, scene: str):
    """Writes config.json in the same format as set_config.py."""
    config = {
        "configNumber": config_id,
        "sceneName": scene,
        "angularSpeed": float(cfg["angularSpeed"]),
        "warningSpeedMultiplier": float(cfg["warningSpeedMultiplier"]),
        "wzRadius": float(cfg["wzRadius"]),
        "tzRadius": float(cfg["tzRadius"]),
        "posObiettivo": cfg["posObiettivo"],
        "campione": 1,
    }
    with open("config.json", "w") as f:
        json.dump(config, f, indent=2)
    print(f"    Config written -> scene: {scene}, {cfg}")


def lancia_episodio_e_attendi():
    """Same mechanism as master_run.py: start_play.txt / sim_done.txt."""
    with open("start_play.txt", "w") as f:
        f.write("go")
    print("    Waiting for the simulation to end...")
    while not os.path.exists(SIM_DONE_FILE):
        time.sleep(0.5)
    os.remove(SIM_DONE_FILE)


def estrai_tempo_distanza_e_traiettoria(config_id: str, cfg: dict, agente: str) -> dict:
    """
    Reads PedPyStats.txt, extracts total time and distance, archives the raw
    trajectory with a unique name (overwriting any existing file with the same
    config_id) and saves a trajectory image.
    """
    from pedPy import preprocess_file, load_trajectory, compute_agent_metrics, plot_trajectories_with_zones

    clean_path = f"PedPyStats_clean_{config_id}.txt"
    if not preprocess_file(TRAJECTORY_FILE, clean_path):
        print(f"    WARNING: trajectory not found for {config_id}")
        return None

    traj = load_trajectory(trajectory_file=pathlib.Path(clean_path),
                            default_frame_rate=100, default_unit=1)
    metrics = compute_agent_metrics(traj)

    if len(metrics) == 0:
        print(f"    WARNING: no agents found in {config_id}")
        return None

    # Trajectory image for this comparison
    img_path = TRAJ_IMG_DIR / f"{config_id}.png"
    try:
        agent_id = traj.data["id"].unique()[0]
        plot_trajectories_with_zones(traj, agent_colors={str(agent_id): "#1D9E75"},
                                      save_path=str(img_path))
        print(f"    Trajectory saved: {img_path}")
    except Exception as e:
        print(f"    WARNING: could not plot {config_id}: {e}")

    # Archive the raw trajectory; .replace() overwrites any file
    # with the same name from a previous run
    raw_archive = OUTPUT_DIR / f"trajectory_{config_id}.txt"
    if pathlib.Path(TRAJECTORY_FILE).exists():
        pathlib.Path(TRAJECTORY_FILE).replace(raw_archive)

    row = metrics.iloc[0]
    return {
        "config_id": config_id,
        "agente": agente,
        "angularSpeed": cfg.get("angularSpeed"),
        "warningSpeedMultiplier": cfg.get("warningSpeedMultiplier"),
        "wzRadius": cfg.get("wzRadius"),
        "tzRadius": cfg.get("tzRadius"),
        "posObiettivo": cfg.get("posObiettivo"),
        "TempoTot": row["duration"],
        "Distanza": row["distance"],
        "VelocitaMedia": row["avg_speed"],
        "ImmagineTraiettoria": str(img_path),
    }


def main():
    pulizia_iniziale()
    risultati = []

    # The ML-Agent runs are disabled (already done): remove the quotes to run them again.
    '''
    # --- 25 ML-Agent configurations, scene WZ1 ---
    for i, cfg in enumerate(CONFIGURAZIONI_TOP24, start=1):
        config_id = f"mlagent_{i:02d}"
        print(f"\n{'='*50}")
        print(f"ML-AGENT {i}/{len(CONFIGURAZIONI_TOP24)} — {cfg}")
        print(f"{'='*50}")

        scrivi_config(cfg, config_id, scene="WZ1")
        lancia_episodio_e_attendi()
        r = estrai_tempo_distanza_e_traiettoria(config_id, cfg, agente="ML-Agent")
        if r:
            risultati.append(r)
    '''
    # --- 1 pure NavMesh baseline, scene WZ1_NV (deterministic, a single run) ---
    print(f"\n{'='*50}")
    print("BASELINE NAVMESH (WZ1_NV) — 1 run")
    print(f"{'='*50}")

    cfg_navmesh = dict(angularSpeed=150, warningSpeedMultiplier=0.65,
                        wzRadius=5, tzRadius=1.25, posObiettivo="Dentro WZ")
    scrivi_config(cfg_navmesh, "navmesh_baseline", scene="WZ1_NV")
    lancia_episodio_e_attendi()
    r = estrai_tempo_distanza_e_traiettoria("navmesh_baseline", cfg_navmesh, agente="NavMesh")
    if r:
        risultati.append(r)

    # --- Save everything in one CSV, separate from steering_test.xlsx ---
    df = pd.DataFrame(risultati)
    df.to_csv(OUTPUT_DIR / "confronto_distanza_tempo.csv", index=False)
    print(f"\nDone. Results saved in: {OUTPUT_DIR / 'confronto_distanza_tempo.csv'}")
    print(f"Trajectory images in: {TRAJ_IMG_DIR}")
    print("fill_excel.py was NOT called: steering_test.xlsx is unchanged.")


if __name__ == "__main__":
    main()