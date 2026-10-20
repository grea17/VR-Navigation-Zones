"""
genera_overlay_coppie.py

Genera le immagini dei percorsi del questionario: due traiettorie sovrapposte
sulla mappa, a partire dai file confronto_dist_tempo/trajectory_*.txt.

  Parte 1: navmesh_baseline (linea blu) contro ognuna delle 24 configurazioni (linea rossa)
           -> navmesh_baseline_vs_mlagent_XX.png
  Parte 2: le 8 coppie (prima = linea verde, seconda = linea arancione)
           -> mlagent_AA_vs_mlagent_BB.png

Le legende usano solo i nomi neutri (Agente Blu, Rosso, Verde, Arancione):
chi risponde non deve leggere "navmesh" o "mlagent".

Uso, dalla cartella Assets\\PedPy:
  python genera_overlay_coppie.py            (tutte e due le parti)
  python genera_overlay_coppie.py --fase 2   (solo la Parte 2)
  python genera_overlay_coppie.py --fase 1   (solo la Parte 1)
  python genera_overlay_coppie.py --config 14 22 24
                                             (solo le immagini che contengono queste configurazioni)
Le immagini finiscono in confronto_dist_tempo\\traiettorie_sovrapposte.
"""

import argparse
import os
import pathlib

import pandas as pd
import pedpy
from pedPy import preprocess_file, plot_trajectories_with_zones

OUTPUT_DIR = pathlib.Path("confronto_dist_tempo")
OVERLAY_DIR = OUTPUT_DIR / "traiettorie_sovrapposte"

# Stessi colori del questionario (index.html, AGENTI)
BLU = "#1f5fd1"
ROSSO = "#d62728"
VERDE = "#1d9e75"
ARANCIONE = "#d4915b"

# Se le nuove immagini risultano diverse da quelle della Parte 1 già pronte, cambia questo valore
DISEGNA_BLUEPRINT = False

# Deve corrispondere a CONFIG in index.html e a prepara_media.py
FASE1 = list(range(1, 25))   # 24 configurazioni
FASE2 = [(6, 10), (5, 6), (23, 24), (2, 7), (22, 23), (19, 24), (8, 10), (1, 7)]


def carica_traiettoria(config_id: str, nuovo_id: str):
    """Carica trajectory_{config_id}.txt e assegna a tutte le righe l'id `nuovo_id`."""
    raw_path = OUTPUT_DIR / f"trajectory_{config_id}.txt"
    if not raw_path.exists():
        print(f"  ATTENZIONE: {raw_path} non trovato")
        return None

    clean_path = f"_tmp_clean_{config_id}.txt"
    if not preprocess_file(str(raw_path), clean_path):
        print(f"  ATTENZIONE: impossibile pulire {raw_path}")
        return None

    traj = pedpy.load_trajectory(trajectory_file=pathlib.Path(clean_path),
                                 default_frame_rate=100, default_unit=1)
    df = traj.data.copy()
    df["id"] = nuovo_id
    try:
        os.remove(clean_path)
    except OSError:
        pass
    return df, traj.frame_rate


def disegna_coppia(id_a: str, nome_a: str, colore_a: str,
                   id_b: str, nome_b: str, colore_b: str):
    """Sovrappone le traiettorie di id_a e id_b e salva {id_a}_vs_{id_b}.png."""
    nome_file = f"{id_a}_vs_{id_b}.png"
    ra = carica_traiettoria(id_a, "A")
    rb = carica_traiettoria(id_b, "B")
    if ra is None or rb is None:
        print(f"  Salto {nome_file}: dati mancanti")
        return

    df_a, frame_rate = ra
    df_b, _ = rb
    combinato = pedpy.TrajectoryData(data=pd.concat([df_a, df_b], ignore_index=True),
                                     frame_rate=frame_rate)

    plot_trajectories_with_zones(
        traj=combinato,
        agent_colors={"A": colore_a, "B": colore_b},
        blueprint_path="SceneBlueprint.json",
        background_img="BackgroundMap.png",
        save_path=str(OVERLAY_DIR / nome_file),
        disegna_blueprint=DISEGNA_BLUEPRINT,
        agent_names={"A": nome_a, "B": nome_b},
    )
    print(f"  {nome_file}")


def main():
    ap = argparse.ArgumentParser(description="Immagini dei percorsi per il questionario")
    ap.add_argument("--fase", type=int, choices=[1, 2], help="genera solo la Parte 1 o solo la Parte 2")
    ap.add_argument("--config", type=int, nargs="+",
                    help="genera solo le immagini che contengono queste configurazioni (es. 14 22 24)")
    args = ap.parse_args()
    fase1 = [n for n in FASE1 if not args.config or n in args.config]
    fase2 = [(a, b) for a, b in FASE2 if not args.config or a in args.config or b in args.config]
    OVERLAY_DIR.mkdir(parents=True, exist_ok=True)
    print(f"Cartella: {OVERLAY_DIR.resolve()}")

    if args.fase in (None, 1):
        print(f"Parte 1 ({len(fase1)} immagini):")
        for n in fase1:
            disegna_coppia("navmesh_baseline", "Agente Blu", BLU,
                           f"mlagent_{n:02d}", "Agente Rosso", ROSSO)

    if args.fase in (None, 2):
        print(f"Parte 2 ({len(fase2)} immagini):")
        for a, b in fase2:
            disegna_coppia(f"mlagent_{a:02d}", "Agente Verde", VERDE,
                           f"mlagent_{b:02d}", "Agente Arancione", ARANCIONE)


if __name__ == "__main__":
    main()
