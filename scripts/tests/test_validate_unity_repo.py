"""Self-tests for DinoRUN's read-only repository integrity validator."""
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from validate_unity_repo import SCENES, validate


class ValidatorTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.write("ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.3.18f1\n")
        self.write("Packages/manifest.json", '{"dependencies": {}}')
        self.write("Packages/packages-lock.json", '{"dependencies": {}}')
        for test, char in [
            ("Assets/Tests/Editor/GameplayRegressionTests.cs", "a"),
            ("Assets/Tests/Editor/DinoRun.RegressionTests.asmdef", "b"),
        ]:
            self.write(test, '{"name": "DinoRun.RegressionTests"}' if test.endswith(".asmdef") else "// tests\n")
            self.write(test + ".meta", self.meta(char))
        data = ["EditorBuildSettings:", "  m_Scenes:"]
        for index, scene in enumerate(SCENES):
            digit = str(index + 1)
            self.write(scene, "scene YAML")
            self.write(scene + ".meta", self.meta(digit))
            data.extend(["  - enabled: 1", f"    path: {scene}", f"    guid: {digit * 32}"])
        self.write("ProjectSettings/EditorBuildSettings.asset", "\n".join(data))

    @staticmethod
    def meta(char):
        return f"fileFormatVersion: 2\nguid: {char * 32}\n"

    def write(self, path, content):
        p = self.root / path
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(content, encoding="utf-8")
        if path.startswith("Assets/"):
            parent = p.parent
            asset_root = self.root / "Assets"
            while parent != asset_root.parent and parent != self.root:
                companion = Path(str(parent) + ".meta")
                if not companion.exists():
                    guid = hashlib.md5(parent.relative_to(self.root).as_posix().encode()).hexdigest()
                    companion.write_text(f"fileFormatVersion: 2\nguid: {guid}\n", encoding="utf-8")
                parent = parent.parent

    def errors(self):
        return validate(self.root)

    def test_valid_fixture(self):
        self.assertEqual(self.errors(), [])

    def test_duplicate_guid(self):
        self.write("Assets/Prefabs/Rock.prefab", "prefab")
        self.write("Assets/Prefabs/Rock.prefab.meta", self.meta("a"))
        self.assertTrue(any("Duplicate GUID" in e for e in self.errors()))

    def test_invalid_meta_guid(self):
        self.write("Assets/Tests/Editor/GameplayRegressionTests.cs.meta", "guid: invalid\n")
        self.assertTrue(any("missing valid GUID" in e for e in self.errors()))

    def test_missing_script_meta(self):
        (self.root / "Assets/Tests/Editor/GameplayRegressionTests.cs.meta").unlink()
        self.assertTrue(any("Missing .meta" in e and "GameplayRegressionTests.cs" in e for e in self.errors()))

    def test_missing_prefab_meta(self):
        self.write("Assets/Prefabs/Bird.prefab", "prefab")
        self.assertTrue(any("Missing .meta" in e and "Bird.prefab" in e for e in self.errors()))

    def test_missing_asset_meta(self):
        self.write("Assets/Settings/UniversalRP.asset", "asset")
        self.assertTrue(any("Missing .meta" in e and "UniversalRP.asset" in e for e in self.errors()))

    def test_missing_texture_meta(self):
        self.write("Assets/Sprites/Player.png", "image")
        self.assertTrue(any("Missing .meta" in e and "Player.png" in e for e in self.errors()))

    def test_missing_folder_meta(self):
        (self.root / "Assets/Tests.meta").unlink()
        self.assertTrue(any("Missing .meta" in e and "Assets/Tests" in e for e in self.errors()))

    def test_orphaned_file_meta(self):
        self.write("Assets/Unused.prefab.meta", self.meta("f"))
        self.assertTrue(any("Orphaned .meta" in e for e in self.errors()))

    def test_orphaned_folder_meta(self):
        self.write("Assets/UnusedFolder.meta", self.meta("e"))
        self.assertTrue(any("Orphaned .meta" in e for e in self.errors()))

    def test_missing_scene(self):
        (self.root / SCENES[0]).unlink()
        self.assertTrue(any("Missing" in e and SCENES[0] in e for e in self.errors()))

    def test_scene_guid_mismatch(self):
        self.write(SCENES[0] + ".meta", self.meta("f"))
        self.assertTrue(any("Scene GUID mismatch" in e for e in self.errors()))

    def test_wrong_scene_order(self):
        p = self.root / "ProjectSettings/EditorBuildSettings.asset"
        data = p.read_text(encoding="utf-8")
        data = data.replace(SCENES[0], "TEMP", 1).replace(SCENES[1], SCENES[0], 1).replace("TEMP", SCENES[1], 1)
        p.write_text(data, encoding="utf-8")
        self.assertTrue(any("Wrong enabled scene order" in e for e in self.errors()))

    def test_invalid_manifest_json(self):
        self.write("Packages/manifest.json", "{broken")
        self.assertTrue(any("invalid JSON" in e for e in self.errors()))

    def test_invalid_asmdef_json(self):
        self.write("Assets/Tests/Editor/DinoRun.RegressionTests.asmdef", "{broken")
        self.assertTrue(any("invalid JSON" in e for e in self.errors()))

    def test_missing_asmdef_name(self):
        self.write("Assets/Tests/Editor/DinoRun.RegressionTests.asmdef", json.dumps({}))
        self.assertTrue(any("asmdef requires nonempty name" in e for e in self.errors()))

    def test_missing_version(self):
        self.write("ProjectSettings/ProjectVersion.txt", "wrong: version\n")
        self.assertTrue(any("missing m_EditorVersion" in e for e in self.errors()))


if __name__ == "__main__":
    unittest.main()
