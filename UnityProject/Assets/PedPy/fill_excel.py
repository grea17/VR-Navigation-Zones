''' VERSIONE OLD
import pandas as pd
from openpyxl import load_workbook
from openpyxl.styles import PatternFill, Font, Alignment

TXT_PATH   = "ZoneStatsArtifact.txt"
EXCEL_PATH = "steering_test.xlsx"

df = pd.read_csv(TXT_PATH, sep='\t')
df.columns = [c.strip() for c in df.columns]

wb = load_workbook(EXCEL_PATH)
ws = wb["Test Completo"]

def get_float(val):
    return float(str(val).replace(',', '.'))

def build_headers(ws):
    headers = {}
    for c in range(1, ws.max_column + 1):
        val = ws.cell(1, c).value
        if val is not None:
            headers[str(val)] = c
    return headers

#headers = build_headers(ws)
#print("Colonne Excel trovate:", list(headers.keys()))
#print("Colonne TXT:", list(df.columns))
#print("Prime righe TXT:\n", df.head(3))

BASE_METRIC_COLS = [
    ("tempoWZ",    "Tempo WZ\n(s)"),
    ("navMesh",    "Attiv.\nNavMesh\n(0/1)"),
    ("distanzaWZ", "distanza wz"),
    ("distanzaTZ", "distanza tz"),
    ("tempoTZ",    "tempo tz"),
    ("tempoTot",   "tempo tot"),
]

def get_col_names(occurrence):
    if occurrence == 0:
        return [(csv, excel) for csv, excel in BASE_METRIC_COLS]
    else:
        suffix = f" ({occurrence + 1})"
        return [(csv, excel + suffix) for csv, excel in BASE_METRIC_COLS]

def ensure_columns(ws, headers, occurrence):
    cols = get_col_names(occurrence)
    for csv_col, excel_col in cols:
        if excel_col not in headers:
            new_col = ws.max_column + 1
            ws.cell(1, new_col).value = excel_col
            ws.cell(1, new_col).fill = PatternFill(
                start_color="1F4E79", end_color="1F4E79", fill_type="solid")
            ws.cell(1, new_col).font = Font(
                bold=True, color="FFFFFF", name="Arial", size=10)
            ws.cell(1, new_col).alignment = Alignment(
                horizontal="center", vertical="center", wrap_text=True)
            headers[excel_col] = new_col
    return headers

def write_metrics(ws, headers, row, occurrence, data_row):
    cols = get_col_names(occurrence)
    for csv_col, excel_col in cols:
        if excel_col in headers:
            try:
                ws.cell(row, headers[excel_col]).value = round(get_float(data_row[csv_col]), 2)
            except Exception as e:
                print(f"  Errore {csv_col}: {e}")

headers = build_headers(ws)
filled = 0

for row in range(2, ws.max_row + 1):
    config_num = ws.cell(row, headers["Configurazione"]).value
    #print(f"Riga {row}: config_num Excel = {repr(config_num)}, tipo = {type(config_num)}")
    
    if config_num is None:
        print("  → config_num è None, skip")
        continue

    mask = (df["configNumber"].astype(int) == int(config_num))
    #print(f"  → Match trovati: {mask.sum()}")
    
    matches = df[mask]
    #matches_filtered = matches[
        #matches["tempoWZ"].astype(str).str.replace(',', '.').astype(float) > 0]
    #print(f"  → Match dopo filtro tempoWZ>0: {len(matches_filtered)}")

    if matches.empty:
        continue

    #print(f"Config {config_num}: {len(matches)} occorrenza/e")

    for i, (_, data_row) in enumerate(matches.iterrows()):
        headers = ensure_columns(ws, headers, i)
        write_metrics(ws, headers, row, i, data_row)

    filled += 1

wb.save(EXCEL_PATH)
print(f"\nCompletato: {filled} righe compilate su {ws.max_row - 1} totali.")
'''
'''
#VERSIONE AGGIORNATA
import pandas as pd
from openpyxl import load_workbook
from openpyxl.styles import PatternFill, Font, Alignment

TXT_PATH        = "ZoneStatsArtifact.txt"
DETAIL_TXT_PATH = "ArtifactStatsDetail.txt"
EXCEL_PATH      = "steering_test.xlsx"

# ── Caricamento file TSV ──────────────────────────────────────────
df = pd.read_csv(TXT_PATH, sep='\t')
df.columns = [c.strip() for c in df.columns]

df_detail = pd.read_csv(DETAIL_TXT_PATH, sep='\t')
df_detail.columns = [c.strip() for c in df_detail.columns]

wb = load_workbook(EXCEL_PATH)
ws = wb["Test Completo"]

# ── Helper ────────────────────────────────────────────────────────
def get_float(val):
    return float(str(val).replace(',', '.'))

def build_headers(ws):
    headers = {}
    for c in range(1, ws.max_column + 1):
        val = ws.cell(1, c).value
        if val is not None:
            headers[str(val)] = c
    return headers

def make_header_cell(ws, col, label):
    """Formatta una cella di intestazione con lo stile standard."""
    ws.cell(1, col).value = label
    ws.cell(1, col).fill = PatternFill(
        start_color="1F4E79", end_color="1F4E79", fill_type="solid")
    ws.cell(1, col).font = Font(
        bold=True, color="FFFFFF", name="Arial", size=10)
    ws.cell(1, col).alignment = Alignment(
        horizontal="center", vertical="center", wrap_text=True)

# ── Colonne metriche aggregate ────────────────────────────────────
BASE_METRIC_COLS = [
    ("tempoWZ",    "Tempo WZ\n(s)"),
    ("navMesh",    "Attiv.\nNavMesh\n(0/1)"),
    ("distanzaWZ", "distanza wz"),
    ("distanzaTZ", "distanza tz"),
    ("tempoTZ",    "tempo tz"),
    ("tempoTot",   "tempo tot"),
]

# ── Colonne metriche per artefatto ────────────────────────────────
# (nome_colonna_TSV, suffisso_excel)
ARTIFACT_NAMES = ["Totem4", "Monitor"]

ARTIFACT_METRIC_COLS = [
    ("tempoWZ", "TempoWZ"),
    ("tempoTZ", "TempoTZ"),
    ("distWZ",  "DistWZ"),
    ("distTZ",  "DistTZ"),
    ("navMesh", "NavMesh"),
]

def get_artifact_col_names(artifact_name, occurrence):
    """
    Restituisce le coppie (campo_tsv, nome_colonna_excel) per un artefatto
    e una data occorrenza. occurrence=0 → nessun suffisso, occurrence=1 → ' (2)', ecc.
    """
    suffix = "" if occurrence == 0 else f" ({occurrence + 1})"
    return [
        (tsv_col, f"{artifact_name}_{excel_label}{suffix}")
        for tsv_col, excel_label in ARTIFACT_METRIC_COLS
    ]

def get_aggregate_col_names(occurrence):
    """
    Restituisce le coppie (campo_tsv, nome_colonna_excel) per le metriche aggregate
    e una data occorrenza.
    """
    suffix = "" if occurrence == 0 else f" ({occurrence + 1})"
    return [(tsv_col, excel_label + suffix) for tsv_col, excel_label in BASE_METRIC_COLS]

def ensure_columns(ws, headers, occurrence):
    """
    Crea le colonne Excel per una data occorrenza se non esistono ancora:
    prima le 6 aggregate, poi le 5 per ogni artefatto.
    """
    # Colonne aggregate
    for _, excel_col in get_aggregate_col_names(occurrence):
        if excel_col not in headers:
            new_col = ws.max_column + 1
            make_header_cell(ws, new_col, excel_col)
            headers[excel_col] = new_col

    # Colonne per artefatto
    for artifact_name in ARTIFACT_NAMES:
        for _, excel_col in get_artifact_col_names(artifact_name, occurrence):
            if excel_col not in headers:
                new_col = ws.max_column + 1
                make_header_cell(ws, new_col, excel_col)
                headers[excel_col] = new_col

    return headers

def write_aggregate_metrics(ws, headers, row, occurrence, data_row):
    """Scrive le 6 metriche aggregate per una data occorrenza."""
    for tsv_col, excel_col in get_aggregate_col_names(occurrence):
        if excel_col in headers:
            try:
                ws.cell(row, headers[excel_col]).value = round(get_float(data_row[tsv_col]), 2)
            except Exception as e:
                print(f"  Errore aggregato {tsv_col}: {e}")

def write_artifact_metrics(ws, headers, row, occurrence, artifact_name, artifact_row):
    """
    Scrive le 5 metriche di un artefatto specifico per una data occorrenza.
    Se artifact_row è None scrive 0 per tutte le metriche (artefatto non raggiunto).
    """
    for tsv_col, excel_col in get_artifact_col_names(artifact_name, occurrence):
        if excel_col in headers:
            try:
                val = round(get_float(artifact_row[tsv_col]), 2) if artifact_row is not None else 0
                ws.cell(row, headers[excel_col]).value = val
            except Exception as e:
                print(f"  Errore artefatto {artifact_name} {tsv_col}: {e}")

# ── Loop principale ───────────────────────────────────────────────
headers = build_headers(ws)
filled  = 0

for row in range(2, ws.max_row + 1):
    config_num = ws.cell(row, headers["Configurazione"]).value

    if config_num is None:
        continue

    config_num = int(config_num)

    # Righe aggregate per questa configurazione
    agg_matches = df[df["configNumber"].astype(int) == config_num]

    # Righe dettaglio per questa configurazione
    det_matches = df_detail[df_detail["configNumber"].astype(int) == config_num]

    if agg_matches.empty:
        continue

    # Itera sulle occorrenze aggregate (una per run/agente multiplo)
    for i, (_, agg_row) in enumerate(agg_matches.iterrows()):
        headers = ensure_columns(ws, headers, i)

        # Scrivi le 6 metriche aggregate
        write_aggregate_metrics(ws, headers, row, i, agg_row)

        # Per ogni artefatto, trova la riga di dettaglio corrispondente
        # all'agente e all'occorrenza i (stesso agente della riga aggregata)
        agente = str(agg_row["agente"]).strip()

        # Righe dettaglio per questo agente specifico in questa configurazione
        det_agente = det_matches[det_matches["agente"].astype(str).str.strip() == agente]

        for artifact_name in ARTIFACT_NAMES:
            # Cerca la riga di dettaglio per questo artefatto
            det_artifact = det_agente[
                det_agente["artifactName"].astype(str).str.strip() == artifact_name
            ]

            if det_artifact.empty:
                # Artefatto non raggiunto → scrivi tutto 0
                write_artifact_metrics(ws, headers, row, i, artifact_name, None)
            else:
                # Prendi la prima riga (dovrebbe essere unica per agente+config+artifact)
                write_artifact_metrics(ws, headers, row, i, artifact_name, det_artifact.iloc[0])

    filled += 1

wb.save(EXCEL_PATH)
print(f"\nCompletato: {filled} righe compilate su {ws.max_row - 1} totali.")
'''

