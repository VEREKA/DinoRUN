# Repository CI checks — without Unity Editor

The workflow [`.github/workflows/repository-checks.yml`](../.github/workflows/repository-checks.yml) runs on pull requests to `main`. After PR #3 is merged, it also supports manual runs from **GitHub Actions → Unity Repository Checks (No Editor) → Run workflow**.

It uses only Python on a standard GitHub-hosted runner. **Unity Editor is not installed or activated by this workflow**, so no Unity license secret is needed.

## What is checked?

The workflow runs **17 Python self-tests** and then checks the checked-out DinoRUN project.

- `ProjectSettings/ProjectVersion.txt` exists and specifies `m_EditorVersion`.
- `Packages/manifest.json` and `Packages/packages-lock.json` parse as JSON objects.
- `Assets/Tests/Editor/GameplayRegressionTests.cs` and `DinoRun.RegressionTests.asmdef` exist.
- All `Assets/**/*.meta` GUID values are 32 hexadecimal characters and unique.
- **Every file and directory** under `Assets/` has a corresponding `.meta` file (including images, prefabs, URP assets, scenes and scripts).
- An orphaned `.meta` without its file or directory is an error.
- `.asmdef` files parse as JSON objects and contain a nonempty assembly name.
- Exactly two enabled build scenes are present, **Menu → GameScene**, with matching `.unity.meta` GUID references.

The check is read-only: it **does not generate, delete or repair** Unity files. One pre-existing orphaned folder metadata file, `Assets/Settings/Build Profiles.meta`, is deleted in this PR so the real repository passes strict orphan checks. There is no corresponding tracked folder or content.

## Limitations

A green GitHub Actions check does **not** mean C# compiled, Unity's 15 `[UnityTest]` cases passed on GitHub, game logic worked, scene/prefab serialized references were valid at runtime or WebGL messages were delivered. Those require Unity Editor and/or a browser/host environment.

## How to run

From the repository root (Python 3.9+):

```bash
python3 -m unittest discover -s scripts/tests -v
python3 scripts/validate_unity_repo.py .
```

On Windows use `py -3` if needed. GitHub Actions runs the same commands.

**Unity checks remain separate:** open the project with **Unity 6000.3.18f1** and run **Window → General → Test Runner → EditMode → Run All**. The owner reported **15/15 tests passing** and successful manual gameplay on PR #2; repeat those tests after relevant gameplay/scene changes.

## Troubleshooting

- Missing or duplicated GUID: restore correct original `.meta` rather than generating a new GUID for referenced assets.
- Orphaned `.meta`: confirm whether a tracked asset/folder was unintentionally removed; if it is an unused remnant, delete only the orphaned metadata.
- Scene order or GUID mismatch: inspect `ProjectSettings/EditorBuildSettings.asset` and corresponding scene metadata.
- Invalid package/assembly JSON: fix syntax without changing Unity package versions or assembly identities.
- Green Python CI but broken Unity build: static checks do not import assets, compile scripts or test behavior.
