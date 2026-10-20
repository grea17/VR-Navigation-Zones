"""
PedPy Analysis Script - Complete Fixed Version
Analisi traiettorie agenti con supporto Warning Zone e Transition Zone
Gestione robusta errori e zone opzionali
"""

import pedpy
from pedpy import load_trajectory, WalkableArea
import matplotlib.pyplot as plt
import matplotlib.patches as patches
from matplotlib.transforms import Affine2D
import matplotlib.patches as mpatches
import pathlib
import pandas as pd
import numpy as np
import seaborn as sns
import json
import os
from typing import List, Dict, Tuple, Optional


# ============================================================================
# CONFIGURAZIONE
# ============================================================================

SCALE = 50  # Unity units → PedPy units
WALKABLE_AREA = [
    (1750, -1750),
    (1750, 1750),
    (-1750, 1750),
    (-1750, -1750),
]


# ============================================================================
# PREPROCESSING
# ============================================================================

def preprocess_file(file_path: str, clean_path: str) -> bool:
    """
    Preprocess trajectory file: rimuove virgole come separatore migliaia
    Returns True se successo, False altrimenti
    """
    try:
        df = pd.read_csv(file_path, sep="\t", header=None, thousands=",")
        df.to_csv(clean_path, sep="\t", header=False, index=False)
        return True
    except FileNotFoundError:
        print(f"File not found: {file_path}")
        return False
    except Exception as e:
        print(f"Error preprocessing {file_path}: {e}")
        return False


# ============================================================================
# BLUEPRINT & ZONE DRAWING
# ============================================================================

def draw_scene_blueprint(ax, blueprint_path: str = "SceneBlueprint.json"):
    """
    Disegna elementi della scena (Objectives, Targets, Warning Zones, Transition Zones)
    SENZA LABEL - solo zone colorate
    """
    path = pathlib.Path(blueprint_path)
    if not path.exists():
        print(f"Blueprint non trovato in {path.absolute()}.")
        return

    with open(path, 'r') as f:
        data = json.load(f)
        elements = data.get("Items", [])

    SCALE = 50

    for el in elements:
        x_center = el['x'] * SCALE
        y_center = el['y'] * SCALE
        el_type = el['type']
        
        # ZONE CIRCOLARI (Warning & Transition) - SENZA LABEL
        if el_type in ["WarningZone", "TransitionZone"]:
            radius = el.get('radius', 1.0) * SCALE
            
            if el_type == "WarningZone":
                color = 'yellow'
                alpha = 0.15
                linestyle = '--'
                linewidth = 2
            else:  # TransitionZone
                color = 'cyan'
                alpha = 0.2
                linestyle = '-'
                linewidth = 2.5
            
            circle = mpatches.Circle((x_center, y_center), radius, 
                                    color=color,
                                    alpha=alpha, 
                                    fill=True, linestyle=linestyle, 
                                    linewidth=linewidth)
            ax.add_patch(circle)
        
        else:
            width = el.get('size_x', 1.0) * SCALE
            height = el.get('size_y', 1.0) * SCALE
            
            bottom_left_x = x_center - (width / 2)
            bottom_left_y = y_center - (height / 2)
            
            color = 'blue' if el_type == "Objective" else 'hotpink'
            rotation_deg = -el.get('rotation', 0.0)
            
            rect = patches.Rectangle((bottom_left_x, bottom_left_y), 
                                     width, height, 
                                     color=color, 
                                     alpha=0.6, linewidth=2)
            
            t = Affine2D().rotate_deg_around(x_center, y_center, rotation_deg)
            rect.set_transform(t + ax.transData)
            ax.add_patch(rect)

    return True


# ============================================================================
# METRICHE BASE
# ============================================================================

def compute_agent_metrics(traj, agent_names: Optional[Dict] = None) -> pd.DataFrame:
    """
    Calcola metriche base per ogni agente: durata, distanza, velocità media
    """
    metrics = []
    for agent_id in traj.data["id"].unique():
        agent_traj = traj.data[traj.data["id"] == agent_id].sort_values("frame")
        x, y, frames = agent_traj["x"].values, agent_traj["y"].values, agent_traj["frame"].values
        
        if len(x) < 2:
            continue
        
        # Distance
        dist = np.sum(np.sqrt(np.diff(x)**2 + np.diff(y)**2))

        # Duration (in seconds)
        duration = (frames[-1] - frames[0]) / traj.frame_rate

        # Average speed
        avg_speed = dist / duration if duration > 0 else 0
        
        metrics.append({
            "id": agent_id,
            "name": agent_names[agent_id] if agent_names and agent_id in agent_names else str(agent_id),
            "duration": duration,
            "distance": dist,
            "avg_speed": avg_speed
        })
    return pd.DataFrame(metrics)



# ============================================================================
# ANALISI ZONE
# ============================================================================

def load_zones(blueprint_path: str) -> Tuple[List[Dict], List[Dict]]:
    """
    Carica Warning Zones e Transition Zones dal blueprint
    """
    path = pathlib.Path(blueprint_path)
    if not path.exists():
        return [], []
    
    try:
        with open(path, 'r') as f:
            data = json.load(f)
            elements = data.get("Items", [])
        
        warning_zones = []
        transition_zones = []
        
        for el in elements:
            x = el['x'] * SCALE
            y = el['y'] * SCALE
            radius = el.get('radius', 1.0) * SCALE
            
            if el['type'] == "WarningZone":
                warning_zones.append({
                    'x': x, 'y': y, 'radius': radius, 
                    'name': el.get('name', 'WZ')
                })
            elif el['type'] == "TransitionZone":
                transition_zones.append({
                    'x': x, 'y': y, 'radius': radius, 
                    'name': el.get('name', 'TZ')
                })
        
        return warning_zones, transition_zones
        
    except Exception as e:
        print(f"Error loading zones: {e}")
        return [], []


