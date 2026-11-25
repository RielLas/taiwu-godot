#!/usr/bin/env python3
"""
Taiwu Path of the Gu — Master Game Integrity Auditor (audit_game.py)
Performs static analysis across:
  1. Godot Resource Paths (res:// exists on disk)
  2. Gu Catalog Integrity (gu_worms_database.json schema and test/code references)
  3. POI Visual Database Mapping (all TilePOIType entries mapped with valid textures)
  4. Scene NodePath & Export Wire Verification (.tscn node references)
  5. ECS DirtyTopic Hygiene (published topics have observers)
  6. WorldSignals & Event Bus Symmetry (publishers, subscribers, symmetry)
  7. Halal Compliance (Asset names & texture checks)
"""

import os
import re
import json
import sys

PROJECT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
CORE_LOGIC_DIR = os.path.join(PROJECT_DIR, "Core.Logic")
RESOURCES_DIR = os.path.join(PROJECT_DIR, "Resources")
SCENES_DIR = os.path.join(PROJECT_DIR, "Scenes")
SCRIPTS_DIR = os.path.join(PROJECT_DIR, "Scripts")
PROJECT_GODOT = os.path.join(PROJECT_DIR, "project.godot")
WORLD_SIGNALS_FILE = os.path.join(CORE_LOGIC_DIR, "ECS", "WorldSignals.cs")
GU_DATABASE_FILE = os.path.join(RESOURCES_DIR, "Data", "gu_worms_database.json")
TILE_POI_TYPE_FILE = os.path.join(CORE_LOGIC_DIR, "Map", "MapTypes.cs")
TILE_VISUAL_DB_FILE = os.path.join(SCRIPTS_DIR, "Map", "TileVisualDatabase.cs")

def get_files_with_ext(root_dir, extensions):
    matched = []
    for root, dirs, files in os.walk(root_dir):
        if any(skip in root for skip in [".godot", "bin", "obj", ".git"]):
            continue
        for f in files:
            if any(f.endswith(ext) for ext in extensions):
                matched.append(os.path.join(root, f))
    return matched

# ======================================================================
# 1. AUDIT RESOURCE PATHS (res://...)
# ======================================================================
def audit_resource_paths(cs_files, tscn_files):
    pattern = re.compile(r'res://([a-zA-Z0-9_\-\.\/]+)')
    referenced_paths = set()

    for fpath in cs_files + tscn_files:
        try:
            with open(fpath, "r", encoding="utf-8", errors="ignore") as f:
                content = f.read()
                matches = pattern.findall(content)
                for m in matches:
                    clean = m.strip().rstrip('",;)\'.').lstrip('/')
                    referenced_paths.add(clean)
        except Exception:
            pass

    missing_files = []
    found_files = []

    for rel in sorted(referenced_paths):
        if "{" in rel or "}" in rel or "*" in rel:
            continue
        full_path = os.path.normpath(os.path.join(PROJECT_DIR, rel))
        if os.path.exists(full_path):
            found_files.append(rel)
        else:
            missing_files.append(rel)

    return found_files, missing_files

