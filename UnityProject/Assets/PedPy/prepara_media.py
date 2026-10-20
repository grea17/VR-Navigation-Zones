"""
prepara_media.py

Prepara le cartelle img/ e video/ del questionario:
  - rinomina i file con nomi neutri (chi risponde non deve leggere "navmesh" o
    "mlagent" nell'indirizzo di un'immagine o di un video);
  - riduce le immagini a 1600 px e le salva in JPG (da qualche MB a poche centinaia di KB);
  - ritaglia i video sulla stanza (via le bande nere e i pannelli di Unity ai lati)
    e li comprime a massimo 720p, senza audio, ottimizzati per il web (serve ffmpeg).

Nomi attesi in ingresso (quelli prodotti dagli script esistenti e dal taglio dei video):
  immagini Parte 1:  navmesh_baseline_vs_mlagent_01.png   -> img/p1_c01.jpg
  immagini Parte 2:  mlagent_06_vs_mlagent_10.png         -> img/p2_c06_c10.jpg
  video riferimento: NavMeshAgent.mp4                     -> video/ref.mp4
  video agenti:      ML_Agent1.mp4 ... ML_Agent24.mp4     -> video/c01.mp4 ... video/c24.mp4
Per i video vanno bene anche varianti come ML_Agent01, mlagent_1 o navmesh,
e i formati .mp4, .mov, .mkv, .avi, .webm (vengono convertiti in .mp4).

Uso, dalla cartella Assets\\PedPy (dove c'è confronto_dist_tempo):
  python prepara_media.py --immagini "confronto_dist_tempo\\traiettorie_sovrapposte" --video "<cartella dei video>" --uscita "<cartella del questionario>"

  --immagini  cartella con le immagini dei percorsi generate da genera_overlay_coppie.py
  --video     cartella con i video tagliati (NavMeshAgent, ML_Agent1 ... ML_Agent24)
  --uscita    cartella dove c'è index.html: lì vengono create img\\ e video\\
I percorsi relativi partono dalla cartella in cui lanci il comando; in caso di dubbio
usa percorsi completi (C:\\...) tra virgolette.
"""

import argparse
import re
import shutil
import subprocess
from pathlib import Path

from PIL import Image

LARGHEZZA_MAX = 1600
QUALITA_JPG = 82
ALTEZZA_MAX_VIDEO = 720
# Ritaglio dei video sulla stanza, in frazioni del fotogramma (x, y, larghezza, altezza).
# Misurato sui video registrati dalla vista dall'alto: tiene tutta la stanza con un piccolo margine.
# Metti None per non ritagliare.
RITAGLIO = (0.2266, 0.0347, 0.5273, 0.9444)

# Deve corrispondere a CONFIG in index.html
FASE1 = list(range(1, 25))   # 24 configurazioni
# Parte 2: le 8 coppie uguali per tutti
FASE2 = [(6, 10), (5, 6), (23, 24), (2, 7), (22, 23), (19, 24), (8, 10), (1, 7)]

RE_IMG_P1 = re.compile(r"navmesh_baseline_vs_mlagent_(\d+)", re.I)
RE_IMG_P2 = re.compile(r"mlagent_(\d+)_vs_mlagent_(\d+)", re.I)
RE_VID_AGENTE = re.compile(r"ml[_\- ]?agent[_\- ]?(\d+)", re.I)        # ML_Agent1, ML_Agent01, mlagent_1
RE_VID_REF = re.compile(r"nav[_\- ]?mesh(?:[_\- ]?agent)?", re.I)       # NavMeshAgent, navmesh
ESTENSIONI_VIDEO = {".mp4", ".mov", ".mkv", ".avi", ".webm"}


def nome_immagine(stem):
    m = RE_IMG_P1.fullmatch(stem)
    if m:
        return f"p1_c{int(m.group(1)):02d}.jpg"
    m = RE_IMG_P2.fullmatch(stem)
    if m:
        return f"p2_c{int(m.group(1)):02d}_c{int(m.group(2)):02d}.jpg"
    return None


def nome_video(stem):
    m = RE_VID_AGENTE.fullmatch(stem)
    if m:
        return f"c{int(m.group(1)):02d}.mp4"
    if RE_VID_REF.fullmatch(stem):
        return "ref.mp4"
    return None


def cartella_ok(src: Path, cosa: str) -> bool:
    """Controlla che la cartella esista e lo dice chiaramente se non è così."""
    if src.is_dir():
        print(f"  cartella: {src.resolve()}")
        return True
    print(f"  ERRORE: la cartella {cosa} non esiste: {src.resolve()}")
    print("  Controlla il percorso: se è relativo, parte dalla cartella in cui lanci il comando.")
    return False


