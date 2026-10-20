"""
fill_excel.py
Copies the results written by Unity (ZoneStatsArtifact.txt and ArtifactStatsDetail.txt)
into the Excel grid, one row per configuration. The columns are created if missing:
<agent>_<metric> for the aggregate metrics and <agent>_<artifact>_<metric> per artifact.
Repeated runs of the same configuration get a suffix: " (2)", " (3)", ...
"""
import os
import pandas as pd
from openpyxl import load_workbook
from openpyxl.styles import PatternFill, Font, Alignment

TXT_PATH        = "ZoneStatsArtifact.txt"
DETAIL_TXT_PATH = "ArtifactStatsDetail.txt"
# Excel grid: "steering_test.xlsx" (main grid) or "test_integrativo.xlsx" (integrative test)
EXCEL_PATH      = "test_integrativo.xlsx"

# ── Load the TSV files ────────────────────────────────────────────
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
    # Artifact names read from the TSV, in alphabetical order
    ARTIFACT_NAMES = sorted(df_detail["artifactName"].astype(str).unique().tolist())
    print(f"Artifacts found: {ARTIFACT_NAMES}")
else:
    print(f"Warning: {DETAIL_TXT_PATH} not found, artifact columns skipped")
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
    """Writes a header cell with the standard style."""
    ws.cell(1, col).value = label
    ws.cell(1, col).fill = PatternFill(
        start_color="1F4E79", end_color="1F4E79", fill_type="solid")
    ws.cell(1, col).font = Font(
        bold=True, color="FFFFFF", name="Arial", size=10)
    ws.cell(1, col).alignment = Alignment(
        horizontal="center", vertical="center", wrap_text=True)

# ── Aggregate metric columns ──────────────────────────────────────
BASE_METRIC_COLS = [
    ("tempoWZ",    "TempoWZ"),
    ("navMesh",    "NavMesh"),
    ("distanzaWZ", "DistWZ"),
    ("distanzaTZ", "DistTZ"),
    ("tempoTZ",    "TempoTZ"),
    ("tempoTot",   "TempoTot"),
]

# ── Per-artifact metric columns ───────────────────────────────────
ARTIFACT_METRIC_COLS = [
    ("tempoWZ", "TempoWZ"),
    ("tempoTZ", "TempoTZ"),
    ("distWZ",  "DistWZ"),
    ("distTZ",  "DistTZ"),
    ("navMesh", "NavMesh"),
]

def get_aggregate_col_names(agent_name, occurrence):
    """
    Returns the (tsv_field, excel_column) pairs for the aggregate metrics,
    prefixed with the agent name.
    occurrence=0: no suffix, occurrence=1: ' (2)', and so on.
    """
    suffix = "" if occurrence == 0 else f" ({occurrence + 1})"
    return [
        (tsv_col, f"{agent_name}_{excel_label}{suffix}")
        for tsv_col, excel_label in BASE_METRIC_COLS
    ]

def get_artifact_col_names(agent_name, artifact_name, occurrence):
    """
    Returns the (tsv_field, excel_column) pairs for one artifact.
    Format: AgentName_ArtifactName_Metric (with the occurrence suffix if > 0).
    """
    suffix = "" if occurrence == 0 else f" ({occurrence + 1})"
    return [
        (tsv_col, f"{agent_name}_{artifact_name}_{excel_label}{suffix}")
        for tsv_col, excel_label in ARTIFACT_METRIC_COLS
    ]

def ensure_columns(ws, headers, agent_name, occurrence):
    """
    Creates the Excel columns for an occurrence if they do not exist yet:
    first the 6 aggregate ones, then 5 for each artifact.
    """
    # Aggregate columns
    for _, excel_col in get_aggregate_col_names(agent_name, occurrence):
        if excel_col not in headers:
            new_col = ws.max_column + 1
            make_header_cell(ws, new_col, excel_col)
            headers[excel_col] = new_col

    # Per-artifact columns
    for artifact_name in ARTIFACT_NAMES:
        for _, excel_col in get_artifact_col_names(agent_name, artifact_name, occurrence):
            if excel_col not in headers:
                new_col = ws.max_column + 1
                make_header_cell(ws, new_col, excel_col)
                headers[excel_col] = new_col

    return headers

def write_aggregate_metrics(ws, headers, row, agent_name, occurrence, data_row):
    """Writes the 6 aggregate metrics of an occurrence."""
    for tsv_col, excel_col in get_aggregate_col_names(agent_name, occurrence):
        if excel_col in headers:
            try:
                ws.cell(row, headers[excel_col]).value = round(get_float(data_row[tsv_col]), 2)
            except Exception as e:
                print(f"  Aggregate error {tsv_col}: {e}")

def write_artifact_metrics(ws, headers, row, agent_name, occurrence, artifact_name, artifact_row):
    """
    Writes the 5 metrics of one artifact for an occurrence.
    If artifact_row is None, all metrics are set to 0 (artifact not reached).
    """
    for tsv_col, excel_col in get_artifact_col_names(agent_name, artifact_name, occurrence):
        if excel_col in headers:
            try:
                val = round(get_float(artifact_row[tsv_col]), 2) if artifact_row is not None else 0
                ws.cell(row, headers[excel_col]).value = val
            except Exception as e:
                print(f"  Artifact error {artifact_name} {tsv_col}: {e}")

# ── Main loop ─────────────────────────────────────────────────────
headers = build_headers(ws)
filled  = 0

for row in range(2, ws.max_row + 1):
    config_num = ws.cell(row, headers["Configurazione"]).value

    if config_num is None:
        continue

    config_num = int(config_num)

    # Aggregate rows of this configuration
    agg_matches = df[df["configNumber"].astype(int) == config_num]

    if agg_matches.empty:
        continue

    # Each agent has its own block of columns; repeated runs of the same agent
    # get the suffix (2), (3), ...
    agent_occurrence = {}  # agent -> occurrence counter

    for _, agg_row in agg_matches.iterrows():
        agente = str(agg_row["agente"]).strip()

        # Occurrence index for this agent
        if agente not in agent_occurrence:
            agent_occurrence[agente] = 0
        else:
            agent_occurrence[agente] += 1
        occ = agent_occurrence[agente]

        # Create the columns if needed
        headers = ensure_columns(ws, headers, agente, occ)

        # Write the 6 aggregate metrics
        write_aggregate_metrics(ws, headers, row, agente, occ, agg_row)

        # Per-artifact columns
        if df_detail is not None:
            det_matches = df_detail[df_detail["configNumber"].astype(int) == config_num]
            det_agente = det_matches[det_matches["agente"].astype(str).str.strip() == agente]

            for artifact_name in ARTIFACT_NAMES:
                det_artifact = det_agente[
                    det_agente["artifactName"].astype(str).str.strip() == artifact_name
                ]

                # If the artifact is missing from the TSV, leave the Excel cells untouched
                # (do not overwrite them with zeros)
                if det_artifact.empty:
                    pass
                else:
                    write_artifact_metrics(ws, headers, row, agente, occ, artifact_name, det_artifact.iloc[0])

    filled += 1

wb.save(EXCEL_PATH)
print(f"\nDone: {filled} rows filled out of {ws.max_row - 1}.")