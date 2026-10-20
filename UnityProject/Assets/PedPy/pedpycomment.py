'''
#for full comparison

present_ids = traj.data["id"].unique().tolist()

ml_pure_ids = present_ids[:1]
ml_hybrid_ids = present_ids[1:2]
navmesh_ids = present_ids[2:]

agent_names = {}

for i, aid in enumerate(ml_pure_ids):
    agent_names[aid] = f"ML-Agent Puro {i+1}"

for i, aid in enumerate(ml_hybrid_ids):
    agent_names[aid] = f"ML-Agent Ibrido {i+1}"

for i, aid in enumerate(navmesh_ids):
    agent_names[aid] = f"NavMeshAgent {i+1}"

ml_pure_colors = plt.cm.Oranges(np.linspace(0.6, 0.9, len(ml_pure_ids)))
ml_hybrid_colors = plt.cm.magma(np.linspace(0.6, 0.9, len(ml_hybrid_ids)))
navmesh_colors = plt.cm.Blues(np.linspace(0.6, 0.9, len(navmesh_ids)))

agent_colors = {}

pure_idx = hybrid_idx = nav_idx = 0

for agent_id in present_ids:
    agent_traj = traj.data[traj.data["id"] == agent_id]
    if agent_id in ml_pure_ids:
        label = agent_names[agent_id]
        color = ml_pure_colors[pure_idx]
        pure_idx += 1

    elif agent_id in ml_hybrid_ids:
        label = agent_names[agent_id]
        color = ml_hybrid_colors[hybrid_idx]
        hybrid_idx += 1

    elif agent_id in navmesh_ids:
        label = agent_names[agent_id]
        color = navmesh_colors[nav_idx]
        nav_idx += 1

    else:
        label = f"Unknown {agent_id}"
        color = "gray"

    agent_colors[label] = color
    ax.plot(agent_traj["x"], agent_traj["y"], color=color, label=label)


#for ML-agent pure and hybrid

present_ids = traj.data["id"].unique().tolist()

ml_pure_ids = present_ids[:1]        # Primo agente = ML Puro
ml_hybrid_ids = present_ids[1:]      # Altri agenti = ML Ibrido

agent_names = {}
for i, aid in enumerate(ml_pure_ids):
    agent_names[aid] = f"ML-Agent Puro {i+1}"
for i, aid in enumerate(ml_hybrid_ids):
    agent_names[aid] = f"ML-Agent Ibrido {i+1}"

ml_pure_colors = plt.cm.Reds(np.linspace(0.5, 0.9, len(ml_pure_ids)))
ml_hybrid_colors = plt.cm.magma(np.linspace(0.5, 0.9, len(ml_hybrid_ids)))

pure_idx = 0
hybrid_idx = 0
agent_colors = {}

for agent_id in present_ids:
    agent_traj = traj.data[traj.data["id"] == agent_id]
    
    if agent_id in ml_pure_ids:
        color = ml_pure_colors[pure_idx]
        pure_idx += 1
        label = f"ML-Agent Puro {pure_idx}"
    elif agent_id in ml_hybrid_ids:
        color = ml_hybrid_colors[hybrid_idx]
        hybrid_idx += 1
        label = f"ML-Agent Ibrido {hybrid_idx}"
    else:
        color = 'gray'
        label = f"Unknown {agent_id}"

    agent_colors[label] = color
    ax.plot(agent_traj["x"], agent_traj["y"], color=color, label=label)
'''

'''  
#for full comparison
metrics["name"] = metrics["id"].apply(
    lambda x:
        f"ML-Agent Puro {ml_pure_ids.index(x) + 1}" if x in ml_pure_ids else
        (f"ML-Agent Ibrido {ml_hybrid_ids.index(x) + 1}" if x in ml_hybrid_ids else
         (f"NavMeshAgent {navmesh_ids.index(x) + 1}" if x in navmesh_ids else str(x)))
)




#for ML-agent pure and hybrid
metrics["name"] = metrics["id"].apply(lambda x: 
    f"ML-Agent Puro {list(ml_pure_ids).index(x) + 1}" if x in ml_pure_ids else 
    (f"ML-Agent Ibrido {list(ml_hybrid_ids).index(x) + 1}" if x in ml_hybrid_ids else str(x))
)

'''

