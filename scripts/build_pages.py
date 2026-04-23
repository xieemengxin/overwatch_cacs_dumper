#!/usr/bin/env python3
"""Rebuild the GitHub Pages data + SDK mirror from current output/.

Called by `.github/workflows/pages.yml` after generate_sdk_v2.py finishes.
Keeps docs/data/ and docs/sdk/ in sync with output/.

Usage:
    python3 scripts/build_pages.py
"""
import json
import re
import shutil
from pathlib import Path
from collections import Counter

ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "output"
DUMP_DIR = OUTPUT / "dump_json"
DOCS = ROOT / "docs"
DATA_DIR = DOCS / "data"
SDK_DIR = DOCS / "sdk"
OVERRIDES_SRC = ROOT / "scripts" / "overrides"


def dump_json(path: Path, obj) -> None:
    path.write_text(json.dumps(obj, ensure_ascii=False, separators=(",", ":")))
    print(f"  {path.relative_to(ROOT)}: {path.stat().st_size:,} B")


def copy_sdk_headers() -> None:
    SDK_DIR.mkdir(parents=True, exist_ok=True)
    for name in [
        "namedump.hpp", "abilitydump.hpp", "entitydump.hpp",
        "statevardump.hpp", "weapondump.hpp", "herokitdump.hpp",
        "vars.csv",
    ]:
        src = OUTPUT / name
        if src.exists():
            shutil.copy(src, SDK_DIR / name)
            print(f"  sdk/{name}: {(SDK_DIR / name).stat().st_size:,} B")


def copy_csv_overrides() -> None:
    for src_name, dst_name in [
        ("entities.csv", "overrides_entities.csv"),
        ("statevars.csv", "overrides_statevars.csv"),
        ("heroes.csv", "overrides_heroes.csv"),
    ]:
        src = OVERRIDES_SRC / src_name
        if src.exists():
            shutil.copy(src, DOCS / dst_name)
            print(f"  {dst_name}: {(DOCS / dst_name).stat().st_size:,} B")


def trim_heroes():
    heroes = json.load(open(DUMP_DIR / "heroes.json", encoding="utf-8"))
    out = []
    for h in heroes:
        if not h.get("is_hero"): continue
        out.append({
            "hero_id": h["hero_id"],
            "name": h.get("name_zhCN"),
            "gender": h.get("gender"),
            "size": h.get("size"),
            "color": h.get("color"),
            "gameplay_entity": (h.get("gameplay_entity") or {}).get("entity_guid"),
            "loadouts": [{
                "id": lo.get("guid_index"),
                "name": lo.get("name_zhCN"),
                "desc": lo.get("description_zhCN"),
                "category": lo.get("category"),
                "button": lo.get("button"),
            } for lo in h.get("loadouts", [])],
        })
    dump_json(DATA_DIR / "heroes.json", out)
    return out


def parse_entitydump():
    hpp = (OUTPUT / "entitydump.hpp").read_text()
    out = []
    for m in re.finditer(
        r'\{0x([0-9A-Fa-f]+)u,\s*"([^"]*)",\s*EntityType::(\w+),\s*0x([0-9A-Fa-f]+)u,\s*"([^"]*)",\s*'
        r'0x([0-9A-Fa-f]+)u,\s*"([^"]*)",\s*"([^"]*)",\s*"([^"]*)"\},', hpp
    ):
        eid, name, etype, hero_id, hero_name, lo_id, lo_name, button, slot = m.groups()
        out.append({
            "id": f"0x{eid}",
            "name": name,
            "type": etype,
            "hero_id": f"0x{hero_id}" if hero_id != "0" else None,
            "hero_name": hero_name or None,
            "loadout_id": f"0x{lo_id}" if lo_id != "0" else None,
            "loadout_name": lo_name or None,
            "button": button or None,
            "slot": slot,
        })
    dump_json(DATA_DIR / "entities.json", out)
    return out


def parse_statevardump():
    hpp = (OUTPUT / "statevardump.hpp").read_text()
    out = []
    for m in re.finditer(
        r'\{0x([0-9A-Fa-f]+)u,\s*"([^"]+)",\s*StateVarKind::(\w+),\s*StateVarDomain::(\w+),\s*"([^"]*)"\},', hpp
    ):
        vid, name, kind, domain, desc = m.groups()
        out.append({
            "var_id": f"0x{vid}",
            "name": name,
            "kind": kind,
            "domain": domain,
            "desc": desc,
        })
    dump_json(DATA_DIR / "statevars.json", out)
    return out


