import os
import re

PROJECT_DIR = r"D:\Wonderland Discord\Projects\Taiwu Path of the Gu\Taiwu_Godot"
SCRIPTS_DIR = os.path.join(PROJECT_DIR, "Scripts")
CORE_LOGIC_DIR = os.path.join(PROJECT_DIR, "Core.Logic")
PROJECT_GODOT = os.path.join(PROJECT_DIR, "project.godot")
EVENT_MANAGER_FILE = os.path.join(CORE_LOGIC_DIR, "Core", "GameEventManager.cs")

def get_all_cs_files():
    cs_files = []
    for root, dirs, files in os.walk(PROJECT_DIR):
        if any(skip in root for skip in [".godot", "bin", "obj", ".git"]):
            continue
        for f in files:
            if f.endswith(".cs"):
                cs_files.append(os.path.join(root, f))
    return cs_files

def audit_event_manager(cs_files):
    if not os.path.exists(EVENT_MANAGER_FILE):
        return [], []
    
    with open(EVENT_MANAGER_FILE, "r", encoding="utf-8", errors="ignore") as f:
        em_content = f.read()

    # 1. Parse all event declarations
    event_decl_pattern = re.compile(r"public\s+static\s+event\s+([A-Za-z0-9_<>,\s\?]+?)\s+([A-Za-z0-9_]+)\s*;")
    events = event_decl_pattern.findall(em_content)
    event_names = [e[1] for e in events]

    # 2. Parse RaiseMethod -> EventName mapping
    # e.g.: public static void RaiseRequestInitiateRefinement() => OnInitiateRefinementRequested?.Invoke();
    raise_map_pattern = re.compile(r"public\s+static\s+void\s+([A-Za-z0-9_]+)\s*\([^)]*\)\s*=>\s*([A-Za-z0-9_]+)\?\.Invoke")
    raise_mappings = raise_map_pattern.findall(em_content)
    
    # Event to its Raise methods
    event_to_raise = {}
    raise_to_event = {}
    for r_method, evt in raise_mappings:
        raise_to_event[r_method] = evt
        event_to_raise.setdefault(evt, []).append(r_method)

    # Read all CS files content
    file_contents = {}
    for path in cs_files:
        try:
            with open(path, "r", encoding="utf-8", errors="ignore") as f:
                file_contents[path] = f.read()
        except:
            pass

    audit_results = []
    for evt in event_names:
        subscribers = []
        publishers = []
        raise_methods = event_to_raise.get(evt, [])

        for path, code in file_contents.items():
            rel_path = os.path.relpath(path, PROJECT_DIR)
            
            # Check subscriptions (+= evt or .evt +=)
            if re.search(rf"\b{evt}\s*\+\=", code):
                subscribers.append(rel_path)
            
            # Check if event invoked directly outside GameEventManager
            if path != EVENT_MANAGER_FILE:
                if f"GameEventManager.{evt}" in code and (f"{evt}?.Invoke" in code or f"{evt}(" in code):
                    publishers.append(f"{rel_path} (direct invoke)")

                # Check if Trigger method for this event is called
                # e.g.: TriggerEscape() invokes OnEscapeRequested
                trigger_name = "Trigger" + evt.replace("On", "").replace("Requested", "")
                if f"GameEventManager.{trigger_name}" in code or f"{trigger_name}(" in code:
                    publishers.append(f"{rel_path} ({trigger_name})")

                # Check if any Raise method for this event is called
                for r_m in raise_methods:
                    if f"{r_m}(" in code or f"GameEventManager.{r_m}" in code:
                        publishers.append(f"{rel_path} ({r_m})")

        audit_results.append({
            "event": evt,
            "raise_methods": raise_methods,
            "subscribers": list(set(subscribers)),
            "publishers": list(set(publishers))
        })

    return audit_results

def audit_input_actions(cs_files):
    if not os.path.exists(PROJECT_GODOT):
        return []

    with open(PROJECT_GODOT, "r", encoding="utf-8", errors="ignore") as f:
        godot_content = f.read()

    input_section = re.search(r"\[input\](.*?)(?=\n\[|$)", godot_content, re.DOTALL)
    if not input_section:
        return []
    
    actions = re.findall(r"^([a-zA-Z0-9_]+)\=\{", input_section.group(1), re.MULTILINE)
    
    file_contents = {}
    for path in cs_files:
        try:
            with open(path, "r", encoding="utf-8", errors="ignore") as f:
                file_contents[path] = f.read()
        except:
            pass

    action_results = []
    for act in actions:
        references = []
        for path, code in file_contents.items():
            rel_path = os.path.relpath(path, PROJECT_DIR)
            if f'"{act}"' in code or f"'{act}'" in code:
                references.append(rel_path)
        action_results.append({
            "action": act,
            "references": list(set(references))
        })
    return action_results

def run_audit():
    cs_files = get_all_cs_files()
    event_results = audit_event_manager(cs_files)
    
    # 1. Categorize events
    dangling_events = [r for r in event_results if len(r["publishers"]) > 0 and len(r["subscribers"]) == 0]
    unfired_events = [r for r in event_results if len(r["subscribers"]) > 0 and len(r["publishers"]) == 0]
    connected_events = [r for r in event_results if len(r["publishers"]) > 0 and len(r["subscribers"]) > 0]
    dead_events = [r for r in event_results if len(r["publishers"]) == 0 and len(r["subscribers"]) == 0]

    print("\n" + "="*70)
    print("DANGLING EVENTS (Triggered by UI / Input / Code, but 0 Listeners!)")
    print("="*70)
    for d in dangling_events:
        print(f"  [X] Event: {d['event']}")
        print(f"      Publishers: {', '.join(d['publishers'])}")
        print(f"      Subscribers: NONE (Silent Dead-End!)")

    print("\n" + "="*70)
    print("ORPHANED LISTENERS (Subscribed in code, but NEVER fired anywhere!)")
    print("="*70)
    for u in unfired_events:
        print(f"  [?] Event: {u['event']}")
        print(f"      Subscribers: {', '.join(u['subscribers'])}")
        print(f"      Publishers: NONE (Dead Ear!)")

    print("\n" + "="*70)
    print("GHOST INPUT ACTIONS (Configured in project.godot, but 0 C# queries!)")
    print("="*70)
    action_results = audit_input_actions(cs_files)
    ghost_actions = [a for a in action_results if len(a["references"]) == 0]
    for g in ghost_actions:
        print(f"  [X] Action: '{g['action']}' (Dead hotkey!)")

    for a in action_results:
        if len(a["references"]) > 0:
            print(f"  [OK] '{a['action']}' -> {', '.join(a['references'])}")

    print("\n" + "="*70)
    print("AUDIT SUMMARY:")
    print("="*70)
    print(f"  Total Event Bus Definitions: {len(event_results)}")
    print(f"  Connected (Healthy) Events:  {len(connected_events)}")
    print(f"  Dangling (Dead-End) Events:  {len(dangling_events)}")
    print(f"  Orphaned (Dead Ear) Events:  {len(unfired_events)}")
    print(f"  Unused / Stub Events:        {len(dead_events)}")
    print(f"  Ghost Input Actions:         {len(ghost_actions)}")
    print("="*70)

if __name__ == "__main__":
    run_audit()