def analyze_time_in_zones(traj, blueprint_path: str = "SceneBlueprint.json") -> pd.DataFrame:
    """
    Calcola quanto tempo ogni agent passa in Warning vs Transition Zone.
    """
    warning_zones, transition_zones = load_zones(blueprint_path)
    
    if not warning_zones and not transition_zones:
        print("Nessuna zona trovata nel blueprint - skip zone analysis")
        return pd.DataFrame()
    
    results = []
    
    for agent_id in traj.data["id"].unique():
        agent_traj = traj.data[traj.data["id"] == agent_id].sort_values("frame")
        
        # Se c'è meno di un frame, non possiamo calcolare differenze di tempo
        if len(agent_traj) < 2:
            continue
            
        frames = agent_traj["frame"].values
        
        time_warning_sec = 0.0
        time_transition_sec = 0.0
        
        # Il tempo totale esatto è: (ultimo_frame - primo_frame) / frame_rate
        time_total_sec = (frames[-1] - frames[0]) / traj.frame_rate
        
        # Iteriamo su tutti i punti tranne l'ultimo per calcolare il tempo speso "fino al prossimo punto"
        for i in range(len(agent_traj) - 1):
            row = agent_traj.iloc[i]
            x, y = row['x'], row['y']
            
            # Calcoliamo il Delta Time (dt) esatto tra questo log e il successivo
            dt = (frames[i+1] - frames[i]) / traj.frame_rate
            
            # Controlliamo prima le Transition Zones (hanno priorità)
            in_transition = any(
                np.sqrt((x - tz['x'])**2 + (y - tz['y'])**2) < tz['radius']
                for tz in transition_zones
            )
            
            if in_transition:
                time_transition_sec += dt
                continue  # Passiamo al punto successivo
                
            # Controlliamo le Warning Zones
            in_warning = any(
                np.sqrt((x - wz['x'])**2 + (y - wz['y'])**2) < wz['radius']
                for wz in warning_zones
            )
            
            if in_warning:
                time_warning_sec += dt
        
        # Il tempo in Free Navigation è semplicemente tutto il resto
        time_free_sec = time_total_sec - time_warning_sec - time_transition_sec
        
        # Salviamo i risultati per l'agente
        results.append({
            "agent_id": agent_id,
            "time_warning": time_warning_sec,
            "time_transition": time_transition_sec,
            "time_free": time_free_sec,
            "total_time": time_total_sec,
            "pct_warning": (time_warning_sec / time_total_sec * 100) if time_total_sec > 0 else 0,
            "pct_transition": (time_transition_sec / time_total_sec * 100) if time_total_sec > 0 else 0
        })
    
    df = pd.DataFrame(results)
    return df


# ============================================================================
# VISUALIZZAZIONI
# ============================================================================

def plot_trajectories_with_zones(traj, agent_colors: Dict, 
                                 blueprint_path: str = "SceneBlueprint.json",
                                 background_img: str = "BackgroundMap.png",
                                 save_path: str = None,
                                 disegna_blueprint: bool = True,
                                 agent_names: Dict = None): 
    """
    Plot traiettorie con overlay zone e background map - SUPPORTO NOMI VERI
    """
    fig, ax = plt.subplots(figsize=(14, 14))
    
    try:
        img = plt.imread(background_img)
        ax.imshow(img, zorder=0, extent=[-1750, 1750, -1750, 1750], alpha=0.7)
    except:
        print(f"Background image not found: {background_img}")
    
    from matplotlib.patches import Patch
    from matplotlib.lines import Line2D
    
    linee_agenti = []
    
    # Plot trajectories
    for agent_id in traj.data["id"].unique():
        agent_traj = traj.data[traj.data["id"] == agent_id]
        
        label = None
        color = None
        
        # 1. Priorità: se abbiamo fornito il dizionario dei veri nomi, lo usiamo
        if agent_names and agent_id in agent_names:
            label = agent_names[agent_id]
            # Associa il colore corretto (cercando per nome oppure per ID originale)
            color = agent_colors.get(label, agent_colors.get(agent_id, 'purple'))
        else:
            # 2. Fallback di sicurezza per gli altri script
            for name, col in agent_colors.items():
                if str(agent_id) == str(name) or str(agent_id) in str(name):
                    label = name
                    color = col
                    break
        
        if label is None:
            label = f"Agent {agent_id}"
            color = 'purple'
        
        # Disegna la traiettoria
        ax.plot(agent_traj["x"], agent_traj["y"], color=color, label=label, 
               linewidth=1.5, alpha=1.0, zorder=5)
        
        linee_agenti.append(Line2D([0], [0], color=color, linewidth=1.5, label=label))
    
    # Creazione legenda
    from matplotlib.patches import Patch
    from matplotlib.lines import Line2D

    legend_elements = []
    if disegna_blueprint:
        draw_scene_blueprint(ax, blueprint_path)
        legend_elements += [
            Patch(facecolor='yellow', edgecolor='gold', alpha=0.3,
                  label='Warning Zone', linestyle='--', linewidth=2),
            Patch(facecolor='cyan', edgecolor='deepskyblue', alpha=0.4,
                  label='Transition Zone', linewidth=2),
        ]

    # Una voce per ogni agente (la versione originale mostrava solo l'ultimo)
    for agent_id, col in agent_colors.items():
        nome = agent_names.get(agent_id, str(agent_id)) if agent_names else str(agent_id)
        legend_elements.append(Line2D([0], [0], color=col, linewidth=3, label=nome))

    ax.legend(handles=legend_elements, loc='lower right', fontsize=14,
              framealpha=0.9, edgecolor='black', fancybox=True)
    ax.set_aspect("equal")

    if disegna_blueprint:
        ax.set_title("Agent Trajectories with Warning & Transition Zones",
                     fontsize=16, fontweight='bold', pad=10, y=1.05,
                     bbox=dict(boxstyle='round,pad=0.5', facecolor='white',
                               alpha=0.85, edgecolor='black', linewidth=2))
    else:
        ax.set_xticks([])
        ax.set_yticks([])

    ax.set_xlim(-1100, 800) 
    ax.set_ylim(-350, 1450)

    #titolo = "Agent Trajectories with Warning & Transition Zones" if disegna_blueprint else "Agent Trajectories Comparison"
    #ax.set_title(titolo, fontsize=16, fontweight='bold', pad=10, y=1.05, 
    #            bbox=dict(boxstyle='round,pad=0.5', facecolor='white', alpha=0.85, edgecolor='black', linewidth=2))
    
    plt.subplots_adjust(top=0.88)
    
    if save_path:
        import os
        dir_name = os.path.dirname(save_path)
        if dir_name:
            os.makedirs(dir_name, exist_ok=True)
        plt.savefig(save_path, dpi=300, bbox_inches='tight') 
        plt.close(fig) 
        print(f" Grafico traiettoria salvato: {save_path}")
    else:
        plt.show()