def parse_weapondump():
    hpp = (OUTPUT / "weapondump.hpp").read_text()
    out = []
    for m in re.finditer(
        r'\{0x([0-9A-Fa-f]+)u,\s*"([^"]+)",\s*0x([0-9A-Fa-f]+)u,\s*(-?\d+),\s*(\d+)u,\s*'
        r'BallisticType::(\w+),\s*(-?\d+),\s*(-?\d+),\s*(-?\d+),\s*(-?\d+),\s*'
        r'0x([0-9A-Fa-f]+)u,\s*"([^"]*)"\},', hpp
    ):
        hid, hname, gidx, si, ni, bt, sp, lt, pe, fr, pid, pn = m.groups()
        out.append({
            "hero_id": f"0x{hid}",
            "hero_name": hname,
            "graph_index": f"0x{gidx}",
            "slot_index": int(si),
            "node_index": int(ni),
            "ballistic": bt,
            "speed": int(sp),
            "lifetime": int(lt),
            "pellets": int(pe),
            "fire_rate": int(fr),
            "projectile_entity_id": f"0x{pid}" if pid != "0" else None,
            "projectile_entity_name": pn or None,
        })
    dump_json(DATA_DIR / "weapons.json", out)
    return out


def parse_herokitdump():
    hpp = (OUTPUT / "herokitdump.hpp").read_text()
    out = []
    for m in re.finditer(
        r'\{0x([0-9A-Fa-f]+)u,\s*"([^"]+)",\s*(\d+)u,\s*(true|false),\s*(true|false),\s*(true|false),', hpp
    ):
        hid, name, gc, hd, hp, hb = m.groups()
        out.append({
            "hero_id": f"0x{hid}",
            "hero_name": name,
            "graph_count": int(gc),
            "has_deflect": hd == "true",
            "has_projectile_weapon": hp == "true",
            "has_beam_weapon": hb == "true",
        })
    dump_json(DATA_DIR / "herokits.json", out)
    return out


def trim_var_usage():
    src = OUTPUT / "var_usage.json"
    if not src.exists():
        print("  [skip] var_usage.json missing")
        return []
    full = json.load(open(src, encoding="utf-8"))
    out = []
    for u in full:
        vt = {
            "var_id": u["var_id"],
            "usage_class": u.get("usage_class"),
            "scope": u.get("scope"),
            "role_hint": u.get("role_hint"),
            "writer_loadout_count": u.get("writer_loadout_count"),
            "reader_loadout_count": u.get("reader_loadout_count"),
            "writer_hero_count": u.get("writer_hero_count"),
            "chase_dest": u.get("chase_dest"),
            "setvar_out": u.get("setvar_out"),
            "graph_count": u.get("graph_count"),
            "is_sync_var": u.get("is_sync_var"),
        }
        sd = u.get("scope_detail") or {}
        scope = u.get("scope")
        if scope == "single_ability":
            vt["binding"] = f"{sd.get('hero_name','?')}/{sd.get('name','?')} @ {sd.get('button','-')}"
        elif scope == "single_hero":
            los = ", ".join(l.get("name", "?") for l in (sd.get("loadouts") or [])[:3])
            vt["binding"] = f"{sd.get('hero_name','?')}: {los}"
        elif scope == "multi_hero":
            vt["binding"] = ", ".join((hb.get("hero_name") or "?") for hb in (sd.get("heroes") or [])[:4])
        elif scope == "global":
            vt["binding"] = f"{sd.get('hero_count','?')}h / {sd.get('loadout_count','?')}lo"
        else:
            vt["binding"] = scope or "?"
        out.append(vt)
    dump_json(DATA_DIR / "var_usage.json", out)
    return out


def write_summary(heroes, entities, statevars, weapons, usage):
    def count_by(xs, key):
        return dict(Counter(x.get(key) for x in xs).most_common())

    # Try to read build/version from a .build.info or fall back to a known constant
    build = "148494"
    version = "2.22.0.0.148915N (CN)"

    summary = {
        "build": build,
        "version": version,
        "heroes_playable": len(heroes),
        "heroes_total": len(json.load(open(DUMP_DIR / "heroes.json", encoding="utf-8"))),
        "loadouts": len(json.load(open(DUMP_DIR / "loadouts.json", encoding="utf-8"))),
        "entities_total": 1957,
        "entities_named": len([e for e in entities if e["hero_id"] or (e["name"] and not e["name"].startswith("0x"))]),
        "entities_with_slot": len([e for e in entities if e["slot"] != "None"]),
        "entities_hero_bound": len([e for e in entities if e["hero_id"]]),
        "statevars": len(statevars),
        "var_usage_total": len(usage),
        "weapons": len(weapons),
        "var_scope_counts": count_by(usage, "scope"),
        "var_class_counts": count_by(usage, "usage_class"),
    }
    dump_json(DATA_DIR / "summary.json", summary)


def main():
    DATA_DIR.mkdir(parents=True, exist_ok=True)
    print(">> Copying SDK headers + CSV…")
    copy_sdk_headers()
    copy_csv_overrides()
    print(">> Building trimmed JSON for browsing…")
    heroes = trim_heroes()
    entities = parse_entitydump()
    statevars = parse_statevardump()
    weapons = parse_weapondump()
    parse_herokitdump()
    usage = trim_var_usage()
    print(">> Writing summary…")
    write_summary(heroes, entities, statevars, weapons, usage)
    print("Done.")


if __name__ == "__main__":
    main()