import os
import pandas as pd
from openpyxl import load_workbook
from openpyxl.styles import PatternFill, Font, Alignment

TXT_PATH        = "ZoneStatsArtifact.txt"
DETAIL_TXT_PATH = "ArtifactStatsDetail.txt"
#EXCEL_PATH      = "steering_test.xlsx"
EXCEL_PATH      = "test_integrativo.xlsx"

# ── Caricamento file TSV ──────────────────────────────────────────
df = pd.read_csv(TXT_PATH, sep='\t')
df.columns = [c.strip() for c in df.columns]

if os.path.exists(DETAIL_TXT_PATH):
    colonne_dettaglio = [
        "configNumber", 
        "agente", 
        "artifactName", 
        "tempoWZ", 
        "tempoTZ", 
        "distWZ", 
        "distTZ", 
        "navMesh"
    ]
    df_detail = pd.read_csv(DETAIL_TXT_PATH, sep='\t', header=None, names=colonne_dettaglio)
    df_detail.columns = [c.strip() for c in df_detail.columns]
    # Nomi artefatti letti dinamicamente dal TSV, ordine alfabetico per coerenza
    ARTIFACT_NAMES = sorted(df_detail["artifactName"].astype(str).unique().tolist())
    print(f"Artefatti trovati: {ARTIFACT_NAMES}")