def plot_zone_time_pie_charts(zone_df: pd.DataFrame, agent_names: Dict = None, title: str = "Zone Time Distribution"):
    """
    Grafico a torta basato sui dati calcolati dalla traiettoria (stessi dati della timeline)
    Genera un grafico a torta per ogni agente.
    """
    if len(zone_df) == 0:
        print("Skipping zone pie charts (no data)")
        return

    for _, row in zone_df.iterrows():
        agent_id = row['agent_id']
        agent_name = agent_names.get(agent_id, str(agent_id)) if agent_names else str(agent_id)
        
        sizes = [row['time_free'], row['time_warning'], row['time_transition']]
        
        # Se l'agente non si è mai mosso o ha tempo 0, salta
        if sum(sizes) <= 0:
            continue
            
        labels = ['Free Navigation', 'Warning Zone', 'Transition Zone']
        colors = ['#90EE90', '#FFD700', '#40E0D0']  # Verde, Giallo, Cyan
        explode = (0.05, 0.05, 0.05)  
        
        fig, ax = plt.subplots(figsize=(10, 8))
        
        wedges, texts, autotexts = ax.pie(
            sizes, 
            explode=explode, 
            labels=labels, 
            colors=colors,
            autopct='%1.1f%%',
            startangle=90,
            textprops={'fontsize': 13, 'fontweight': 'bold'}
        )
        
        for autotext in autotexts:
            autotext.set_color('black')
            autotext.set_fontsize(14)
            autotext.set_fontweight('bold')
        
        legend_labels = [
            f'{labels[0]}: {sizes[0]:.2f}s ({sizes[0]/sum(sizes)*100:.1f}%)',
            f'{labels[1]}: {sizes[1]:.2f}s ({sizes[1]/sum(sizes)*100:.1f}%)',
            f'{labels[2]}: {sizes[2]:.2f}s ({sizes[2]/sum(sizes)*100:.1f}%)'
        ]
        
        ax.legend(legend_labels, loc='upper left', bbox_to_anchor=(1, 1), 
                 fontsize=11, frameon=True, shadow=True)
        
        ax.set_title(f'{title}\nAgent: {agent_name}\nTotal Time: {sum(sizes):.2f}s', 
                    fontsize=15, fontweight='bold', pad=20)
        
        plt.tight_layout()
        plt.show()

def draw_table_image(df, title: str = "Metriche Agenti"):
    """
    Disegna tabella come immagine
    """
    if len(df) == 0:
        print(f"Skipping table '{title}' (empty dataframe)")
        return
    
    df_plot = df.round(2)
    
    fig, ax = plt.subplots(figsize=(12, max(len(df) * 0.5 + 2, 4))) 
    ax.axis('tight')
    ax.axis('off')
    
    the_table = ax.table(cellText=df_plot.values, 
                        colLabels=df_plot.columns, 
                        cellLoc='center', 
                        loc='center',
                        colWidths=[0.15] * len(df_plot.columns))
    
    the_table.auto_set_font_size(False)
    the_table.set_fontsize(11)
    the_table.scale(1.2, 2.0) 
    
    # Style header
    for i in range(len(df_plot.columns)):
        the_table[(0, i)].set_facecolor('#4CAF50')
        the_table[(0, i)].set_text_props(weight='bold', color='white')
    
    plt.title(title, pad=20, fontsize=14, fontweight='bold')
    plt.tight_layout()
    plt.show()

