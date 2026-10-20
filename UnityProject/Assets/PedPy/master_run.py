"""
master_run.py
Runs the whole simulation pipeline, one configuration at a time:
set_config.py -> Unity (via start_play.txt / sim_done.txt) -> pedPy.py,
then fill_excel.py at the end (also after Ctrl+C).

Usage: python master_run.py [start_config]
"""
import os
import sys
import time

# Configuration range (main grid: 1-1296, integrative test: 1297-1476)
START_CONFIG = 1297
END_CONFIG   = 1476

# Optional start configuration, to resume an interrupted run
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

        # Write config.json
        os.system(f"python set_config.py {config_num}")

        print("Telling Unity to start...")

        # This file triggers AutoPlayManager in Unity
        with open("start_play.txt", "w") as f:
            f.write("go")

        print("Waiting for the simulation to end...")

        # Wait until Unity writes sim_done.txt
        while not os.path.exists("sim_done.txt"):
            time.sleep(0.5)

        # Remove it for the next configuration
        os.remove("sim_done.txt")

        print("Simulation ended. Generating graphs...")

        # Run the analysis and save the trajectory plot
        os.system(f"python pedPy.py {config_num}")

except KeyboardInterrupt:
    print("\n\nExecution interrupted manually")

finally:
    print("\n")
    print("Excel compilation in progress...")
    os.system("python fill_excel.py")
    print("Done")