else:
    print(f"⚠️  {DETAIL_TXT_PATH} non trovato — colonne artefatto saltate")
    df_detail = None
    ARTIFACT_NAMES = []

wb = load_workbook(EXCEL_PATH)
ws = wb["Test Completo"]

# ── Helper ────────────────────────────────────────────────────────
def get_float(val):
    return float(str(val).replace(',', '.'))

def build_headers(ws):
    headers = {}
    for c in range(1, ws.max_column + 1):
        val = ws.cell(1, c).value
        if val is not None:
            headers[str(val)] = c
    return headers

def make_header_cell(ws, col, label):
    """Formatta una cella di intestazione con lo stile standard."""
    ws.cell(1, col).value = label
    ws.cell(1, col).fill = PatternFill(
        start_color="1F4E79", end_color="1F4E79", fill_type="solid")
    ws.cell(1, col).font = Font(
        bold=True, color="FFFFFF", name="Arial", size=10)
    ws.cell(1, col).alignment = Alignment(
        horizontal="center", vertical="center", wrap_text=True)

# ── Colonne metriche aggregate ────────────────────────────────────
BASE_METRIC_COLS = [
    ("tempoWZ",    "TempoWZ"),
    ("navMesh",    "NavMesh"),
    ("distanzaWZ", "DistWZ"),
    ("distanzaTZ", "DistTZ"),
    ("tempoTZ",    "TempoTZ"),
    ("tempoTot",   "TempoTot"),
]