'''
#for full comparison

def create_comparison_heatmap(traj, pure_ids, hybrid_ids, navmesh_ids, grid_size=100):

    all_ids = pure_ids + hybrid_ids + navmesh_ids
    all_data = traj.data[traj.data["id"].isin(all_ids)]

    if len(all_data) == 0:
        print("Nessun dato disponibile per la heatmap.")
        return

    H_global, _, _ = np.histogram2d(
        all_data["x"], all_data["y"],
        bins=grid_size,
        range=[[-1750, 1750], [-1750, 1750]]
    )

    nonzero = H_global[H_global > 0]
    vmax = np.percentile(nonzero, 99) if len(nonzero) > 0 else 1

    fig, axes = plt.subplots(1, 3, figsize=(24, 8), constrained_layout=True)
    img = plt.imread("BackgroundMap.png")

    categories = [
        (pure_ids, "ML-Agent Puro Coverage"),
        (hybrid_ids, "ML-Agent Ibrido Coverage"),
        (navmesh_ids, "NavMeshAgent Coverage")
    ]

    last_hist = None

    for ax, (ids, title) in zip(axes, categories):

        data = traj.data[traj.data["id"].isin(ids)]

        ax.imshow(img, zorder=0, extent=[-1750, 1750, -1750, 1750], alpha=0.5)

        if len(data) > 0:
            last_hist = ax.hist2d(
                data["x"], data["y"],
                bins=grid_size,
                range=[[-1750, 1750], [-1750, 1750]],
                cmap='inferno',
                alpha=0.75,
                cmin=1,
                vmin=1,
                vmax=vmax
            )

        ax.set_title(title, fontsize=14, fontweight='bold')
        ax.axis('off')

    if last_hist is not None:
        fig.colorbar(last_hist[3], ax=axes, label="Frequenza di Passaggio")

    plt.show()

create_comparison_heatmap(traj, ml_pure_ids, ml_hybrid_ids, navmesh_ids)


#for ML-agent pure and hybrid

def create_comparison_heatmap(traj, pure_ids, hybrid_ids, grid_size=100):
    
    # Calcola range globale
    all_data = traj.data[traj.data["id"].isin(pure_ids + hybrid_ids)]
    H_global, _, _ = np.histogram2d(
        all_data["x"], all_data["y"], 
        bins=grid_size, 
        range=[[-1750, 1750], [-1750, 1750]]
    )
    vmax = np.percentile(H_global[H_global > 0], 98.8)
    
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(20, 10), constrained_layout=True)
    
    img = plt.imread("BackgroundMap.png")
    
    # ML-Agent Puro heatmap
    pure_data = traj.data[traj.data["id"].isin(pure_ids)]
    ax1.imshow(img, zorder=0, extent=[-1750, 1750, -1750, 1750], alpha=0.5)
    h1 = ax1.hist2d(pure_data["x"], pure_data["y"], bins=grid_size,
                    range=[[-1750, 1750], [-1750, 1750]],
                    cmap='inferno', alpha=0.7, cmin=1, vmin=1, vmax=vmax)
    ax1.set_title("ML-Agent Puro Coverage", fontsize=14, fontweight='bold')
    ax1.axis('off')
    
    # ML-Agent Ibrido heatmap
    hybrid_data = traj.data[traj.data["id"].isin(hybrid_ids)]
    ax2.imshow(img, zorder=0, extent=[-1750, 1750, -1750, 1750], alpha=0.5)
    h2 = ax2.hist2d(hybrid_data["x"], hybrid_data["y"], bins=grid_size,
                    range=[[-1750, 1750], [-1750, 1750]],
                    cmap='inferno', alpha=0.7, cmin=1, vmin=1, vmax=vmax)
    ax2.set_title("ML-Agent Ibrido Coverage", fontsize=14, fontweight='bold')
    ax2.axis('off')
    
    # Colorbar condivisa
    fig.colorbar(h2[3], ax=[ax1, ax2], label="Frequenza di Passaggio")
    
    plt.show()

# Chiamata aggiornata
create_comparison_heatmap(traj, ml_pure_ids, ml_hybrid_ids)

'''
import pedpy
from pedpy import load_trajectory, WalkableArea
import matplotlib.pyplot as plt
import matplotlib.patches as patches
from matplotlib.transforms import Affine2D
import pathlib
import pandas as pd
import numpy as np
import seaborn as sns
import json


