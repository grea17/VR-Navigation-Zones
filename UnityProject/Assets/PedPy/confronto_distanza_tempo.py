"""
confronto_distanza_tempo.py

Rilancia le 25 configurazioni migliori (ML-Agent, scena WZ1) + 1 baseline
NavMesh (scena WZ1_NV), estraendo TempoTot, distanza percorsa e salvando
un'immagine della traiettoria per ciascun episodio — tutto in una cartella
separata, dedicata a questo confronto.

NON tocca steering_test.xlsx / fill_excel.py: tutti i dati vanno in
confronto_dist_tempo/, mai nel dataset principale.

Riusa direttamente le funzioni già pronte in pedPy.py (preprocess_file,
load_trajectory, compute_agent_metrics, plot_trajectories_with_zones),
senza duplicarne la logica.
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

# Le 25 configurazioni migliori (ML-Agent, scena WZ1). L'ordine qui
# determina gli ID: la prima è "mlagent_01", la seconda "mlagent_02", ecc.
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
    """Rimuove eventuali residui di run precedenti interrotti a metà."""
    if pathlib.Path(SIM_DONE_FILE).exists():
        pathlib.Path(SIM_DONE_FILE).unlink()
        print(f"Rimosso residuo: {SIM_DONE_FILE}")


def scrivi_config(cfg: dict, config_id: str, scene: str):
    """Scrive config.json nello stesso formato di set_config.py."""
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
    print(f"    Config scritta → scena: {scene}, {cfg}")


def lancia_episodio_e_attendi():
    """Stesso meccanismo di master_run.py: start_play.txt / sim_done.txt."""
    with open("start_play.txt", "w") as f:
        f.write("go")
    print("    In attesa che la simulazione finisca...")
    while not os.path.exists(SIM_DONE_FILE):
        time.sleep(0.5)
    os.remove(SIM_DONE_FILE)


def estrai_tempo_distanza_e_traiettoria(config_id: str, cfg: dict, agente: str) -> dict:
    """
    Legge PedPyStats.txt, estrae TempoTot/distanza, archivia la traiettoria
    grezza con nome univoco (sovrascrivendo eventuali file già esistenti con
    lo stesso config_id), e genera un'immagine di traiettoria dedicata.
    """
    from pedPy import preprocess_file, load_trajectory, compute_agent_metrics, plot_trajectories_with_zones

    clean_path = f"PedPyStats_clean_{config_id}.txt"
    if not preprocess_file(TRAJECTORY_FILE, clean_path):
        print(f"    ATTENZIONE: traiettoria non trovata per {config_id}")
        return None

    traj = load_trajectory(trajectory_file=pathlib.Path(clean_path),
                            default_frame_rate=100, default_unit=1)
    metrics = compute_agent_metrics(traj)

    if len(metrics) == 0:
        print(f"    ATTENZIONE: nessun agente trovato in {config_id}")
        return None

    # Immagine di traiettoria dedicata a questo confronto
    img_path = TRAJ_IMG_DIR / f"{config_id}.png"
    try:
        agent_id = traj.data["id"].unique()[0]
        plot_trajectories_with_zones(traj, agent_colors={str(agent_id): "#1D9E75"},
                                      save_path=str(img_path))
        print(f"    Traiettoria salvata: {img_path}")
    except Exception as e:
        print(f"    ATTENZIONE: impossibile generare il grafico per {config_id}: {e}")

    # Archivia la traiettoria grezza; .replace() sovrascrive senza errori
    # se un file con lo stesso nome esiste già da un run precedente
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
    '''
    # --- 25 configurazioni ML-Agent, scena WZ1 ---
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
    # --- 1 baseline NavMesh puro, scena WZ1_NV (deterministico, un solo run) ---
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

    # --- Salva tutto in un unico CSV, separato da steering_test.xlsx ---
    df = pd.DataFrame(risultati)
    df.to_csv(OUTPUT_DIR / "confronto_distanza_tempo.csv", index=False)
    print(f"\nCompletato. Risultati salvati in: {OUTPUT_DIR / 'confronto_distanza_tempo.csv'}")
    print(f"Immagini di traiettoria in: {TRAJ_IMG_DIR}")
    print("fill_excel.py NON è stato chiamato — steering_test.xlsx non è stato toccato.")


if __name__ == "__main__":
    main()