def prepara_immagini(src: Path, dst: Path):
    if not cartella_ok(src, "delle immagini"):
        return
    dst.mkdir(parents=True, exist_ok=True)
    file = sorted(src.glob("*.png")) + sorted(src.glob("*.jpg"))
    if not file:
        print("  ATTENZIONE: nessuna immagine .png o .jpg in questa cartella.")
    for f in file:
        nuovo = nome_immagine(f.stem)
        if not nuovo:
            print(f"  ignorata (nome non riconosciuto): {f.name}")
            continue
        img = Image.open(f).convert("RGB")
        if img.width > LARGHEZZA_MAX:
            altezza = round(img.height * LARGHEZZA_MAX / img.width)
            img = img.resize((LARGHEZZA_MAX, altezza), Image.LANCZOS)
        out = dst / nuovo
        img.save(out, "JPEG", quality=QUALITA_JPG, optimize=True, progressive=True)
        print(f"  {f.name} -> img/{nuovo} ({out.stat().st_size // 1024} KB)")


def prepara_video(src: Path, dst: Path):
    if not cartella_ok(src, "dei video"):
        return
    dst.mkdir(parents=True, exist_ok=True)
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        print("  ffmpeg non trovato: i video vengono solo copiati e rinominati, senza ritaglio né compressione.")
        print("  Per installarlo: winget install --id Gyan.FFmpeg -e  (poi riapri il terminale)")
    file = sorted(p for p in src.iterdir() if p.suffix.lower() in ESTENSIONI_VIDEO)
    if not file:
        print(f"  ATTENZIONE: nessun video ({', '.join(sorted(ESTENSIONI_VIDEO))}) in questa cartella.")
    for f in file:
        nuovo = nome_video(f.stem)
        if not nuovo:
            print(f"  ignorato (nome non riconosciuto): {f.name}")
            continue
        out = dst / nuovo
        if ffmpeg:
            filtri = f"scale=-2:'min({ALTEZZA_MAX_VIDEO},ih)'"
            if RITAGLIO:
                x, y, w, h = RITAGLIO
                filtri = (f"crop=trunc(iw*{w}/2)*2:trunc(ih*{h}/2)*2:trunc(iw*{x}):trunc(ih*{y}),"
                          + filtri)
            cmd = [
                ffmpeg, "-y", "-loglevel", "error", "-i", str(f),
                "-vf", filtri,
                "-c:v", "libx264", "-preset", "slow", "-crf", "28",
                "-pix_fmt", "yuv420p", "-an", "-movflags", "+faststart",
                str(out),
            ]
            subprocess.run(cmd, check=True)
        else:
            if f.suffix.lower() != ".mp4":
                print(f"  ATTENZIONE: {f.name} non è un .mp4 e senza ffmpeg non può essere convertito")
            shutil.copy2(f, out)
        print(f"  {f.name} -> video/{nuovo} ({out.stat().st_size // 1024} KB)")


def controlla(dst_img: Path, dst_vid: Path):
    gruppi = {
        "immagini della Parte 1": [dst_img / f"p1_c{n:02d}.jpg" for n in FASE1],
        "immagini della Parte 2": [dst_img / f"p2_c{a:02d}_c{b:02d}.jpg" for a, b in FASE2],
        "video": [dst_vid / "ref.mp4"] + [dst_vid / f"c{n:02d}.mp4" for n in FASE1],
    }
    tutti = [p for file in gruppi.values() for p in file]
    pronti = [p for p in tutti if p.exists()]
    totale_mb = sum(p.stat().st_size for p in pronti) / 1_048_576
    print(f"\nFile pronti: {len(pronti)}/{len(tutti)}, totale {totale_mb:.0f} MB")
    for nome, file in gruppi.items():
        mancanti = [p.name for p in file if not p.exists()]
        if mancanti:
            print(f"  MANCANO {len(mancanti)} {nome}: {', '.join(mancanti)}")
    for p in pronti:
        mb = p.stat().st_size / 1_048_576
        if mb > 25:
            print(f"  TROPPO GRANDE per GitHub: {p.parent.name}/{p.name} ({mb:.0f} MB, il limite è 25 MB)")


def main():
    ap = argparse.ArgumentParser(description="Prepara immagini e video del questionario")
    ap.add_argument("--immagini", type=Path, help="cartella con le immagini dei percorsi (png)")
    ap.add_argument("--video", type=Path, help="cartella con i video tagliati (mp4)")
    ap.add_argument("--uscita", type=Path, default=Path("."), help="cartella del questionario (default: corrente)")
    args = ap.parse_args()

    dst_img, dst_vid = args.uscita / "img", args.uscita / "video"
    print(f"Uscita: {args.uscita.resolve()}")
    if not (args.uscita / "index.html").exists():
        print("  Nota: qui non c'è index.html. Alla fine img\\ e video\\ vanno spostate accanto a index.html.")
    if args.immagini:
        print("Immagini:")
        prepara_immagini(args.immagini, dst_img)
    if args.video:
        print("Video:")
        prepara_video(args.video, dst_vid)
    controlla(dst_img, dst_vid)


if __name__ == "__main__":
    main()