def draw_scene_blueprint(ax, blueprint_path="SceneBlueprint.json"):
    path = pathlib.Path(blueprint_path)
    if not path.exists():
        print(f"Blueprint non trovato in {path.absolute()}.")
        return

    with open(path, 'r') as f:
        data = json.load(f)
        elements = data.get("Items", [])

    SCALE = 50

    for el in elements:
        # 1. Moltiplica coordinate per 50
        x_center = el['x'] * SCALE
        y_center = el['y'] * SCALE
        
        # 2. Moltiplica dimensioni per 50
        width = el.get('size_x', 1.0) * SCALE
        height = el.get('size_y', 1.0) * SCALE
        
        # 3. Disegno partendo dall'angolo inferiore sinistro
        bottom_left_x = x_center - (width / 2)
        bottom_left_y = y_center - (height / 2)
        
        color = 'blue' if el['type'] == "Objective" else 'hotpink'
        rotation_deg = -el.get('rotation', 0.0)
        
        # 4. Crea rettangolo e applica la rotazione corretta
        rect = patches.Rectangle((bottom_left_x, bottom_left_y), 
                                 width, height, 
                                 color=color, alpha=0.5)
        
        t = Affine2D().rotate_deg_around(x_center, y_center, rotation_deg)
        rect.set_transform(t + ax.transData)
        ax.add_patch(rect)


def preprocess_file(file_path, clean_path):
    # Load using pandas
    df = pd.read_csv(file_path, sep="\t", header=None, thousands=",")
    # Save in standard format without commas
    df.to_csv(clean_path, sep="\t", header=False, index=False)

# Preprocess PedPyStats.txt -> PedPyStats_clean.txt
preprocess_file("PedPyStats.txt", "PedPyStats_clean.txt")
preprocess_file("UserStats.txt", "UserStats_clean.txt")



# For agents
traj = load_trajectory(
    trajectory_file=pathlib.Path("PedPyStats_clean.txt"),
    default_frame_rate=100,
    default_unit=1
)

# For user
trajUser = load_trajectory(
    trajectory_file=pathlib.Path("UserStats_clean.txt"),
    default_frame_rate=100,
    default_unit=1
)

walkable_area = WalkableArea(
    [
        (1750, -1750),
        (1750, 1750),
        (-1750, 1750),
        (-1750, -1750),
    ])

fig, ax = plt.subplots()

# Plot background map
img = plt.imread("BackgroundMap.png")
ax.imshow(img, zorder=0, extent=[-1750, 1750, -1750, 1750])



    
#for NavMesh and ML-Agent
# 1. Recupera automaticamente tutti gli ID unici trovati nel file
present_ids = traj.data["id"].unique().tolist()
#print(f"ID rilevati nel file: {present_ids}")

# Assegna gli ID in ordine di apparizione o numerico
ml_agent_ids = present_ids[:1]      
navmesh_agent_ids = present_ids[1:] 

# Crea i nomi dinamicamente per la legenda
agent_names = {}
for i, aid in enumerate(ml_agent_ids):
    agent_names[aid] = f"ML-Agent {i+1}"
for i, aid in enumerate(navmesh_agent_ids):
    agent_names[aid] = f"NavMesh {i+1}"