def create_heatmap(traj, ml_ids, navmesh_ids, grid_size=100):
    """Crea heatmap affiancate con scala condivisa (Lineare)"""
    
    # Calcola range globale
    all_data = traj.data[traj.data["id"].isin(ml_ids + navmesh_ids)]
    H_global, _, _ = np.histogram2d(
        all_data["x"], all_data["y"], 
        bins=grid_size, 
        range=[[-1750, 1750], [-1750, 1750]]
    )
    vmax = H_global.max()
    
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(20, 10), constrained_layout=True)
    img = plt.imread("BackgroundMap.png")
    
    # ML-Agent heatmap
    ml_data = traj.data[traj.data["id"].isin(ml_ids)]
    ax1.imshow(img, zorder=0, extent=[-1750, 1750, -1750, 1750], alpha=0.5)
    
    # Rimosso LogNorm, aggiunto cmin=1, vmin=1, vmax=vmax
    h1 = ax1.hist2d(ml_data["x"], ml_data["y"], bins=grid_size,
                    range=[[-1750, 1750], [-1750, 1750]],
                    cmap='inferno', alpha=0.7, 
                    cmin=1, vmin=1, vmax=vmax) 
    ax1.set_title("ML-Agent puro Coverage", fontsize=14, fontweight='bold')
    ax1.axis('off')
    
    # NavMesh heatmap
    nm_data = traj.data[traj.data["id"].isin(navmesh_ids)]
    ax2.imshow(img, zorder=0, extent=[-1750, 1750, -1750, 1750], alpha=0.5)
    
    h2 = ax2.hist2d(nm_data["x"], nm_data["y"], bins=grid_size,
                    range=[[-1750, 1750], [-1750, 1750]],
                    cmap='inferno', alpha=0.7, 
                    cmin=1, vmin=1, vmax=vmax)
    ax2.set_title("NavMesh Coverage", fontsize=14, fontweight='bold')
    ax2.axis('off')
    
    # Colorbar condivisa
    fig.colorbar(h2[3], ax=[ax1, ax2], label="Frequenza di Passaggio")
    
    plt.show()

def plot_metric_with_values(data: pd.DataFrame, x: str, y: str, 
                            title: str, ylabel: str, custom_palette: Dict):
    """
    Bar plot con valori sopra le barre
    """
    if len(data) == 0:
        print(f"Skipping plot '{title}' (empty data)")
        return
    
    plt.figure(figsize=(10, 6))
    sns.set_style("whitegrid")
    
    ax = sns.barplot(data=data, x=x, y=y, hue=x, palette=custom_palette, legend=False)
    
    for container in ax.containers:
        ax.bar_label(container, fmt='%.1f', padding=3, fontsize=10, fontweight='bold')
        
    plt.title(title, fontsize=14, fontweight='bold')
    plt.xlabel("") 
    plt.ylabel(ylabel, fontsize=12)
    plt.ylim(0, data[y].max() * 1.15) 
    plt.xticks(rotation=45, ha='right')
    plt.tight_layout()
    plt.show()


def plot_zone_time_distribution(zone_df: pd.DataFrame, agent_colors: Dict):
    """
    Plot distribuzione tempo in zone (stacked bar chart)
    """
    if len(zone_df) == 0:
        print("Skipping zone time distribution (no data)")
        return
    
    fig, ax = plt.subplots(figsize=(12, 7))
    
    x = np.arange(len(zone_df))
    width = 0.6
    
    # Stacked bars
    p1 = ax.bar(x, zone_df['time_free'], width, label='Free Navigation', 
               color='lightgreen', alpha=0.8)
    p2 = ax.bar(x, zone_df['time_warning'], width, bottom=zone_df['time_free'],
               label='Warning Zone', color='gold', alpha=0.8)
    p3 = ax.bar(x, zone_df['time_transition'], width, 
               bottom=zone_df['time_free'] + zone_df['time_warning'],
               label='Transition Zone', color='cyan', alpha=0.8)
    
    # Labels
    ax.set_xlabel('Agent ID', fontsize=12, fontweight='bold')
    ax.set_ylabel('Time (seconds)', fontsize=12, fontweight='bold')
    ax.set_title('Time Distribution Across Zones', fontsize=14, fontweight='bold')
    ax.set_xticks(x)
    ax.set_xticklabels(zone_df['agent_id'])
    ax.legend(fontsize=10)
    ax.grid(axis='y', alpha=0.3)
    
    plt.tight_layout()
    plt.show()

