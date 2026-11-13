#!/usr/bin/env python3
"""Read-only structural integrity checks for a Unity repository (no Unity Editor)."""
import json
import re
import sys
from pathlib import Path

SCENES = ("Assets/Scenes/Menu.unity", "Assets/Scenes/GameScene.unity")
TESTS = (
    "Assets/Tests/Editor/GameplayRegressionTests.cs",
    "Assets/Tests/Editor/DinoRun.RegressionTests.asmdef",
)
GUID = re.compile(r"(?m)^guid:\s*([0-9a-fA-F]{32})\s*$")
VERSION = re.compile(r"(?m)^m_EditorVersion:\s*(\S+)\s*$")
SCENE = re.compile(
    r"(?m)^\s*-\s+enabled:\s*(\d+)\s*\n\s*path:\s*(\S+)\s*\n"
    r"\s*guid:\s*([0-9a-fA-F]{32})\s*$"
)


def validate(root: Path) -> list[str]:
    root = root.resolve()
    errors = []

    def required(path: str) -> Path:
        file = root / path
        if not file.is_file():
            errors.append(f"Missing: {path}")
        return file

    version = required("ProjectSettings/ProjectVersion.txt")
    settings = required("ProjectSettings/EditorBuildSettings.asset")
    if version.is_file() and not VERSION.search(version.read_text(encoding="utf-8-sig")):
        errors.append("ProjectVersion.txt: missing m_EditorVersion")

    for path in ("Packages/manifest.json", "Packages/packages-lock.json"):
        file = required(path)
        if file.is_file():
            try:
                if not isinstance(json.loads(file.read_text(encoding="utf-8-sig")), dict):
                    errors.append(f"{path}: expected JSON object")
            except (ValueError, UnicodeError) as exc:
                errors.append(f"{path}: invalid JSON: {exc}")

    for path in TESTS:
        required(path)

    assets = root / "Assets"
    if not assets.is_dir():
        errors.append("Missing: Assets directory")
    else:
        seen = {}
        for meta in sorted(assets.rglob("*.meta")):
            relative = meta.relative_to(root).as_posix()
            match = GUID.search(meta.read_text(encoding="utf-8-sig"))
            if not match:
                errors.append(f"{relative}: missing valid GUID")
            else:
                guid = match.group(1).lower()
                if guid in seen:
                    errors.append(f"Duplicate GUID {guid}: {seen[guid]} and {relative}")
                seen[guid] = relative

        # Every asset file and directory needs a matching .meta. This scans all
        # extensions, including .prefab, .asset, .png, .jslib, and folder entries.
        for item in sorted(assets.rglob("*")):
            if item.name.endswith(".meta"):
                if not Path(str(item)[:-5]).exists():
                    errors.append(f"Orphaned .meta: {item.relative_to(root)}")
            elif not Path(str(item) + ".meta").is_file():
                errors.append(f"Missing .meta: {item.relative_to(root)}")

        for asmdef in sorted(assets.rglob("*.asmdef")):
            try:
                data = json.loads(asmdef.read_text(encoding="utf-8-sig"))
                if not isinstance(data, dict) or not isinstance(data.get("name"), str) or not data["name"].strip():
                    errors.append(f"{asmdef.relative_to(root)}: asmdef requires nonempty name")
            except (ValueError, UnicodeError) as exc:
                errors.append(f"{asmdef.relative_to(root)}: invalid JSON: {exc}")

    if settings.is_file():
        scenes = SCENE.findall(settings.read_text(encoding="utf-8-sig"))
        if not scenes:
            errors.append("No parsable scenes in EditorBuildSettings")
        enabled = [path for flag, path, _ in scenes if flag == "1"]
        if enabled != list(SCENES):
            errors.append(f"Wrong enabled scene order: {enabled}")
        for _, path, guid in scenes:
            scene = required(path)
            meta = Path(str(scene) + ".meta")
            if not meta.is_file():
                errors.append(f"Missing scene .meta: {path}.meta")
            else:
                match = GUID.search(meta.read_text(encoding="utf-8-sig"))
                if not match or match.group(1).lower() != guid.lower():
                    errors.append(f"Scene GUID mismatch: {path}")
    return errors


def main() -> int:
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1]
    if not root.is_dir():
        print(f"Directory not found: {root}", file=sys.stderr)
        return 2
    errors = validate(root)
    for error in errors:
        print(f"ERROR: {error}", file=sys.stderr)
    if errors:
        print(f"Repository checks failed: {len(errors)} error(s).", file=sys.stderr)
        return 1
    print("Repository structure checks PASSED (without launching Unity).")
    print("Does NOT compile C# or execute Unity tests.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