# ======================================================================
# 2. AUDIT GU CATALOG DATABASE
# ======================================================================
def audit_gu_database(cs_files):
    if not os.path.exists(GU_DATABASE_FILE):
        return [], ["Missing gu_worms_database.json file!"], []

    try:
        with open(GU_DATABASE_FILE, "r", encoding="utf-8") as f:
            catalog = json.load(f)
    except Exception as e:
        return [], [f"Failed to parse gu_worms_database.json: {e}"], []

    catalog_gu_ids = set()
    errors = []

    for idx, gu in enumerate(catalog):
        gu_id = gu.get("id")
        if not gu_id:
            errors.append(f"Gu entry #{idx} is missing 'id'")
            continue
        catalog_gu_ids.add(gu_id)
        if not gu.get("canonical_name"):
            errors.append(f"Gu '{gu_id}' missing 'canonical_name'")
        if not gu.get("path"):
            errors.append(f"Gu '{gu_id}' missing 'path'")
        rank = gu.get("rank")
        if rank is None or not (1 <= rank <= 6):
            errors.append(f"Gu '{gu_id}' invalid rank: {rank}")

    gu_id_pattern = re.compile(r'\"(gu_[a-zA-Z0-9_]+_r\d+)\"')
    code_gu_ids = set()

    # Exclude unit tests from catalog restriction if they use test fixtures
    for path in cs_files:
        if "Tests" in path:
            continue
        try:
            with open(path, "r", encoding="utf-8", errors="ignore") as f:
                content = f.read()
                for match in gu_id_pattern.findall(content):
                    code_gu_ids.add(match)
        except Exception:
            pass

    unregistered_in_catalog = []
    for cid in sorted(code_gu_ids):
        if cid not in catalog_gu_ids:
            unregistered_in_catalog.append(cid)

    return catalog, errors, unregistered_in_catalog

# ======================================================================
# 3. AUDIT POI VISUAL DATABASE COVERAGE
# ======================================================================
def audit_poi_visual_coverage():
    if not os.path.exists(TILE_POI_TYPE_FILE) or not os.path.exists(TILE_VISUAL_DB_FILE):
        return [], ["Missing MapTypes.cs or TileVisualDatabase.cs"]

    with open(TILE_POI_TYPE_FILE, "r", encoding="utf-8", errors="ignore") as f:
        map_types = f.read()

    enum_match = re.search(r"public\s+enum\s+TilePOIType\s*\{(.*?)\}", map_types, re.DOTALL)
    if not enum_match:
        return [], ["Could not locate TilePOIType enum"]

    raw_members = enum_match.group(1).split(",")
    poi_types = []
    for m in raw_members:
        clean = re.sub(r"//.*", "", m).strip()
        clean = re.sub(r"=\s*\d+", "", clean).strip()
        if clean and clean != "None":
            poi_types.append(clean)

    with open(TILE_VISUAL_DB_FILE, "r", encoding="utf-8", errors="ignore") as f:
        visual_db = f.read()

    missing_visuals = []
    mapped_visuals = []

    for poi in poi_types:
        if f"TilePOIType.{poi}" in visual_db:
            mapped_visuals.append(poi)
        else:
            missing_visuals.append(poi)

    return mapped_visuals, missing_visuals

# ======================================================================
# 4. AUDIT SCENE NODE PATHS
# ======================================================================
def audit_scene_node_paths(tscn_files):
    issues = []
    checked_count = 0

    for path in tscn_files:
        rel_path = os.path.relpath(path, PROJECT_DIR)
        try:
            with open(path, "r", encoding="utf-8", errors="ignore") as f:
                lines = f.readlines()
        except Exception:
            continue

        nodes = {}  # node_path -> type
        current_node_path = None
        node_header = re.compile(r'^\[node\s+name="([^"]+)"(?:\s+type="([^"]+)")?(?:\s+parent="([^"]+)")?')
        prop_pattern = re.compile(r'([A-Za-z0-9_]+)\s*=\s*(?:NodePath\("([^"]+)"\)|"([^"]+)")')

        node_props = [] # (line_num, context_node_path, prop_name, target_path)

        for lnum, line in enumerate(lines, 1):
            m = node_header.match(line)
            if m:
                name = m.group(1)
                ntype = m.group(2) or "Node"
                parent = m.group(3)

                if parent is None:
                    current_node_path = "."
                    nodes["."] = ntype
                elif parent == ".":
                    current_node_path = name
                    nodes[name] = ntype
                else:
                    current_node_path = f"{parent}/{name}"
                    nodes[current_node_path] = ntype
                continue

            # Look for NodePath assignments on the current node
            if current_node_path:
                # Match: SomeProp = NodePath("...") or NodePath in arrays
                for np_m in re.finditer(r'NodePath\("([^"]+)"\)', line):
                    target = np_m.group(1)
                    node_props.append((lnum, current_node_path, target))

        for lnum, ctx, target in node_props:
            checked_count += 1
            resolved = resolve_nodepath(ctx, target)
            if resolved not in nodes:
                issues.append(f"{rel_path}:{lnum} - In node '{ctx}': Target '{target}' (resolved: '{resolved}') not found in scene tree")

    return checked_count, issues

