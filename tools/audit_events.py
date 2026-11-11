import os
import re

PROJECT_DIR = r"D:\Wonderland Discord\Projects\Taiwu Path of the Gu\Taiwu_Godot"
CORE_LOGIC_DIR = os.path.join(PROJECT_DIR, "Core.Logic")
PROJECT_GODOT = os.path.join(PROJECT_DIR, "project.godot")
WORLD_SIGNALS_FILE = os.path.join(CORE_LOGIC_DIR, "ECS", "WorldSignals.cs")

def get_runtime_cs_files():
    cs_files = []
    for root, dirs, files in os.walk(PROJECT_DIR):
        if any(skip in root for skip in [".godot", "bin", "obj", ".git", "Core.Logic.Tests"]):
            continue
        for f in files:
            if f.endswith(".cs"):
                cs_files.append(os.path.join(root, f))
    return cs_files

def audit_world_signals(cs_files):
    if not os.path.exists(WORLD_SIGNALS_FILE):
        return []

    with open(WORLD_SIGNALS_FILE, "r", encoding="utf-8", errors="ignore") as f:
        ws_content = f.read()

    # 1. Parse all event declarations on WorldSignals
    event_decl_pattern = re.compile(r"public\s+static\s+event\s+([A-Za-z0-9_<>,\s\.\?]+?)\s+([A-Za-z0-9_]+)\s*;")
    events = event_decl_pattern.findall(ws_content)
    event_names = [e[1] for e in events]

    # 2. Parse EmitMethod -> EventName mapping (arrow or block bodies)
    emit_map_pattern = re.compile(
        r"public\s+static\s+void\s+([A-Za-z0-9_]+)\s*\([^)]*\)\s*(?:=>|\{[^}]*?)\s*([A-Za-z0-9_]+)\?\.Invoke",
        re.DOTALL
    )
    emit_mappings = emit_map_pattern.findall(ws_content)

    event_to_emit = {}
    for e_method, evt in emit_mappings:
        event_to_emit.setdefault(evt, []).append(e_method)

    file_contents = {}
    for path in cs_files:
        try:
            with open(path, "r", encoding="utf-8", errors="ignore") as f:
                file_contents[path] = f.read()
        except Exception:
            pass

    audit_results = []
    for evt in event_names:
        subscribers = []
        unsubscribers = []
        publishers = []
        emit_methods = event_to_emit.get(evt, [])

        for path, code in file_contents.items():
            rel_path = os.path.relpath(path, PROJECT_DIR)

            if re.search(rf"\b{evt}\s*\+\=", code):
                subscribers.append(rel_path)
            if re.search(rf"\b{evt}\s*\-\=", code):
                unsubscribers.append(rel_path)

            if os.path.normpath(path) != os.path.normpath(WORLD_SIGNALS_FILE):
                if f"WorldSignals.{evt}" in code and (f"{evt}?.Invoke" in code or f"{evt}(" in code):
                    publishers.append(f"{rel_path} (direct invoke)")

                for e_m in emit_methods:
                    if re.search(rf"\bWorldSignals\.{e_m}\s*\(", code) or re.search(rf"\b{e_m}\s*\(", code):
                        publishers.append(f"{rel_path} ({e_m})")

        audit_results.append({
            "event": evt,
            "emit_methods": emit_methods,
            "subscribers": sorted(set(subscribers)),
            "unsubscribers": sorted(set(unsubscribers)),
            "publishers": sorted(set(publishers)),
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
        except Exception:
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
            "references": sorted(set(references)),
        })
    return action_results

def run_audit():
    cs_files = get_runtime_cs_files()
    event_results = audit_world_signals(cs_files)

    dangling_events = [r for r in event_results if len(r["publishers"]) > 0 and len(r["subscribers"]) == 0]
    unfired_events = [r for r in event_results if len(r["subscribers"]) > 0 and len(r["publishers"]) == 0]
    connected_events = [r for r in event_results if len(r["publishers"]) > 0 and len(r["subscribers"]) > 0]
    dead_events = [r for r in event_results if len(r["publishers"]) == 0 and len(r["subscribers"]) == 0]
    asymmetrical = [r for r in event_results if set(r["subscribers"]) != set(r["unsubscribers"])]

    print("\n" + "="*70)
    print("DANGLING SIGNALS (Published in runtime code, but 0 Listeners!)")
    print("="*70)
    for d in dangling_events:
        print(f"  [X] Signal: {d['event']}")
        print(f"      Publishers: {', '.join(d['publishers'])}")

    print("\n" + "="*70)
    print("ORPHANED LISTENERS (Subscribed in runtime code, but NEVER emitted!)")
    print("="*70)
    for u in unfired_events:
        print(f"  [?] Signal: {u['event']}")
        print(f"      Subscribers: {', '.join(u['subscribers'])}")

    print("\n" + "="*70)
    print("SUBSCRIPTION SYMMETRY CHECK (+= vs -=)")
    print("="*70)
    for s in asymmetrical:
        missing = sorted(set(s["subscribers"]) - set(s["unsubscribers"]))
        extra = sorted(set(s["unsubscribers"]) - set(s["subscribers"]))
        print(f"  [!] Signal: {s['event']} | Missing -=: {missing} | Extra -=: {extra}")

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
    print(f"  Total WorldSignals Definitions: {len(event_results)}")
    print(f"  Connected (Healthy) Signals:    {len(connected_events)}")
    print(f"  Dangling (Dead-End) Signals:    {len(dangling_events)}")
    print(f"  Orphaned (Dead Ear) Signals:    {len(unfired_events)}")
    print(f"  Unused / Stub Signals:          {len(dead_events)}")
    print(f"  Asymmetrical Subscriptions:     {len(asymmetrical)}")
    print(f"  Ghost Input Actions:            {len(ghost_actions)}")
    print("="*70)

if __name__ == "__main__":
    run_audit()
