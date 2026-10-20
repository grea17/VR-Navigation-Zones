import os
import sys
import time

# Valori di default
START_CONFIG = 1297 #1  
END_CONFIG   = 1476 #1296 

# Ripresa da dove ci si era fermati
if len(sys.argv) > 1:
    try:
        START_CONFIG = int(sys.argv[1])
    except ValueError:
        print("Error: You must enter an integer for configuration.")
        sys.exit(1)

print(f"Starting simulations from {START_CONFIG} to {END_CONFIG}")

try:
    for config_num in range(START_CONFIG, END_CONFIG + 1):
        print(f"\n{'='*50}")
        print(f"CONFIGURATION {config_num}/{END_CONFIG}")
        print(f"{'='*50}")
        
        # Scrivi config.json
        os.system(f"python set_config.py {config_num}")
        
        # Leggi config.json per mostrare info utili
        import json
        try:
            with open("config.json") as f:
                cfg = json.load(f)
            #print(f"Scena:    {cfg['sceneName']} | Angular: {cfg['angularSpeed']} | WarnMult: {cfg['warningSpeedMultiplier']}")
        except:
            pass
        
	# Salta configurazioni con tzRadius troppo piccolo
        #if cfg.get('tzRadius', 0) != 0.75:
            #print(f" tzRadius={cfg['tzRadius']} — configurazione saltata")
            #continue

        print("Telling Unity to start...")
        
        # Crea il file che fa scattare l'AutoPlayManager in Unity
        with open("start_play.txt", "w") as f:
            f.write("go")
        
        print("Waiting for the simulation to end...")
        
        # Continua a girare in tondo finché Unity non crea il file "sim_done.txt"
        while not os.path.exists("sim_done.txt"):
            time.sleep(0.5)
            
        # Unity ha finito e ha creato il file! Lo eliminiamo per il prossimo giro.
        os.remove("sim_done.txt")
        
        print("Simulation ended. Generating graphs...")
        
        # Lancia l'analisi e salva il grafico
        os.system(f"python pedPy.py {config_num}")

except KeyboardInterrupt:
    print("\n\nExecution interrupted manually")

finally:
    print("\n")
    print("Excel compilation in progress...")
    os.system("python fill_excel.py")
    print("Done")