def plot_timeline_zones(traj, blueprint_path: str = "SceneBlueprint.json"):
    """
    Genera un Timeline Plot che mostra la sequenza temporale delle zone visitate.
    """
    warning_zones, transition_zones = load_zones(blueprint_path)
    if not warning_zones and not transition_zones:
        print("Nessuna zona per la timeline")
        return

    for agent_id in traj.data["id"].unique():
        agent_traj = traj.data[traj.data["id"] == agent_id].sort_values("frame")
        
        # Prepariamo i dati per la timeline
        times = (agent_traj["frame"].values - agent_traj["frame"].values[0]) / traj.frame_rate
        states = []

        for _, row in agent_traj.iterrows():
            x, y = row['x'], row['y']
            
            in_tz = any(np.sqrt((x-tz['x'])**2 + (y-tz['y'])**2) < tz['radius'] for tz in transition_zones)
            in_wz = any(np.sqrt((x-wz['x'])**2 + (y-wz['y'])**2) < wz['radius'] for wz in warning_zones)
            
            if in_tz: states.append(2)    # Transition
            elif in_wz: states.append(1)  # Warning
            else: states.append(0)        # Free

        # Plotting
        plt.figure(figsize=(15, 4))
        
        # Creiamo una mappa colori personalizzata
        from matplotlib.colors import ListedColormap
        cmap = ListedColormap(['lightgreen', 'gold', 'cyan'])
        
        # Usiamo pcolormesh per creare la barra temporale continua
        plt.pcolormesh(times, [0, 1], np.array(states)[np.newaxis, :-1], cmap=cmap, shading='flat')
        
        # Estetica
        plt.title(f"Timeline dell'Agente {agent_id}: Sequenza Zone", fontsize=14, fontweight='bold', pad=15)
        plt.xlabel("Tempo (secondi)", fontsize=12)
        plt.yticks([]) # Nascondiamo l'asse Y che non serve
        
        # Legenda manuale
        from matplotlib.patches import Patch
        legend_elements = [
            Patch(facecolor='lightgreen', label='Free Navigation'),
            Patch(facecolor='gold', label='Warning Zone'),
            Patch(facecolor='cyan', label='Transition Zone')
        ]
        plt.legend(handles=legend_elements, loc='upper right', bbox_to_anchor=(1, 1.25), ncol=3)
        
        plt.tight_layout()
        plt.show()

def plot_speed_over_time(traj, agent_names: Dict, custom_colors: Dict):
    """
    Plot profilo velocità nel tempo (smoothed)
    """
    plt.figure(figsize=(14, 7))
    sns.set_style("whitegrid")

    for agent_id in traj.data["id"].unique():
        agent_traj = traj.data[traj.data["id"] == agent_id].sort_values("frame")
        x, y, frames = agent_traj["x"].values, agent_traj["y"].values, agent_traj["frame"].values
        
        if len(x) < 2:
            continue
        
        # Calcolo velocità istantanea
        dx = np.diff(x)
        dy = np.diff(y)
        dt = np.diff(frames) / traj.frame_rate
        
        with np.errstate(divide='ignore', invalid='ignore'):
            raw_speed = np.sqrt(dx**2 + dy**2) / dt
            raw_speed = np.nan_to_num(raw_speed)
        
        # Smoothing (Media Mobile)
        speed_series = pd.Series(raw_speed)
        smooth_speed = speed_series.rolling(window=30, min_periods=1).mean()
        
        # Tempo normalizzato
        time = (frames[1:] - frames[0]) / traj.frame_rate
        
        label = agent_names.get(agent_id, str(agent_id))
        
        # ✅ FIX: Gestione robusta colore
        color = None
        for name, col in custom_colors.items():
            if str(agent_id) in str(name) or label == name:
                color = col
                break
        
        if color is None:
            color = 'gray'
        
        smooth_speed = smooth_speed.clip(upper=150)

        plt.plot(time, smooth_speed, label=label, color=color, alpha=0.8, linewidth=2.5)
    
    plt.xlabel("Time (s)", fontsize=12, fontweight='bold')
    plt.ylabel("Speed (units/s)", fontsize=12, fontweight='bold')
    plt.title("Speed Profile Over Time (Smoothed)", fontsize=14, fontweight='bold')
    plt.legend(fontsize=10, loc='best')
    plt.grid(True, alpha=0.3)
    plt.tight_layout()
    plt.show()


def create_comparison_heatmap(traj, ml_ids: List, navmesh_ids: List, 
                              background_img: str = "BackgroundMap.png",
                              grid_size: int = 100):
    """
    Heatmap comparativa ML-Agent vs NavMesh
    """
    all_data = traj.data[traj.data["id"].isin(ml_ids + navmesh_ids)]
    
    if len(all_data) == 0:
        print("No data for heatmap")
        return
    
    H_global, _, _ = np.histogram2d(
        all_data["x"], all_data["y"], 
        bins=grid_size, 
        range=[[-1750, 1750], [-1750, 1750]]
    )
    nonzero = H_global[H_global > 0]
    vmax = np.percentile(nonzero, 98) if len(nonzero) > 0 else 1
    
    fig, axes = plt.subplots(1, 2, figsize=(20, 10))
    
    try:
        img = plt.imread(background_img)
    except:
        img = None
    
    # ML-Agent heatmap
    ml_data = traj.data[traj.data["id"].isin(ml_ids)]
    if img is not None:
        axes[0].imshow(img, zorder=0, extent=[-1750, 1750, -1750, 1750], alpha=0.5)
    
    if len(ml_data) > 0:
        h1 = axes[0].hist2d(ml_data["x"], ml_data["y"], bins=grid_size,
                        range=[[-1750, 1750], [-1750, 1750]],
                        cmap='inferno', alpha=0.75, cmin=1, vmin=1, vmax=vmax)
    axes[0].set_title("ML-Agent Coverage", fontsize=16, fontweight='bold')
    axes[0].axis('off')
    
    # NavMesh heatmap
    nm_data = traj.data[traj.data["id"].isin(navmesh_ids)]
    if img is not None:
        axes[1].imshow(img, zorder=0, extent=[-1750, 1750, -1750, 1750], alpha=0.5)
    
    h2 = None
    if len(nm_data) > 0:
        h2 = axes[1].hist2d(nm_data["x"], nm_data["y"], bins=grid_size,
                        range=[[-1750, 1750], [-1750, 1750]],
                        cmap='inferno', alpha=0.75, cmin=1, vmax=vmax)
    axes[1].set_title("NavMesh Coverage", fontsize=16, fontweight='bold')
    axes[1].axis('off')
    
    if h2 is not None:
        fig.colorbar(h2[3], ax=axes, label="Passage Frequency", shrink=0.8)
    
    plt.tight_layout()
    plt.show()