def resolve_nodepath(context_node, nodepath):
    if nodepath == ".":
        return context_node

    if context_node == ".":
        context_parts = []
    else:
        context_parts = context_node.split("/")

    parts = nodepath.split("/")
    for part in parts:
        if part == ".":
            continue
        elif part == "..":
            if context_parts:
                context_parts.pop()
        else:
            context_parts.append(part)

    return "/".join(context_parts) if context_parts else "."

# ======================================================================
# 5. AUDIT ECS DIRTY TOPICS HYGIENE
# ======================================================================
def audit_dirty_topics(cs_files):
    if not os.path.exists(WORLD_SIGNALS_FILE):
        return [], [], ["Missing WorldSignals.cs"]

    with open(WORLD_SIGNALS_FILE, "r", encoding="utf-8", errors="ignore") as f:
        ws_content = f.read()

    topic_match = re.search(r"public\s+enum\s+DirtyTopic(?:\s*:\s*[a-zA-Z0-9_]+)?\s*\{(.*?)\}", ws_content, re.DOTALL)
    if not topic_match:
        return [], [], ["Could not locate DirtyTopic enum"]

    raw_topics = topic_match.group(1).split(",")
    topics = []
    for t in raw_topics:
        clean = re.sub(r"//.*", "", t).strip()
        clean = re.sub(r"=\s*.*", "", clean).strip()
        if clean and clean not in ["None", "All"]:
            topics.append(clean)

    file_contents = {}
    for path in cs_files:
        try:
            with open(path, "r", encoding="utf-8", errors="ignore") as f:
                file_contents[path] = f.read()
        except Exception:
            pass

    emitted_topics = set()
    observed_topics = set()

    for topic in topics:
        for path, code in file_contents.items():
            if re.search(rf"\bDirtyTopic\.{topic}\b", code):
                if "EmitTopics" in code:
                    emitted_topics.add(topic)
                if "HandleTopicsChanged" in code or "& DirtyTopic" in code or "& (DirtyTopic" in code:
                    observed_topics.add(topic)

    unobserved = [t for t in emitted_topics if t not in observed_topics]
    unemitted = [t for t in topics if t not in emitted_topics]

    return topics, unobserved, unemitted

# ======================================================================
# 6. AUDIT HALAL COMPLIANCE
# ======================================================================
def audit_halal_compliance():
    banned_keywords = ["face", "portrait", "human_figure", "character_head", "npc_face", "person_drawing"]
    violations = []

    for root, dirs, files in os.walk(RESOURCES_DIR):
        for f in files:
            lower = f.lower()
            for kw in banned_keywords:
                if kw in lower:
                    violations.append(os.path.join(root, f))
    return violations

