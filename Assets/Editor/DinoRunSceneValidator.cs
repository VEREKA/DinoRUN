using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Read-only Editor checks of DinoRUN's production scenes and obstacle prefabs.
/// No scene or prefab is saved or modified by this command.
/// </summary>
public static class DinoRunSceneValidator
{
    private static int checks;
    private static int errors;
    private static int warnings;

    [MenuItem("Tools/VEREKA/Validate DinoRUN")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[DinoRUN Validator] Exit Play Mode before validation.");
            return;
        }

        checks = errors = warnings = 0;
        ValidateBuildScenes();
        ValidateObstaclePrefabs();

        string summary = $"[DinoRUN Validator] {checks} checks, {errors} errors, {warnings} warnings.";
        if (errors > 0) Debug.LogError(summary);
        else if (warnings > 0) Debug.LogWarning(summary);
        else Debug.Log(summary);
    }

    private static void Check(bool condition, string where, string message, bool optional = false)
    {
        checks++;
        if (condition) return;
        string output = $"[DinoRUN Validator] {where}: {message}";
        if (optional)
        {
            warnings++;
            Debug.LogWarning(output);
        }
        else
        {
            errors++;
            Debug.LogError(output);
        }
    }

    private static T[] Components<T>(Scene scene) where T : Component
    {
        var list = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            list.AddRange(root.GetComponentsInChildren<T>(true));
        return list.ToArray();
    }

    private static void RequireReference(Component component, string field, string where)
    {
        SerializedProperty prop = new SerializedObject(component).FindProperty(field);
        Check(prop != null &&
              prop.propertyType == SerializedPropertyType.ObjectReference &&
              prop.objectReferenceValue != null, where,
              $"{component.GetType().Name}.{field} must be assigned.");
    }

    private static void CheckMissingScripts(GameObject root, string where)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            // Missing MonoBehaviour entries are returned as null by Unity.
            foreach (MonoBehaviour behaviour in transform.GetComponents<MonoBehaviour>())
                Check(behaviour != null, where, $"Missing MonoBehaviour on {transform.name}.");
        }
    }

    private static void ValidateBuildScenes()
    {
        string[] sceneNames = { "Menu", "GameScene" };
        var enabled = new List<EditorBuildSettingsScene>();
        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            if (entry.enabled) enabled.Add(entry);

        Check(enabled.Count == sceneNames.Length, "Build Settings",
              "Expected exactly Menu and GameScene enabled.");
        for (int index = 0; index < sceneNames.Length; index++)
        {
            string path = "Assets/Scenes/" + sceneNames[index] + ".unity";
            Check(index < enabled.Count && enabled[index].path == path, path,
                  "Build Settings scene is missing, disabled or at the wrong index.");
            if (!File.Exists(path))
            {
                Check(false, path, "Scene file does not exist.");
                continue;
            }

            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (opened)
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                if (!scene.IsValid() || !scene.isLoaded)
                    Check(false, path, "Scene could not be loaded.");
                else
                    ValidateScene(scene, sceneNames[index]);
            }
            catch (Exception ex)
            {
                Check(false, path, "Scene inspection failed: " + ex.Message);
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void ValidateScene(Scene scene, string name)
    {
        string where = scene.path;

        var cameras = Components<Camera>(scene);
        int taggedCameras = 0;
        foreach (Camera camera in cameras)
            if (camera.CompareTag("MainCamera")) taggedCameras++;
        Check(taggedCameras == 1, where, "Expected one camera tagged MainCamera.");
        Check(Components<Canvas>(scene).Length > 0, where, "Missing UI Canvas.");
        Check(Components<EventSystem>(scene).Length > 0, where, "Missing EventSystem.");

        MainMenu[] menus = Components<MainMenu>(scene);
        Check(menus.Length == 1, where, "Expected one MainMenu controller.");
        foreach (MainMenu menu in menus)
        {
            if (name == "Menu")
                RequireReference(menu, "startButton", where);
            else
                foreach (string field in new[]
                {
                    "tryAgainButton", "endScreenImage", "livescoreTxt",
                    "endscoreTxt", "gameOverTxt", "pressAnyKeyTxt",
                    "arrows", "instructions"
                })
                    RequireReference(menu, field, where);
            // Other MainMenu references can intentionally be absent in Menu.unity.
        }

        if (name == "GameScene")
            ValidateGameplayScene(scene, where);
        foreach (GameObject root in scene.GetRootGameObjects())
            CheckMissingScripts(root, where);
    }

    private static void ValidateGameplayScene(Scene scene, string where)
    {
        var players = Components<PlayerScript>(scene);
        Check(players.Length == 1, where, "Expected one PlayerScript.");
        foreach (PlayerScript player in players)
        {
            Check(player.GetComponent<Rigidbody2D>() != null, where,
                  "Player requires Rigidbody2D.");
            Check(player.GetComponent<CapsuleCollider2D>() != null, where,
                  "Player requires CapsuleCollider2D.");
            Check(player.GetComponentInChildren<Animator>(true) != null, where,
                  "Player requires Animator in child hierarchy.");
            Check(player.GetComponentInChildren<PlayerSlideVisuals>(true) != null, where,
                  "Player requires PlayerSlideVisuals in child hierarchy.");
            Check(player.GetComponentInChildren<SpriteRenderer>(true) != null, where,
                  "Player requires visible artwork in child hierarchy.");
            Check(player.JumpForce > 0f && player.slideDuration > 0f, where,
                  "JumpForce and slideDuration must both be positive.");
            Check(player.scoreMultiplier > 0f, where,
                  "Player scoreMultiplier must be positive.");
        }

        var framings = Components<RunnerCameraFraming>(scene);
        Check(framings.Length == 1, where, "Expected one RunnerCameraFraming.");
        foreach (RunnerCameraFraming framing in framings)
        {
            Check(framing.GetComponent<Camera>() != null, where,
                  "RunnerCameraFraming must be on a Camera.");
            // Runtime fallback uses FindFirstObjectByType<PlayerScript>.
            Check(framing.player != null || players.Length == 1, where,
                  "Camera framing has no player and runtime fallback cannot resolve one.");
        }

        var generators = Components<SpikeGenerator>(scene);
        Check(generators.Length == 1, where, "Expected one SpikeGenerator.");
        foreach (SpikeGenerator generator in generators)
        {
            RequireReference(generator, "bottomSpikePrefab", where);
            RequireReference(generator, "topSpikePrefab", where);

            SerializedObject so = new SerializedObject(generator);
            SerializedProperty assignedPlayer = so.FindProperty("player");
            // A null serialized player is intentional: SpikeGenerator.Start() finds it at runtime.
            Check(assignedPlayer != null &&
                  (assignedPlayer.objectReferenceValue != null || players.Length == 1),
                  where, "SpikeGenerator has no player and runtime fallback cannot resolve one.");

            SerializedProperty min = so.FindProperty("minDistance");
            SerializedProperty max = so.FindProperty("maxDistance");
            Check(min != null && max != null &&
                  min.floatValue > 0f && max.floatValue >= min.floatValue,
                  where, "Generator distance bounds must be positive and ordered.");
            SerializedProperty chance = so.FindProperty("chanceForTopSpawn");
            Check(chance != null && chance.floatValue >= 0f && chance.floatValue <= 1f,
                  where, "chanceForTopSpawn must be between 0 and 1.");
            SerializedProperty reaction = so.FindProperty("minimumReactionTime");
            Check(reaction != null && reaction.floatValue > 0f, where,
                  "minimumReactionTime must be positive.");
        }

        var visuals = Components<PlayerSlideVisuals>(scene);
        Check(visuals.Length == 1, where, "Expected one PlayerSlideVisuals.");
        foreach (PlayerSlideVisuals visual in visuals)
            RequireReference(visual, "sparkSprite", where);

        var finishes = new List<Collider2D>();
        foreach (Collider2D col in Components<Collider2D>(scene))
            if (col.CompareTag("Finish")) finishes.Add(col);
        Check(finishes.Count > 0, where, "Missing Finish-tagged despawn collider.");
        foreach (Collider2D col in finishes)
            Check(col.isTrigger, where, "Finish collider must be a trigger.");

        Check(Components<GroundTile>(scene).Length >= 2, where,
              "Expected at least two looping ground tiles.");
    }

    private static void ValidateObstaclePrefabs()
    {
        CheckObstacle("Assets/Prefabs/Bird.prefab");
        CheckObstacle("Assets/Prefabs/Rock.prefab");
    }

    private static void CheckObstacle(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Check(prefab != null, path, "Obstacle prefab missing.");
        if (prefab == null) return;

        Check(prefab.CompareTag("spike"), path, "Obstacle must be tagged spike for player death.");
        Check(prefab.GetComponent<SpikeScript>() != null, path,
              "Obstacle requires SpikeScript for movement and pooling.");
        Check(prefab.GetComponent<Rigidbody2D>() != null, path,
              "Obstacle requires Rigidbody2D.");
        Check(prefab.GetComponent<Collider2D>() != null, path,
              "Obstacle requires a 2D collider.");
        Check(prefab.GetComponentInChildren<SpriteRenderer>(true) != null, path,
              "Obstacle requires visual SpriteRenderer.");
        CheckMissingScripts(prefab, path);
    }
}