def densify_trajectory(traj_df, factor=5):
    new_points = []

    for i in range(len(traj_df) - 1):
        x1, y1 = traj_df.iloc[i][['x', 'y']]
        x2, y2 = traj_df.iloc[i + 1][['x', 'y']]

        for t in np.linspace(0, 1, factor, endpoint=False):
            x = x1 + (x2 - x1) * t
            y = y1 + (y2 - y1) * t
            new_points.append((x, y))

    # aggiungi ultimo punto
    x_last, y_last = traj_df.iloc[-1][['x', 'y']]
    new_points.append((x_last, y_last))

    return np.array(new_points)

def create_zone_heatmap(traj, blueprint_path: str = "SceneBlueprint.json",
                       background_img: str = "BackgroundMap.png"):
    """
    Heatmap separata per Warning Zone, Transition Zone, Free Navigation
    """
    warning_zones, transition_zones = load_zones(blueprint_path)
    
    if not warning_zones and not transition_zones:
        print("No zones in blueprint - skipping zone heatmap")
        return
    
    # Classify trajectory points
    warning_points = []
    transition_points = []
    free_points = []
    dense_points = densify_trajectory(traj.data, factor=2)
    
    for x, y in dense_points:
        
        in_transition = any(
            np.sqrt((x - tz['x'])**2 + (y - tz['y'])**2) < tz['radius']
            for tz in transition_zones
        )
        
        in_warning = any(
            np.sqrt((x - wz['x'])**2 + (y - wz['y'])**2) < wz['radius']
            for wz in warning_zones
        )
        
        if in_transition:
            transition_points.append((x, y))
        elif in_warning:
            warning_points.append((x, y))
        else:
            free_points.append((x, y))
    
    # Plot
    fig, axes = plt.subplots(1, 3, figsize=(24, 8))
    
    try:
        img = plt.imread(background_img)
    except:
        img = None
    
    titles = ["Free Navigation", "Warning Zone", "Transition Zone"]
    point_sets = [free_points, warning_points, transition_points]
    colors = ['green', 'orange', 'blue']
    
    for ax, title, points, color in zip(axes, titles, point_sets, colors):
        if img is not None:
            ax.imshow(img, extent=[-1750, 1750, -1750, 1750], alpha=0.5)
        
        if points:
            points_array = np.array(points)
            ax.scatter(points_array[:, 0], points_array[:, 1], 
                      c=color, alpha=0.4, s=8, edgecolors='none')
            ax.set_title(f"{title} ({len(points)} points)", fontsize=14, fontweight='bold')
        else:
            ax.set_title(f"{title} (0 points)", fontsize=14, fontweight='bold')
        
        ax.axis('off')
        
    
    plt.tight_layout()
    plt.show()