# Colori per tipologia
# Genera 10 sfumature di rosso per gli ML e 10 di blu per i NavMesh
ml_colors = plt.cm.Reds(np.linspace(0.4, 1, len(ml_agent_ids)))
navmesh_colors = plt.cm.Blues(np.linspace(0.4, 1, len(navmesh_agent_ids)))

ml_idx = 0
navmesh_idx = 0

agent_colors = {}

for agent_id in present_ids:
    agent_traj = traj.data[traj.data["id"] == agent_id]
    
    if agent_id in ml_agent_ids:
        color = ml_colors[ml_idx]
        ml_idx += 1
        label = f"ML-Agent {ml_idx}"
    elif agent_id in navmesh_agent_ids:
        color = navmesh_colors[navmesh_idx]
        navmesh_idx += 1
        label = f"NavMesh {navmesh_idx}"
    else:
        color = 'gray'
        label = f"Unknown {agent_id}"

    agent_colors[label] = color

    ax.plot(agent_traj["x"], agent_traj["y"], color=color, label=label)


draw_scene_blueprint(ax, "SceneBlueprint.json")
ax.set_aspect("equal")
ax.legend()
plt.show()


def compute_agent_metrics(traj, agent_names=None):
    metrics = []
    for agent_id in traj.data["id"].unique():
        agent_traj = traj.data[traj.data["id"] == agent_id].sort_values("frame")
        x, y, frames = agent_traj["x"].values, agent_traj["y"].values, agent_traj["frame"].values
        
        dist = np.sum(np.sqrt(np.diff(x)**2 + np.diff(y)**2))

        # duration (in seconds)
        duration = (frames[-1] - frames[0]) / traj.frame_rate

        # average speed
        avg_speed = dist / duration if duration > 0 else 0
        
        metrics.append({
            "id": agent_id,
            "name": agent_names[agent_id] if agent_names and agent_id in agent_names else str(agent_id),
            "duration": duration,
            "distance": dist,
            "avg_speed": avg_speed
        })
    return pd.DataFrame(metrics)

metrics = compute_agent_metrics(traj, agent_names)


def draw_table_image(df):

    df_plot = df.round(2)
    
    fig, ax = plt.subplots(figsize=(10, 4)) 
    ax.axis('tight')
    ax.axis('off')
    
    # Creazione della tabella grafica
    the_table = ax.table(cellText=df_plot.values, 
                        colLabels=df_plot.columns, 
                        cellLoc='center', 
                        loc='center')
    
    the_table.auto_set_font_size(False)
    the_table.set_fontsize(12)
    the_table.scale(1.2, 1.8) 
    
    plt.title("Tabella Riassuntiva Metriche Agenti", pad=20)
    plt.show()

draw_table_image(metrics)


def plot_metric_with_values(data, x, y, title, ylabel, custom_palette):
    plt.figure(figsize=(8, 5))
    sns.set_style("whitegrid")
    
    ax = sns.barplot(data=data, x=x, y=y, hue=x, palette=custom_palette, legend=False)
    
    for i in ax.containers:
        ax.bar_label(i, fmt='%.1f', padding=3, fontsize=10, fontweight='bold')
        
    plt.title(title, fontsize=14, fontweight='bold')
    plt.xlabel("") 
    plt.ylabel(ylabel, fontsize=12)
    plt.ylim(0, data[y].max() * 1.15) 
    plt.tight_layout()
    plt.show()

# Ricalcola le metriche
metrics = compute_agent_metrics(traj, agent_names=None)


#for NavMesh and ML-Agent

metrics["name"] = metrics["id"].apply(lambda x: 
    f"ML-Agent {list(ml_agent_ids).index(x) + 1}" if x in ml_agent_ids else 
    (f"NavMesh {list(navmesh_agent_ids).index(x) + 1}" if x in navmesh_agent_ids else str(x))
)

# Chiama i grafici passando 'agent_colors'
plot_metric_with_values(metrics, "name", "duration", "Durata Traiettoria", "Secondi (s)", agent_colors)
plot_metric_with_values(metrics, "name", "distance", "Distanza Percorsa", "Unità", agent_colors)
plot_metric_with_values(metrics, "name", "avg_speed", "Velocità Media", "Unità/s", agent_colors)