# ── Colonne metriche per artefatto ────────────────────────────────
ARTIFACT_METRIC_COLS = [
    ("tempoWZ", "TempoWZ"),
    ("tempoTZ", "TempoTZ"),
    ("distWZ",  "DistWZ"),
    ("distTZ",  "DistTZ"),
    ("navMesh", "NavMesh"),
]

def get_aggregate_col_names(agent_name, occurrence):
    """
    Restituisce le coppie (campo_tsv, nome_colonna_excel) per le metriche aggregate.
    Il nome agente viene usato come prefisso.
    occurrence=0 → nessun suffisso, occurrence=1 → ' (2)', ecc.
    """
    suffix = "" if occurrence == 0 else f" ({occurrence + 1})"
    return [
        (tsv_col, f"{agent_name}_{excel_label}{suffix}")
        for tsv_col, excel_label in BASE_METRIC_COLS
    ]

def get_artifact_col_names(agent_name, artifact_name, occurrence):
    """
    Restituisce le coppie (campo_tsv, nome_colonna_excel) per un artefatto.
    Formato: AgentName_ArtifactName_Metrica (suffisso occorrenza se > 0).
    """
    suffix = "" if occurrence == 0 else f" ({occurrence + 1})"
    return [
        (tsv_col, f"{agent_name}_{artifact_name}_{excel_label}{suffix}")
        for tsv_col, excel_label in ARTIFACT_METRIC_COLS
    ]