def compare_scenarios(baseline_file: str, warningzone_file: str,
                     blueprint_path: str = "SceneBlueprint.json"):
    """
    Confronta metriche tra baseline e warning zone scenario
    """
    print("\n" + "="*70)
    print("LOADING TRAJECTORIES FOR COMPARISON")
    print("="*70)
    
    # Preprocess
    if not preprocess_file(baseline_file, "baseline_clean.txt"):
        return None, None, None
    if not preprocess_file(warningzone_file, "warningzone_clean.txt"):
        return None, None, None
    
    # Load
    try:
        traj_baseline = load_trajectory(
            trajectory_file=pathlib.Path("baseline_clean.txt"),
            default_frame_rate=100,
            default_unit=1
        )
        
        traj_wz = load_trajectory(
            trajectory_file=pathlib.Path("warningzone_clean.txt"),
            default_frame_rate=100,
            default_unit=1
        )
    except Exception as e:
        print(f"Error loading trajectories: {e}")
        return None, None, None
    
    # Compute metrics
    print("\nComputing baseline metrics...")
    metrics_baseline = compute_agent_metrics(traj_baseline)
    zones_baseline = analyze_time_in_zones(traj_baseline, blueprint_path)
    
    print("\nComputing warning zone metrics...")
    metrics_wz = compute_agent_metrics(traj_wz)
    zones_wz = analyze_time_in_zones(traj_wz, blueprint_path)
    
    # Comparison DataFrame
    comparison_data = {
        "Metric": [
            "Avg Duration (s)", 
            "Avg Distance", 
            "Avg Speed", 
            
        ],
        "Baseline": [
            metrics_baseline["duration"].mean(),
            metrics_baseline["distance"].mean(),
            metrics_baseline["avg_speed"].mean(),
            
        ],
        "Warning Zone": [
            metrics_wz["duration"].mean(),
            metrics_wz["distance"].mean(),
            metrics_wz["avg_speed"].mean(),
         
        ]
    }
    
    # Add zone metrics if available
    if len(zones_baseline) > 0 and len(zones_wz) > 0:
        comparison_data["Metric"].extend([
            "Avg Time in Transition Zone (s)",
            "% Time in Transition Zone"
        ])
        comparison_data["Baseline"].extend([
            zones_baseline["time_transition"].mean(),
            zones_baseline["pct_transition"].mean()
        ])
        comparison_data["Warning Zone"].extend([
            zones_wz["time_transition"].mean(),
            zones_wz["pct_transition"].mean()
        ])
    
    comparison = pd.DataFrame(comparison_data)
    
    # Calculate improvement
    comparison["Improvement (%)"] = (
        (comparison["Warning Zone"] - comparison["Baseline"]) / 
        comparison["Baseline"].abs() * 100
    )
    
    # Special handling for Transition Zone time (reduction is positive)
    if len(comparison) > 4:
        comparison.loc[4, "Improvement (%)"] = (
            (comparison.loc[4, "Baseline"] - comparison.loc[4, "Warning Zone"]) / 
            comparison.loc[4, "Baseline"] * 100
        )
        comparison.loc[5, "Improvement (%)"] = (
            (comparison.loc[5, "Baseline"] - comparison.loc[5, "Warning Zone"]) / 
            comparison.loc[5, "Baseline"] * 100
        )
    
    print("COMPARISON: Baseline vs Warning Zone \n")
    print(comparison.round(2).to_string(index=False))
    
    # Visualization
    fig, axes = plt.subplots(2, 3, figsize=(18, 10))
    fig.suptitle("Baseline vs Warning Zone - Detailed Comparison", 
                fontsize=16, fontweight='bold')
    
    metrics_list = [
        ("duration", "Duration (s)", axes[0, 0], metrics_baseline, metrics_wz),
        ("distance", "Distance", axes[0, 1], metrics_baseline, metrics_wz),
        ("avg_speed", "Avg Speed", axes[0, 2], metrics_baseline, metrics_wz),
    ]
    
    for metric, title, ax, df_base, df_wz in metrics_list:
        data = pd.DataFrame({
            "Scenario": ["Baseline"] * len(df_base) + ["Warning Zone"] * len(df_wz),
            "Value": list(df_base[metric]) + list(df_wz[metric])
        })
        sns.barplot(data=data, x="Scenario", y="Value", ax=ax, 
                   palette={"Baseline": "salmon", "Warning Zone": "lightgreen"})
        ax.set_title(title, fontweight='bold')
        ax.set_ylabel("")
        ax.grid(axis='y', alpha=0.3)
    
    # Transition Zone time
    if len(zones_baseline) > 0 and len(zones_wz) > 0:
        data_transition = pd.DataFrame({
            "Scenario": (["Baseline"] * len(zones_baseline) + 
                        ["Warning Zone"] * len(zones_wz)),
            "Value": (list(zones_baseline["time_transition"]) + 
                     list(zones_wz["time_transition"]))
        })
        sns.barplot(data=data_transition, x="Scenario", y="Value", ax=axes[1, 1], 
                   palette={"Baseline": "salmon", "Warning Zone": "lightgreen"})
        axes[1, 1].set_title("Time in Transition Zone (s)", fontweight='bold')
        axes[1, 1].set_ylabel("")
        axes[1, 1].grid(axis='y', alpha=0.3)
        
        # % Time in zones
        data_pct = pd.DataFrame({
            "Scenario": (["Baseline"] * len(zones_baseline) + 
                        ["Warning Zone"] * len(zones_wz)),
            "Value": (list(zones_baseline["pct_transition"]) + 
                     list(zones_wz["pct_transition"]))
        })
        sns.barplot(data=data_pct, x="Scenario", y="Value", ax=axes[1, 2], 
                   palette={"Baseline": "salmon", "Warning Zone": "lightgreen"})
        axes[1, 2].set_title("% Time in Transition Zone", fontweight='bold')
        axes[1, 2].set_ylabel("")
        axes[1, 2].grid(axis='y', alpha=0.3)
    else:
        axes[1, 1].text(0.5, 0.5, 'No zone data', ha='center', va='center')
        axes[1, 1].axis('off')
        axes[1, 2].text(0.5, 0.5, 'No zone data', ha='center', va='center')
        axes[1, 2].axis('off')
    
    plt.tight_layout()
    plt.show()
    
    return comparison, traj_baseline, traj_wz