#for NavMesh and ML-Agent
def create_comparison_heatmap(traj, ml_ids, navmesh_ids, grid_size=100):
    
    # Calcola range globale
    all_data = traj.data[traj.data["id"].isin(ml_ids + navmesh_ids)]
    H_global, _, _ = np.histogram2d(
        all_data["x"], all_data["y"], 
        bins=grid_size, 
        range=[[-1750, 1750], [-1750, 1750]]
    )
    vmax = np.percentile(H_global[H_global > 0], 98.8)
    
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(20, 10), constrained_layout=True)
    
    img = plt.imread("BackgroundMap.png")
    
    # ML-Agent heatmap
    ml_data = traj.data[traj.data["id"].isin(ml_ids)]
    ax1.imshow(img, zorder=0, extent=[-1750, 1750, -1750, 1750], alpha=0.5)
    h1 = ax1.hist2d(ml_data["x"], ml_data["y"], bins=grid_size,
                    range=[[-1750, 1750], [-1750, 1750]],
                    cmap='inferno', alpha=0.7, cmin=1, vmin=1, vmax=vmax)
    ax1.set_title("ML-Agent Coverage", fontsize=14, fontweight='bold')
    ax1.axis('off')
    
    # NavMesh heatmap
    nm_data = traj.data[traj.data["id"].isin(navmesh_ids)]
    ax2.imshow(img, zorder=0, extent=[-1750, 1750, -1750, 1750], alpha=0.5)
    h2 = ax2.hist2d(nm_data["x"], nm_data["y"], bins=grid_size,
                    range=[[-1750, 1750], [-1750, 1750]],
                    cmap='inferno', alpha=0.7, cmin=1, vmax=vmax)
    ax2.set_title("NavMesh Coverage", fontsize=14, fontweight='bold')
    ax2.axis('off')
    
    # Colorbar condivisa
    fig.colorbar(h2[3], ax=[ax1, ax2], label="Frequenza di Passaggio")
    
    plt.show()

create_comparison_heatmap(traj, ml_agent_ids, navmesh_agent_ids)

def plot_speed_over_time(traj, agent_names, custom_colors):
    
    plt.figure(figsize=(12, 6))
    sns.set_style("whitegrid")

    for agent_id in traj.data["id"].unique():
        agent_traj = traj.data[traj.data["id"] == agent_id].sort_values("frame")
        x, y, frames = agent_traj["x"].values, agent_traj["y"].values, agent_traj["frame"].values
        
        # Calcolo velocità istantanea
        dx = np.diff(x)
        dy = np.diff(y)
        dt = np.diff(frames) / traj.frame_rate
        # Evita divisioni per zero
        with np.errstate(divide='ignore', invalid='ignore'):
            raw_speed = np.sqrt(dx**2 + dy**2) / dt
            raw_speed = np.nan_to_num(raw_speed) # Rimpiazza eventuali NaN con 0
        
        # Smoothing (Media Mobile)
        speed_series = pd.Series(raw_speed)
        smooth_speed = speed_series.rolling(window=30, min_periods=1).mean()
        
        # Tempo normalizzato
        time = (frames[1:] - frames[0]) / traj.frame_rate
        
        label = agent_names.get(agent_id, str(agent_id))
        
        color = custom_colors.get(label, 'gray')
        
        smooth_speed = smooth_speed.clip(upper=150)  # Limita valori a max 150

        plt.plot(time, smooth_speed, label=label, color=color, alpha=0.8, linewidth=2)
    
    plt.xlabel("Tempo (s)", fontsize=12)
    plt.ylabel("Velocità (unità/s)", fontsize=12)
    plt.title("Profilo di Velocità (Smoothed)", fontsize=14, fontweight='bold')
    plt.legend(fontsize=10)
    plt.grid(True, alpha=0.3)
    plt.tight_layout()
    plt.show()
    
plot_speed_over_time(traj, agent_names, agent_colors)

