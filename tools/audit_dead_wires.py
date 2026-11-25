import os
import sys

TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_DIR = os.path.dirname(TOOLS_DIR)
WORLD_SIGNALS_FILE = os.path.join(PROJECT_DIR, "Core.Logic", "ECS", "WorldSignals.cs")

if TOOLS_DIR not in sys.path:
    sys.path.insert(0, TOOLS_DIR)

from audit_events import run_audit

if __name__ == "__main__":
    if not os.path.exists(WORLD_SIGNALS_FILE):
        print(f"ERROR: WorldSignals.cs not found at {WORLD_SIGNALS_FILE}", file=sys.stderr)
        sys.exit(1)
    run_audit()