def ensure_columns(ws, headers, agent_name, occurrence):
    """
    Crea le colonne Excel per una data occorrenza se non esistono ancora:
    prima le 6 aggregate con prefisso agente, poi le 5 per ogni artefatto.
    """
    # Colonne aggregate con prefisso agente
    for _, excel_col in get_aggregate_col_names(agent_name, occurrence):
        if excel_col not in headers:
            new_col = ws.max_column + 1
            make_header_cell(ws, new_col, excel_col)
            headers[excel_col] = new_col

    # Colonne per artefatto con prefisso agente
    for artifact_name in ARTIFACT_NAMES:
        for _, excel_col in get_artifact_col_names(agent_name, artifact_name, occurrence):
            if excel_col not in headers:
                new_col = ws.max_column + 1
                make_header_cell(ws, new_col, excel_col)
                headers[excel_col] = new_col

    return headers

def write_aggregate_metrics(ws, headers, row, agent_name, occurrence, data_row):
    """Scrive le 6 metriche aggregate per una data occorrenza."""
    for tsv_col, excel_col in get_aggregate_col_names(agent_name, occurrence):
        if excel_col in headers:
            try:
                ws.cell(row, headers[excel_col]).value = round(get_float(data_row[tsv_col]), 2)
            except Exception as e:
                print(f"  Errore aggregato {tsv_col}: {e}")

def write_artifact_metrics(ws, headers, row, agent_name, occurrence, artifact_name, artifact_row):
    """
    Scrive le 5 metriche di un artefatto specifico per una data occorrenza.
    Se artifact_row è None scrive 0 per tutte le metriche (artefatto non raggiunto).
    """
    for tsv_col, excel_col in get_artifact_col_names(agent_name, artifact_name, occurrence):
        if excel_col in headers:
            try:
                val = round(get_float(artifact_row[tsv_col]), 2) if artifact_row is not None else 0
                ws.cell(row, headers[excel_col]).value = val
            except Exception as e:
                print(f"  Errore artefatto {artifact_name} {tsv_col}: {e}")

# ── Loop principale ───────────────────────────────────────────────
headers = build_headers(ws)
filled  = 0

for row in range(2, ws.max_row + 1):
    config_num = ws.cell(row, headers["Configurazione"]).value

    if config_num is None:
        continue

    config_num = int(config_num)

    # Righe aggregate per questa configurazione
    agg_matches = df[df["configNumber"].astype(int) == config_num]

    if agg_matches.empty:
        continue

    # Raggruppa le occorrenze per agente — ogni agente ha il suo blocco di colonne
    # Se stesso agente appare più volte (riesecuzioni), usa suffisso (2), (3)...
    agent_occurrence = {}  # agente → contatore occorrenze

    for _, agg_row in agg_matches.iterrows():
        agente = str(agg_row["agente"]).strip()

        # Determina l'occorrenza per questo agente
        if agente not in agent_occurrence:
            agent_occurrence[agente] = 0
        else:
            agent_occurrence[agente] += 1
        occ = agent_occurrence[agente]

        # Crea colonne se necessario (con prefisso agente)
        headers = ensure_columns(ws, headers, agente, occ)

        # Scrivi le 6 metriche aggregate
        write_aggregate_metrics(ws, headers, row, agente, occ, agg_row)

        # Colonne artefatto
        if df_detail is not None:
            det_matches = df_detail[df_detail["configNumber"].astype(int) == config_num]
            det_agente = det_matches[det_matches["agente"].astype(str).str.strip() == agente]

            for artifact_name in ARTIFACT_NAMES:
                det_artifact = det_agente[
                    det_agente["artifactName"].astype(str).str.strip() == artifact_name
                ]
                
                # Se l'artefatto non c'è nel file txt per questa riga, 
                # non facciamo nulla per non sovrascrivere l'Excel con degli 0.
                if det_artifact.empty:
                    pass 
                else:
                    write_artifact_metrics(ws, headers, row, agente, occ, artifact_name, det_artifact.iloc[0])

    filled += 1

wb.save(EXCEL_PATH)
print(f"\nCompletato: {filled} righe compilate su {ws.max_row - 1} totali.")