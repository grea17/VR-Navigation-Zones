import pandas as pd
import json
import sys
import os

#EXCEL_PATH = "steering_test.xlsx"
EXCEL_PATH = "test_integrativo.xlsx"
CONFIG_PATH = "config.json"

df = pd.read_excel(EXCEL_PATH, sheet_name="Test Completo", header=0)
df.columns = [str(c).strip() for c in df.columns]


# Rinomina colonne per comodità
col_map = {
    "angularSpeed\n(°/s)":          "angularSpeed",
    "warningSpeed\nMultiplier":      "warningSpeedMultiplier",
    "WZ Radius\n(m)":               "wzRadius",
    "TZ Radius\n(m)":               "tzRadius",
    "Pos. Obiettivo":               "posObiettivo",
    "Campione":                     "campione",
}
df = df.rename(columns=col_map)

# Numero configurazione da riga command line (1-based)
if len(sys.argv) < 2:
    print("Uso: python set_config.py <numero_configurazione>")
    sys.exit(1)

config_num = int(sys.argv[1])
#row = df.iloc[config_num - 1]
row = df[df["Configurazione"] == config_num].iloc[0]

# Determina scena in base a posObiettivo
pos = str(row["posObiettivo"]).strip()
scene = "WZ1" if pos == "Dentro WZ" else "WZ2"

config = {
    "configNumber":           config_num,
    "sceneName":              scene,
    "angularSpeed":           float(str(row["angularSpeed"]).replace(",", ".")),
    "warningSpeedMultiplier": float(str(row["warningSpeedMultiplier"]).replace(",", ".")),
    "wzRadius":               float(str(row["wzRadius"]).replace(",", ".")),
    "tzRadius":               float(str(row["tzRadius"]).replace(",", ".")),
    "posObiettivo":           pos,
    "campione":               int(row["campione"]),
}

with open(CONFIG_PATH, "w") as f:
    json.dump(config, f, indent=2)

print(f"Config {config_num} scritta → scena: {scene}, angularSpeed: {config['angularSpeed']}, "
      f"warningSpeedMultiplier: {config['warningSpeedMultiplier']}, "
      f"wzRadius: {config['wzRadius']}, tzRadius: {config['tzRadius']}, "
      f"campione: {config['campione']}")