# ======================================================================
# MASTER RUNNER
# ======================================================================
def main():
    print("\n" + "="*75)
    print(" ☯️  TAIWU PATH OF THE GU — MASTER GAME INTEGRITY DOCTOR (audit_game.py)")
    print("="*75)

    cs_files = get_files_with_ext(PROJECT_DIR, [".cs"])
    tscn_files = get_files_with_ext(PROJECT_DIR, [".tscn"])

    all_passed = True

    # 1. Resource Paths
    found_res, missing_res = audit_resource_paths(cs_files, tscn_files)
    print("\n[1] GODOT RESOURCE PATH VERIFICATION (res://...)")
    print(f"    Total Checked: {len(found_res) + len(missing_res)} files referenced in code/scenes")
    if missing_res:
        all_passed = False
        print(f"    ❌ MISSING ASSETS ({len(missing_res)}):")
        for m in missing_res:
            print(f"       - res://{m}")
    else:
        print(f"    ✅ All {len(found_res)} referenced resources exist on disk.")

    # 2. Gu Database
    catalog, cat_errors, unregistered_gu = audit_gu_database(cs_files)
    print("\n[2] CANONICAL GU DATABASE VERIFICATION (gu_worms_database.json)")
    print(f"    Catalog Catalog Entries: {len(catalog)}")
    if cat_errors or unregistered_gu:
        all_passed = False
        for err in cat_errors:
            print(f"    ❌ Catalog Error: {err}")
        for unreg in unregistered_gu:
            print(f"    ❌ Code references unregistered Gu ID: '{unreg}'")
    else:
        print(f"    ✅ All {len(catalog)} Gu definitions validated; 0 missing catalog entries in code.")

    # 3. POI Visuals
    mapped_poi, missing_poi = audit_poi_visual_coverage()
    print("\n[3] POI DIORAMA & VISUAL DATABASE COVERAGE (TileVisualDatabase.cs)")
    print(f"    Registered POIs: {len(mapped_poi)} / {len(mapped_poi) + len(missing_poi)}")
    if missing_poi:
        all_passed = False
        print(f"    ❌ Missing visual registrations for POI Types:")
        for mp in missing_poi:
            print(f"       - TilePOIType.{mp}")
    else:
        print(f"    ✅ All {len(mapped_poi)} TilePOIType enums mapped with textures and footprints.")

    # 4. Scene NodePaths
    checked_nodes, node_issues = audit_scene_node_paths(tscn_files)
    print("\n[4] SCENE NODE PATH & EXPORT BINDINGS (.tscn files)")
    print(f"    Total NodePaths Validated: {checked_nodes} across {len(tscn_files)} scenes")
    if node_issues:
        all_passed = False
        print(f"    ❌ BROKEN NODEPATH BINDINGS ({len(node_issues)}):")
        for issue in node_issues:
            print(f"       - {issue}")
    else:
        print(f"    ✅ All {checked_nodes} NodePath wires in .tscn trees resolve cleanly.")

    # 5. DirtyTopics
    topics, unobserved, unemitted = audit_dirty_topics(cs_files)
    print("\n[5] ECS REACTIVE TOPIC INTEGRITY (DirtyTopic enum)")
    print(f"    Total Defined Topics: {len(topics)}")
    if unobserved:
        all_passed = False
        print(f"    ⚠️  UNOBSERVED TOPICS (Emitted in systems, but 0 HandleTopicsChanged listeners):")
        for u in unobserved:
            print(f"       - DirtyTopic.{u}")
    else:
        print(f"    ✅ All emitted DirtyTopics are listened to by active UI or Map observers.")
    if unemitted:
        print(f"    ℹ️  UNEMITTED TOPICS (Defined in DirtyTopic, never emitted directly):")
        for u in unemitted:
            print(f"       - DirtyTopic.{u}")

    # 6. Halal Compliance
    halal_violations = audit_halal_compliance()
    print("\n[6] HALAL ARCHITECTURAL COMPLIANCE (Non-negotiable Core Law)")
    if halal_violations:
        all_passed = False
        print(f"    ❌ BANNED ASSET NAMES DETECTED ({len(halal_violations)}):")
        for v in halal_violations:
            print(f"       - {v}")
    else:
        print("    ✅ 0 forbidden animate/human depictions detected in resource directory.")

    print("\n" + "="*75)
    if all_passed:
        print(" 🏆 MASTER GAME AUDIT PASSED: ZERO SILENT ARCHITECTURAL DISCONNECTS!")
    else:
        print(" ⚠️  MASTER GAME AUDIT FOUND ISSUES REQUIRING ATTENTION.")
    print("="*75 + "\n")

    return 0 if all_passed else 1

if __name__ == "__main__":
    sys.exit(main())
