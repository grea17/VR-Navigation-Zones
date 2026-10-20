"""
validate_top_configs.py

Esegue N repliche extra sulle configurazioni A (Efficienza) e B (Naturalezza)
selezionate dall'analisi statistica, per il piano di validazione descritto
in Doppia_Analisi_Efficienza_vs_Naturalezza.md (rischio di selection bias
con n=3 nel disegno fattoriale originale).

NON modifica set_config.py, ConfigLoader.cs, né alcun altro script Unity/C#.
Scrive config.json nello stesso identico formato di set_config.py (incluso
il calcolo di sceneName da posObiettivo). Usa lo stesso meccanismo di
sincronizzazione di master_run.py (file flag start_play.txt / sim_done.txt),
non un'attesa a tempo fisso.

NON chiama fill_excel.py: i risultati vanno in un file separato
(validation_A_B_results.txt), per non mescolare repliche extra (n=20) con
il disegno fattoriale originale (n=3 uniforme per tutte le altre
combinazioni), che romperebbe la validità dei test statistici già fatti
nel notebook (Kruskal-Wallis, Dunn, ranking per effect size).

PRIMA DI LANCIARE 40 REPLICHE: fai un test con N_REPLICHE=1, apri
config.json dopo la scrittura e verifica che la scena cambi visibilmente
in Unity — solo allora rimetti N_REPLICHE=20.
"""

import os
import json
import time
from pathlib import Path
from datetime import datetime

# ============================================================
# CONFIGURAZIONE
# ============================================================
ZONE_STATS_PATH = "ZoneStats.txt"                 # stesso file letto da fill_excel.py
ARTIFACT_STATS_PATH = "ArtifactStatsDetail.txt"    # stesso file letto da fill_excel.py
OUTPUT_VALIDATION = Path("validation_A_B_results.txt")  # NUOVO file, separato dal dataset principale

N_REPLICHE = 20

CONFIGURAZIONI = {
    "A_Efficienza": dict(angularSpeed=150, warningSpeedMultiplier=0.65,
                          wzRadius=5, tzRadius=1.25, posObiettivo="Dentro WZ"),
    "B_Naturalezza": dict(angularSpeed=90, warningSpeedMultiplier=0.50,
                           wzRadius=4, tzRadius=2.00, posObiettivo="Dentro WZ"),
}


def scrivi_config(cfg: dict, config_num: int, campione: int = 1):
    """
    Scrive config.json nello stesso identico formato di set_config.py,
    incluso il calcolo di sceneName da posObiettivo (WZ1/WZ2) e i campi
    configNumber/campione che ConfigLoader.cs si aspetta di trovare.
    """
    scene = "WZ1" if cfg["posObiettivo"].strip() == "Dentro WZ" else "WZ2"

    config = {
        "configNumber": config_num,
        "sceneName": scene,
        "angularSpeed": float(cfg["angularSpeed"]),
        "warningSpeedMultiplier": float(cfg["warningSpeedMultiplier"]),
        "wzRadius": float(cfg["wzRadius"]),
        "tzRadius": float(cfg["tzRadius"]),
        "posObiettivo": cfg["posObiettivo"],
        "campione": campione,
    }

    with open("config.json", "w") as f:
        json.dump(config, f, indent=2)

    print(f"    Config scritta → scena: {scene}, angularSpeed: {config['angularSpeed']}, "
          f"warningSpeedMultiplier: {config['warningSpeedMultiplier']}, "
          f"wzRadius: {config['wzRadius']}, tzRadius: {config['tzRadius']}, "
          f"campione: {config['campione']}")


def lancia_episodio_e_attendi():
    """
    Stesso meccanismo di master_run.py: crea start_play.txt per far partire
    l'AutoPlayManager in Unity, poi aspetta che Unity crei sim_done.txt.
    Nessun timer fisso: l'attesa dura esattamente quanto dura l'episodio.
    Presuppone che Unity sia già aperto sulla scena, come quando lanci
    master_run.py.
    """
    with open("start_play.txt", "w") as f:
        f.write("go")

    print("    In attesa che la simulazione finisca...")
    while not os.path.exists("sim_done.txt"):
        time.sleep(0.5)

    os.remove("sim_done.txt")


def leggi_svuota_e_accoda(nome_config: str, replica: int):
    """
    Legge ZoneStats.txt e ArtifactStatsDetail.txt (scritti da Unity, non
    toccati altrimenti), li copia con etichetta nel file di validazione,
    poi li svuota (non li cancella) per evitare che un futuro rilancio di
    fill_excel.py li trovi ancora popolati e li aggiunga per errore al
    dataset fattoriale principale.
    """
    timestamp = datetime.now().isoformat(timespec='seconds')

    with open(OUTPUT_VALIDATION, "a", encoding="utf-8") as out:
        out.write(f"\n=== {nome_config} | replica {replica} | {timestamp} ===\n")

        for path, label in [(ZONE_STATS_PATH, "ZoneStats"), (ARTIFACT_STATS_PATH, "ArtifactStatsDetail")]:
            p = Path(path)
            if p.exists():
                contenuto = p.read_text(encoding="utf-8")
                out.write(f"--- {label} ---\n{contenuto}\n")
                p.write_text("", encoding="utf-8")  # svuota, non cancella
            else:
                out.write(f"--- {label}: FILE NON TROVATO (episodio fallito?) ---\n")


def main():
    if OUTPUT_VALIDATION.exists():
        print(f"Attenzione: {OUTPUT_VALIDATION} esiste già, i nuovi risultati verranno accodati.")

    try:
        for idx, (nome_config, cfg) in enumerate(CONFIGURAZIONI.items(), start=1):
            print(f"\n{'='*50}")
            print(f"CONFIGURAZIONE: {nome_config} — {cfg}")
            print(f"{'='*50}")

            for replica in range(1, N_REPLICHE + 1):
                print(f"\n  Replica {replica}/{N_REPLICHE}...")
                # configNumber negativo per distinguerle chiaramente dalle 1-1296 originali
                scrivi_config(cfg, config_num=-idx, campione=replica)
                lancia_episodio_e_attendi()
                leggi_svuota_e_accoda(nome_config, replica)

    except KeyboardInterrupt:
        print("\n\nEsecuzione interrotta manualmente")

    finally:
        print(f"\nCompletato. Risultati salvati in: {OUTPUT_VALIDATION.resolve()}")
        print("NOTA: fill_excel.py NON è stato chiamato — i dati di validazione")
        print("sono separati dal dataset fattoriale principale, di proposito.")


if __name__ == "__main__":
    main()