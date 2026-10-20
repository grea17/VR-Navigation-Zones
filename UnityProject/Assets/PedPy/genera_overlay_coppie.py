"""
genera_overlay_coppie.py

Generates the path images of the questionnaire: two trajectories overlaid
on the map, from the files confronto_dist_tempo/trajectory_*.txt.

  Part 1: navmesh_baseline (blue line) against each of the 24 configurations (red line)
          -> navmesh_baseline_vs_mlagent_XX.png
  Part 2: the 8 pairs (first = green line, second = orange line)
          -> mlagent_AA_vs_mlagent_BB.png

The legends only use neutral names (Agente Blu, Rosso, Verde, Arancione),
so that respondents do not read "navmesh" or "mlagent".

Usage, from the Assets\\PedPy folder:
  python genera_overlay_coppie.py            (both parts)
  python genera_overlay_coppie.py --fase 2   (Part 2 only)
  python genera_overlay_coppie.py --fase 1   (Part 1 only)
  python genera_overlay_coppie.py --config 14 22 24
                                             (only the images that contain these configurations)
The images are saved in confronto_dist_tempo\\traiettorie_sovrapposte.
"""

import argparse
import os
import pathlib

import pandas as pd
import pedpy
from pedPy import preprocess_file, plot_trajectories_with_zones

OUTPUT_DIR = pathlib.Path("confronto_dist_tempo")
OVERLAY_DIR = OUTPUT_DIR / "traiettorie_sovrapposte"

# Same colors as the questionnaire (index.html, AGENTI)
BLU = "#1f5fd1"
ROSSO = "#d62728"
VERDE = "#1d9e75"
ARANCIONE = "#d4915b"

# Change this value if the new images differ from the existing Part 1 images
DISEGNA_BLUEPRINT = False

# Must match CONFIG in index.html and prepara_media.py
FASE1 = list(range(1, 25))   # 24 configurations
FASE2 = [(6, 10), (5, 6), (23, 24), (2, 7), (22, 23), (19, 24), (8, 10), (1, 7)]


def carica_traiettoria(config_id: str, nuovo_id: str):
    """Loads trajectory_{config_id}.txt and sets the id of every row to `nuovo_id`."""
    raw_path = OUTPUT_DIR / f"trajectory_{config_id}.txt"
    if not raw_path.exists():
        print(f"  WARNING: {raw_path} not found")
        return None

    clean_path = f"_tmp_clean_{config_id}.txt"
    if not preprocess_file(str(raw_path), clean_path):
        print(f"  WARNING: could not preprocess {raw_path}")
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
    """Overlays the trajectories of id_a and id_b and saves {id_a}_vs_{id_b}.png."""
    nome_file = f"{id_a}_vs_{id_b}.png"
    ra = carica_traiettoria(id_a, "A")
    rb = carica_traiettoria(id_b, "B")
    if ra is None or rb is None:
        print(f"  Skipping {nome_file}: missing data")
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
    ap = argparse.ArgumentParser(description="Path images for the questionnaire")
    ap.add_argument("--fase", type=int, choices=[1, 2], help="generate only Part 1 or only Part 2")
    ap.add_argument("--config", type=int, nargs="+",
                    help="generate only the images that contain these configurations (e.g. 14 22 24)")
    args = ap.parse_args()
    fase1 = [n for n in FASE1 if not args.config or n in args.config]
    fase2 = [(a, b) for a, b in FASE2 if not args.config or a in args.config or b in args.config]
    OVERLAY_DIR.mkdir(parents=True, exist_ok=True)
    print(f"Folder: {OVERLAY_DIR.resolve()}")

    if args.fase in (None, 1):
        print(f"Part 1 ({len(fase1)} images):")
        for n in fase1:
            disegna_coppia("navmesh_baseline", "Agente Blu", BLU,
                           f"mlagent_{n:02d}", "Agente Rosso", ROSSO)

    if args.fase in (None, 2):
        print(f"Part 2 ({len(fase2)} images):")
        for a, b in fase2:
            disegna_coppia(f"mlagent_{a:02d}", "Agente Verde", VERDE,
                           f"mlagent_{b:02d}", "Agente Arancione", ARANCIONE)


if __name__ == "__main__":
    main()