def main_analysis(trajectory_file: str = "PedPyStats.txt",
                 user_file: str = "UserStats.txt",
                 blueprint_path: str = "SceneBlueprint.json",
                 background_img: str = "BackgroundMap.png",
                 config_num: str = None,  
                 save_only_traj: bool = False):

    
    if not preprocess_file(trajectory_file, "PedPyStats_clean.txt"):
        print("Cannot continue without trajectory file")
        return None, None, None, None
    
    preprocess_file(user_file, "UserStats_clean.txt") 
    
    # Load trajectories
    try:
        traj = load_trajectory(
            trajectory_file=pathlib.Path("PedPyStats_clean.txt"),
            default_frame_rate=100,
            default_unit=1
        )
    except Exception as e:
        print(f"Error loading trajectory: {e}")
        return None, None, None, None
    
    # Detect agent IDs
    present_ids = traj.data["id"].unique().tolist()
    
    if len(present_ids) == 0:
        print("No agents found in trajectory")
        return None, None, None, None
    
    # Assign agent types (customize based on your setup)
    # Assumption: first agent = ML, rest = NavMesh
    ml_agent_ids = present_ids[:max(1, len(present_ids)//2)]
    navmesh_agent_ids = present_ids[len(ml_agent_ids):]
    
    # Create names and colors
    agent_names = {}
    for i, aid in enumerate(ml_agent_ids):
        agent_names[aid] = f"ML-Agent {i+1}"
    for i, aid in enumerate(navmesh_agent_ids):
        agent_names[aid] = f"NavMesh {i+1}"
    
    # Generate colors
    if len(ml_agent_ids) > 0:
        ml_colors = plt.cm.Reds(np.linspace(0.4, 1, len(ml_agent_ids)))
    else:
        ml_colors = []
    
    if len(navmesh_agent_ids) > 0:
        navmesh_colors = plt.cm.Blues(np.linspace(0.4, 1, len(navmesh_agent_ids)))
    else:
        navmesh_colors = []
    
    agent_colors = {}
    for i, aid in enumerate(ml_agent_ids):
        agent_colors[f"ML-Agent {i+1}"] = ml_colors[i] if len(ml_colors) > 0 else 'red'
    for i, aid in enumerate(navmesh_agent_ids):
        agent_colors[f"NavMesh {i+1}"] = navmesh_colors[i] if len(navmesh_colors) > 0 else 'blue'
    
    # Compute metrics
    print("\nComputing metrics...")
    metrics = compute_agent_metrics(traj, agent_names)
    
    # Add names to metrics
    metrics["name"] = metrics["id"].apply(lambda x: agent_names.get(x, str(x)))
    
    # Zone analysis (if blueprint available)
    zone_analysis = pd.DataFrame()
    if pathlib.Path(blueprint_path).exists():
        print("\nAnalyzing zones (and Navigation types)...\n")
        zone_analysis = analyze_time_in_zones(traj, blueprint_path)
    else:
        print("\nBlueprint not found - skipping zone analysis")
    
    # 1. Trajectories with zones
    if config_num:
        base_dir = "traiettorie"
        # Assicuriamoci che la cartella esista per poter controllare i file
        os.makedirs(base_dir, exist_ok=True) 
        
        base_name = f"configurazione{config_num}"
        save_file = os.path.join(base_dir, f"{base_name}.png")
        
        # Se il file esiste già, aggiungiamo il suffisso di versione (.1, .2, .3, ecc.)
        version = 1
        while os.path.exists(save_file):
            save_file = os.path.join(base_dir, f"{base_name}.{version}.png")
            version += 1
    else:
        save_file = None

    plot_trajectories_with_zones(traj, agent_colors, blueprint_path, background_img, save_path=save_file)

    # Se vogliamo SOLO salvare le traiettorie, usciamo subito dalla funzione
    if save_only_traj:
        return traj, metrics, zone_analysis

    create_heatmap(traj, ml_agent_ids, navmesh_agent_ids, grid_size=100)
    
    # 2. Metrics tables
    draw_table_image(metrics, "Agent Metrics Summary")
    
    # 3. Bar plots
    plot_metric_with_values(metrics, "name", "duration", 
                           "Trajectory Duration", "Seconds (s)", agent_colors)
    plot_metric_with_values(metrics, "name", "distance", 
                           "Distance Traveled", "Units", agent_colors)
    plot_metric_with_values(metrics, "name", "avg_speed", 
                           "Average Speed", "Units/s", agent_colors)
    
    # 4. Zone analysis plots
    if len(zone_analysis) > 0:
        plot_zone_time_distribution(zone_analysis, agent_colors)
        plot_timeline_zones(traj, blueprint_path)
        plot_zone_time_pie_charts(zone_analysis, agent_names, title="Zone & Navigation Distribution")
        create_zone_heatmap(traj, blueprint_path, background_img)
        
    else:
        print("Skipping zone plots (no zone data)")
    
    # 5. Speed over time
    plot_speed_over_time(traj, agent_names, agent_colors)

    
    
    # 6. Heatmap comparison (only if we have both types)
    if len(ml_agent_ids) > 0 and len(navmesh_agent_ids) > 0:
        create_comparison_heatmap(traj, ml_agent_ids, navmesh_agent_ids, background_img)
    elif len(ml_agent_ids) > 0:
        print("Skipping comparison heatmap (only ML agents present)")
    else:
        print("Skipping comparison heatmap (only NavMesh agents present)")
    
    return traj, metrics, zone_analysis



if __name__ == "__main__":
    import sys
    
    # Legge il numero della configurazione se passato da terminale
    current_config = sys.argv[1] if len(sys.argv) > 1 else None
    
    traj, metrics, zones = main_analysis(
        trajectory_file="PedPyStats.txt",
        user_file="UserStats.txt",
        blueprint_path="SceneBlueprint.json",
        background_img="BackgroundMap.png",
        config_num=current_config,     # Passa il numero (es. "1")
        save_only_traj=True            # Salta gli altri grafici
    )
    
    # ============================================
    # MODALITÀ 2: CONFRONTO BASELINE vs WARNING ZONE
    # ============================================
    
    # Uncomment per confronto scenari
    '''
    print("\n🔹 Running scenario comparison...")
    comparison, traj_base, traj_wz = compare_scenarios(
        baseline_file="PedPyStats_baseline.txt",
        warningzone_file="PedPyStats_warningzone.txt",
        blueprint_path="SceneBlueprint.json"
    )
    
    if comparison is not None:
        # Save comparison to CSV
        comparison.to_csv("comparison_results.csv", index=False)
        print("✓ Comparison saved to comparison_results.csv")
    '''