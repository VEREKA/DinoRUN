using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using System.IO;
using Object = UnityEngine.Object;

// Reflection keeps the tests separate from the game's predefined Assembly-CSharp assembly.
public class GameplayRegressionTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static Component Find(string name) => (Component)Object.FindFirstObjectByType(GameType(name));
    private static object Read(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);
    private static void Write(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Members).Invoke(target, args);
    private static object Manager(string name) => GameType(name).GetProperty("Instance").GetValue(null);
    private static void Play() => Call(Manager("GameStateManager"), "SetState", Enum.Parse(GameType("GameState"), "Playing"));

    private static void OpenGame()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
    }

    private static int Subscriptions(object manager, string eventName, string targetType)
    {
        var handlers = (Delegate)Read(manager, eventName);
        return handlers?.GetInvocationList().Count(handler => handler.Target?.GetType().Name == targetType) ?? 0;
    }

    private static void AssertSubscriptions()
    {
        Assert.That(Subscriptions(Manager("GameStateManager"), "OnStateChanged", "MainMenu"), Is.EqualTo(1));
        Assert.That(Subscriptions(Manager("GameStateManager"), "OnStateChanged", "PlayerScript"), Is.EqualTo(1));
        Assert.That(Subscriptions(Manager("ScoreManager"), "OnScoreChanged", "MainMenu"), Is.EqualTo(1));
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        Time.captureDeltaTime = 0f;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DirectStartAndRepeatedSceneLoadsKeepExactlyOneSubscription()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;
        AssertSubscriptions();
        foreach (string name in new[] { "GameStateManager", "ScoreManager", "SpeedManager", "PoolManager" })
            Assert.That(Object.FindObjectsByType(GameType(name), FindObjectsSortMode.None).Length, Is.EqualTo(1), name);

        var player = (Behaviour)Find("PlayerScript");
        var menu = (Behaviour)Find("MainMenu");
        player.enabled = false;
        menu.enabled = false;
        Assert.That(Subscriptions(Manager("GameStateManager"), "OnStateChanged", "PlayerScript"), Is.Zero);
        Assert.That(Subscriptions(Manager("ScoreManager"), "OnScoreChanged", "MainMenu"), Is.Zero);
        player.enabled = true;
        menu.enabled = true;
        AssertSubscriptions();

        yield return SceneManager.LoadSceneAsync("Menu");
        for (int retry = 0; retry < 3; retry++)
        {
            yield return SceneManager.LoadSceneAsync("GameScene");
            yield return null;
            AssertSubscriptions();
            Call(Manager("ScoreManager"), "UpdateScore", 42f);
            var text = Read(Find("MainMenu"), "livescoreTxt");
            Assert.That(Property(text, "text"), Is.EqualTo("SCORE: 42"));
        }
    }

    [UnityTest]
    public IEnumerator FastObstacleCrossingPlayerStillEndsTheRun()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;
        var generator = (Behaviour)Find("SpikeGenerator");
        generator.enabled = false;
        Play();
        var player = Find("PlayerScript");
        var body = player.GetComponent<Rigidbody2D>();
        body.position = new Vector2(-6.72f, 2f);
        body.gravityScale = 0f;
        Call(Manager("SpeedManager"), "SetSpeed", 90f);
        var prefab = Read(generator, "topSpikePrefab");
        var spike = (Component)Call(Manager("PoolManager"), "Spawn", prefab, new Vector3(body.position.x + 0.86f, 2f, 0f));
        var mode = Physics2D.simulationMode;
        try
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.SyncTransforms();
            Call(spike, "FixedUpdate");
            Physics2D.Simulate(Time.fixedDeltaTime);
            Assert.That(Property(Manager("GameStateManager"), "State").ToString(), Is.EqualTo("GameOver"));
        }
        finally { Physics2D.simulationMode = mode; }
    }

    [UnityTest]
    public IEnumerator MissedFinishTriggerReturnsAndReusesTheObstacleOnce()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;
        var generator = (Behaviour)Find("SpikeGenerator");
        generator.enabled = false;
        var prefab = Read(generator, "bottomSpikePrefab");
        var pool = Manager("PoolManager");
        var spike = (Component)Call(pool, "Spawn", prefab, new Vector3(-20f, 0f, 0f));
        int id = spike.GetInstanceID();
        Call(spike, "FixedUpdate");
        Assert.That(spike.gameObject.activeSelf, Is.False);
        Call(spike, "ReturnToPool");
        var reused = (Component)Call(pool, "Spawn", prefab, new Vector3(17f, -4f, 0f));
        Assert.That(reused.GetInstanceID(), Is.EqualTo(id));
        Assert.That(reused.GetComponent<Rigidbody2D>().position.x, Is.EqualTo(17f).Within(0.001f));
        var another = (Component)Call(pool, "Spawn", prefab, new Vector3(20f, -4f, 0f));
        Assert.That(another.GetInstanceID(), Is.Not.EqualTo(id), "An active instance must not remain in the queue.");
    }

    [UnityTest]
    public IEnumerator AlternatingObstaclesAllowRecoveryAtLowAndHighSpeeds()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;
        var generator = (Behaviour)Find("SpikeGenerator");
        generator.enabled = false;
        Play();
        var player = Find("PlayerScript");
        float recovery = Mathf.Max((float)Property(player, "JumpRecoveryTime"), (float)Read(player, "slideDuration"));
        foreach (float speed in new[] { 5f, 18f, 90f })
        {
            Call(Manager("SpeedManager"), "SetSpeed", speed);
            foreach (bool previousTop in new[] { false, true })
            {
                Write(generator, "hasPreviousSpike", true);
                Write(generator, "previousWasTop", previousTop);
                Write(generator, "nextIsTop", !previousTop);
                Write(generator, "targetDistance", 0f);
                Write(generator, "distanceCounter", 0f);
                int steps = 0;
                do
                {
                    Call(generator, "FixedUpdate");
                    steps++;
                    Assert.That(steps, Is.LessThan(1000), "Generator did not spawn.");
                } while ((float)Read(generator, "distanceCounter") > 0f);
                Assert.That(steps * Time.fixedDeltaTime, Is.GreaterThanOrEqualTo(recovery + 0.25f));
            }
        }
    }

    [UnityTest]
    public IEnumerator NarrowerViewportLimitsSpeedToPreserveReactionTime()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;
        var generator = Find("SpikeGenerator");
        var camera = Camera.main;
        camera.aspect = 16f / 9f;
        Call(generator, "UpdateVisibleSpeedLimit");
        float wideLimit = (float)Property(Manager("SpeedManager"), "GameplaySpeedLimit");
        camera.aspect = 4f / 3f;
        Call(generator, "UpdateVisibleSpeedLimit");
        float narrowLimit = (float)Property(Manager("SpeedManager"), "GameplaySpeedLimit");
        Assert.That(narrowLimit, Is.LessThan(wideLimit));
        Call(Manager("SpeedManager"), "SetSpeed", 90f);
        Assert.That((float)Property(Manager("SpeedManager"), "CurrentSpeed"), Is.EqualTo(narrowLimit).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator RepeatedSlidePreservesFeetAndDoesNotShortenTheNextSlide()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;
        ((Behaviour)Find("SpikeGenerator")).enabled = false;
        Play();
        var player = Find("PlayerScript");
        var collider = player.GetComponent<CapsuleCollider2D>();
        Vector2 size = collider.size;
        Vector2 offset = collider.offset;
        Write(player, "isGrounded", true);
        Call(player, "startSlide");
        Assert.That(collider.offset.y - collider.size.y * 0.5f,
            Is.EqualTo(offset.y - size.y * 0.5f).Within(0.0001f));
        for (int i = 0; i < 5; i++) Call(player, "FixedUpdate");
        float remaining = (float)Read(player, "slideTimeRemaining");
        Call(player, "startSlide");
        Assert.That((float)Read(player, "slideTimeRemaining"), Is.EqualTo(remaining));
        for (int i = 0; i < 30; i++) Call(player, "FixedUpdate");
        Assert.That((bool)Read(player, "isSliding"), Is.False);
        Assert.That(collider.size, Is.EqualTo(size));
        Assert.That(collider.offset, Is.EqualTo(offset));
        Call(player, "startSlide");
        Call(player, "FixedUpdate");
        Assert.That((bool)Read(player, "isSliding"), Is.True);
        ((Behaviour)player).enabled = false;
        Assert.That(collider.size, Is.EqualTo(size));
        Assert.That(collider.offset, Is.EqualTo(offset));
    }

    [UnityTest]
    public IEnumerator PauseFreezesPhysicsScoreDifficultyAndSlideWithoutResettingOnResume()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;
        ((Behaviour)Find("SpikeGenerator")).enabled = false;
        Play();
        var player = Find("PlayerScript");
        var body = player.GetComponent<Rigidbody2D>();
        body.position = new Vector2(-6.72f, 2f);
        body.linearVelocity = Vector2.up * 3f;
        Write(player, "isGrounded", true);
        Call(player, "startSlide");
        Call(Manager("ScoreManager"), "UpdateScore", 42.25f);
        Write(Manager("SpeedManager"), "useScoreForProgress", false);
        float playingTime = (float)Read(Manager("SpeedManager"), "playingTime");
        float remaining = (float)Read(player, "slideTimeRemaining");
        Vector2 position = body.position;
        Vector2 velocity = body.linearVelocity;
        Call(Manager("GameStateManager"), "TogglePause");
        Assert.That(Time.timeScale, Is.Zero);
        yield return new WaitForSecondsRealtime(0.15f);
        Assert.That(body.position, Is.EqualTo(position));
        Assert.That(body.linearVelocity, Is.EqualTo(velocity));
        Assert.That((float)Property(Manager("ScoreManager"), "CurrentScore"), Is.EqualTo(42.25f));
        Assert.That((float)Read(Manager("SpeedManager"), "playingTime"), Is.EqualTo(playingTime));
        Assert.That((float)Read(player, "slideTimeRemaining"), Is.EqualTo(remaining));
        Call(Manager("GameStateManager"), "TogglePause");
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(body.linearVelocity, Is.EqualTo(velocity));
        Assert.That((float)Property(Manager("ScoreManager"), "CurrentScore"), Is.EqualTo(42.25f));
        yield return new WaitForSecondsRealtime(0.1f);
        Assert.That((float)Property(Manager("ScoreManager"), "CurrentScore"), Is.GreaterThan(42.25f));
        Call(Manager("GameStateManager"), "TogglePause");
        yield return SceneManager.LoadSceneAsync("Menu");
        Assert.That(Time.timeScale, Is.EqualTo(1f), "Leaving a paused run must restore time.");
    }

    [UnityTest]
    public IEnumerator ScoreHasOneOwnerAndOnlyNotifiesWhenDisplayedValueChanges()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;
        AssertScoreNotifications();
    }

    private static void AssertScoreNotifications()
    {
        // Create the callback after entering Play Mode, which reloads the domain.
        var score = Manager("ScoreManager");
        int notifications = 0;
        Action<float> handler = value => notifications++;
        var scoreEvent = score.GetType().GetEvent("OnScoreChanged");
        scoreEvent.AddEventHandler(score, handler);
        try
        {
            Call(score, "UpdateScore", 10.1f);
            Call(score, "UpdateScore", 10.2f);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That((float)Property(score, "CurrentScore"), Is.EqualTo(10.2f));
            Assert.That(Property(Read(Find("MainMenu"), "livescoreTxt"), "text"), Is.EqualTo("SCORE: 10"));
            Call(score, "Update");
            Assert.That((float)Property(score, "CurrentScore"), Is.EqualTo(10.2f), "Menu time must not count.");
        }
        finally { scoreEvent.RemoveEventHandler(score, handler); }
    }

    [UnityTest]
    public IEnumerator PoolKeepsActiveAndInactiveInstancesAcrossSceneRestarts()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;
        var prefab = Read(Find("SpikeGenerator"), "bottomSpikePrefab");
        var pool = (Component)Manager("PoolManager");
        var first = (Component)Call(pool, "Spawn", prefab, new Vector3(17f, -4f, 0f));
        var second = (Component)Call(pool, "Spawn", prefab, new Vector3(20f, -4f, 0f));
        int[] ids = { first.GetInstanceID(), second.GetInstanceID() };
        Call(pool, "Return", first);
        Call(pool, "Return", first);
        for (int retry = 0; retry < 3; retry++)
        {
            yield return SceneManager.LoadSceneAsync("GameScene");
            yield return null;
            Assert.That(first != null && second != null, Is.True);
            Assert.That(first.gameObject.activeSelf || second.gameObject.activeSelf, Is.False);
            Assert.That(pool.transform.childCount, Is.EqualTo(2));
            first = (Component)Call(pool, "Spawn", prefab, new Vector3(17f, -4f, 0f));
            second = (Component)Call(pool, "Spawn", prefab, new Vector3(20f, -4f, 0f));
            Assert.That(new[] { first.GetInstanceID(), second.GetInstanceID() }, Is.EquivalentTo(ids));
        }
        yield return SceneManager.LoadSceneAsync("Menu");
        Assert.That(first.gameObject.activeSelf || second.gameObject.activeSelf, Is.False);
        Assert.That(pool.transform.childCount, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator DiagnosticsStayOffAndDoNotAddHelpersDuringGameplay()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;
        Assert.That((bool)Read(Find("DebugHUD"), "show"), Is.False);
        var generator = (Behaviour)Find("SpikeGenerator");
        generator.enabled = false;
        Play();
        var spike = (Component)Call(Manager("PoolManager"), "Spawn", Read(generator, "bottomSpikePrefab"), new Vector3(17f, -4f, 0f));
        yield return new WaitForSecondsRealtime(0.1f);
        Assert.That(spike.GetComponent(GameType("SpikeDebugState")), Is.Null);
        Assert.That(Object.FindObjectsByType(GameType("GroundTileDebugState"), FindObjectsSortMode.None), Is.Empty);
    }

    private static Component PrepareLandingTest()
    {
        ((Behaviour)Find("SpikeGenerator")).enabled = false;
        foreach (var ground in GameObject.FindGameObjectsWithTag("ground"))
            ground.GetComponent<Collider2D>().enabled = false;
        Play();
        var player = Find("PlayerScript");
        Write(player, "isGrounded", false);
        Call(Read(player, "groundContacts"), "Clear");
        player.GetComponent<Rigidbody2D>().position = new Vector2(0f, 1f);
        player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.down;
        return player;
    }

    private static BoxCollider2D GroundBox(float x, float width)
    {
        var platform = new GameObject("TestGround");
        platform.tag = "ground";
        platform.transform.position = new Vector3(x, -0.125f, 0f);
        var box = platform.AddComponent<BoxCollider2D>();
        box.size = new Vector2(width, 0.25f);
        return box;
    }

    [UnityTest]
    public IEnumerator JumpPressedBeforeLandingExecutesOnceAndExpiredInputDoesNotJump()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;
        var player = PrepareLandingTest();
        GroundBox(0f, 4f);
        var body = player.GetComponent<Rigidbody2D>();
        var mode = Physics2D.simulationMode;
        try
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.SyncTransforms();
            Call(player, "RequestJump");
            Assert.That((bool)Read(player, "isGrounded"), Is.False);
            bool jumped = false;
            for (int i = 0; i < 6; i++)
            {
                Call(player, "FixedUpdate");
                Physics2D.Simulate(Time.fixedDeltaTime);
                if (body.linearVelocity.y > 1f) { jumped = true; break; }
            }
            Assert.That(jumped, Is.True, "A press shortly before landing must produce a jump.");
            Assert.That((bool)Read(player, "JumpRequested"), Is.False);
            Assert.That((bool)Read(player, "isGrounded"), Is.False);

            body.position = new Vector2(0f, 1f);
            body.linearVelocity = Vector2.down;
            Call(Read(player, "groundContacts"), "Clear");
            Write(player, "isGrounded", false);
            Call(player, "RequestJump");
            for (int i = 0; i < 8; i++) Call(player, "FixedUpdate");
            for (int i = 0; i < 8; i++)
            {
                Call(player, "FixedUpdate");
                Physics2D.Simulate(Time.fixedDeltaTime);
            }
            Assert.That(body.linearVelocity.y, Is.LessThanOrEqualTo(0.1f), "Expired input must not jump on landing.");
            Assert.That((bool)Read(player, "isGrounded"), Is.True);
        }
        finally { Physics2D.simulationMode = mode; }
    }

    [UnityTest]
    public IEnumerator GroundContactSurvivesASeamAndClearsWhenTheLastSupportDisappears()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;
        var player = PrepareLandingTest();
        var left = GroundBox(-1f, 2f);
        var right = GroundBox(1f, 2f);
        var body = player.GetComponent<Rigidbody2D>();
        var mode = Physics2D.simulationMode;
        try
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 10; i++) Physics2D.Simulate(Time.fixedDeltaTime);
            Assert.That((bool)Read(player, "isGrounded"), Is.True);
            Assert.That((int)Property(Read(player, "groundContacts"), "Count"), Is.EqualTo(2));
            left.enabled = false;
            Physics2D.Simulate(Time.fixedDeltaTime);
            Assert.That((bool)Read(player, "isGrounded"), Is.True);
            right.enabled = false;
            Physics2D.Simulate(Time.fixedDeltaTime);
            Assert.That((bool)Read(player, "isGrounded"), Is.False);
            Call(player, "RequestJump");
            Call(player, "FixedUpdate");
            Physics2D.Simulate(Time.fixedDeltaTime);
            Assert.That(body.linearVelocity.y, Is.LessThanOrEqualTo(0f));
        }
        finally { Physics2D.simulationMode = mode; }
    }

    [UnityTest]
    public IEnumerator PauseButtonsWorkWithoutKeyboardAndFitPortraitScreens()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;
        var ui = Find("PauseMenuUI");
        var pause = (Button)Read(ui, "pauseButton");
        var resume = (Button)Read(ui, "resumeButton");
        Assert.That(pause.gameObject.activeSelf, Is.False);
        Play();
        Assert.That(pause.gameObject.activeSelf, Is.True);
        pause.onClick.Invoke();
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(((GameObject)Read(ui, "overlay")).activeSelf, Is.True);
        Screen.SetResolution(720, 1280, FullScreenMode.Windowed);
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4];
        ((RectTransform)resume.transform).GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corner);
            Assert.That(screen.x, Is.InRange(0f, (float)Screen.width));
            Assert.That(screen.y, Is.InRange(0f, (float)Screen.height));
        }
        ExecuteEvents.Execute(resume.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(((GameObject)Read(ui, "overlay")).activeSelf, Is.False);
        Assert.That((bool)Read(Find("PlayerScript"), "JumpRequested"), Is.False);
        var camera = Camera.main;
        foreach (float aspect in new[] { 4f / 3f, 9f / 16f })
        {
            camera.aspect = aspect;
            Call(Find("RunnerCameraFraming"), "ApplyFraming");
            float playerX = camera.WorldToViewportPoint(Find("PlayerScript").transform.position).x;
            Assert.That(playerX, Is.InRange(0.1f, 0.25f), "The robot must stay visible on narrow screens.");
        }
        camera.ResetAspect();
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
    }

    private static void CapturePresentation(string name, int width = 1280, int height = 720)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        string directory = Environment.GetEnvironmentVariable("DINORUN_TEST_OUTPUT") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/GameplayVisuals"));
        Directory.CreateDirectory(directory);
        var camera = Camera.main;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(canvas => canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var oldCameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
        var oldDistances = canvases.Select(canvas => canvas.planeDistance).ToArray();
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var oldTarget = RenderTexture.active;
        var oldCameraTarget = camera.targetTexture;
        Texture2D texture = null;
        try
        {
            camera.targetTexture = target;
            Call(Find("RunnerCameraFraming"), "ApplyFraming");
            foreach (var canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target;
            texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new UnityEngine.Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                canvases[i].worldCamera = oldCameras[i];
                canvases[i].planeDistance = oldDistances[i];
            }
            RenderTexture.active = oldTarget;
            camera.targetTexture = oldCameraTarget;
            Call(Find("RunnerCameraFraming"), "ApplyFraming");
            if (texture != null) Object.DestroyImmediate(texture);
            RenderTexture.ReleaseTemporary(target);
        }
    }

    [UnityTest]
    public IEnumerator SlideClearsFlyingObstacleButStandingDoesNot()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;
        var player = PrepareLandingTest();
        GroundBox(0f, 8f);
        var prefab = Read(Find("SpikeGenerator"), "topSpikePrefab");
        var pool = Manager("PoolManager");
        Call(Manager("SpeedManager"), "SetSpeed", 18f);
        var mode = Physics2D.simulationMode;
        try
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 10; i++) Physics2D.Simulate(Time.fixedDeltaTime);
            Assert.That((bool)Read(player, "isGrounded"), Is.True);
            Call(player, "startSlide");
            var spike = (Component)Call(pool, "Spawn", prefab, new Vector3(1.8f, 1.5f, 0f));
            for (int i = 0; i < 12; i++)
            {
                Call(player, "FixedUpdate");
                Call(spike, "FixedUpdate");
                Physics2D.Simulate(Time.fixedDeltaTime);
            }
            Assert.That(Property(Manager("GameStateManager"), "State").ToString(), Is.EqualTo("Playing"));
            Call(pool, "Return", spike);
            Call(player, "stopSlide");
            spike = (Component)Call(pool, "Spawn", prefab, new Vector3(1.8f, 1.5f, 0f));
            for (int i = 0; i < 12; i++)
            {
                Call(spike, "FixedUpdate");
                Physics2D.Simulate(Time.fixedDeltaTime);
            }
            Assert.That(Property(Manager("GameStateManager"), "State").ToString(), Is.EqualTo("GameOver"));
        }
        finally { Physics2D.simulationMode = mode; }
    }

    [UnityTest]
    public IEnumerator SlideChangesOnlyArtworkAndRestoresTheRunPose()
    {
        OpenGame();
        yield return new EnterPlayMode();
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
        yield return null;
        ((Behaviour)Find("SpikeGenerator")).enabled = false;
        Time.captureDeltaTime = 0.02f;
        Play();
        for (int frame = 0; frame < 20; frame++) yield return null;
        var player = Find("PlayerScript");
        var body = player.GetComponent<Rigidbody2D>();
        var visuals = Find("PlayerSlideVisuals");
        var renderer = visuals.GetComponentInChildren<SpriteRenderer>();
        var runSprite = renderer.sprite;
        var size = player.GetComponent<CapsuleCollider2D>().size;
        Assert.That(player.GetComponent<SpriteRenderer>(), Is.Null);
        CapturePresentation("run");
        Vector2 position = body.position;
        Write(player, "isGrounded", true);
        Call(player, "startSlide");
        Assert.That(body.position, Is.EqualTo(position), "Starting a slide must not push the rigidbody into the ground.");
        Assert.That(renderer.sprite, Is.Not.EqualTo(runSprite));
        Assert.That(renderer.sprite.pixelsPerUnit, Is.EqualTo(runSprite.pixelsPerUnit));
        for (int frame = 0; frame < 8; frame++) yield return null;
        Assert.That(visuals.transform.localScale.y, Is.LessThan(0.8f));
        Assert.That((float)Read(visuals, "blend"), Is.EqualTo(1f));
        CapturePresentation("slide");
        Call(Manager("GameStateManager"), "TogglePause");
        yield return null;
        yield return null;
        CapturePresentation("pause-landscape");
        Screen.SetResolution(720, 1280, FullScreenMode.Windowed);
        yield return null;
        yield return null;
        CapturePresentation("pause-portrait", 720, 1280);
        Call(Manager("GameStateManager"), "TogglePause");
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
        for (int frame = 0; frame < 30; frame++) yield return null;
        Assert.That(renderer.sprite, Is.EqualTo(runSprite));
        Assert.That(visuals.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(player.GetComponent<CapsuleCollider2D>().size, Is.EqualTo(size));
    }

    // Literal template requirements: an Inspector edit must not change the
    // baseline tuning silently. Customer retuning requires updated tests.
    [UnityTest]
    public IEnumerator SerializedPlayerTuningMatchesTemplateContract()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;

        var player = Find("PlayerScript");
        Assert.That((float)Read(player, "JumpForce"), Is.EqualTo(12f).Within(0.001f));
        Assert.That((float)Read(player, "slideDuration"), Is.EqualTo(0.4f).Within(0.001f));
        Assert.That((float)Read(player, "jumpBufferDuration"), Is.EqualTo(0.12f).Within(0.001f));
        Assert.That((float)Read(player, "scoreMultiplier"), Is.EqualTo(4f).Within(0.001f));
        Assert.That((float)Property(Manager("ScoreManager"), "ScorePerSecond"),
            Is.EqualTo(4f).Within(0.001f),
            "PlayerScript.OnEnable must apply the actual scene score multiplier.");
    }

    [UnityTest]
    public IEnumerator ScoreAccrualUsesFourPointsPerSecondAndStopsOutsidePlaying()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;

        ((Behaviour)Find("SpikeGenerator")).enabled = false;
        var score = Manager("ScoreManager");
        var state = Manager("GameStateManager");
        Call(score, "ResetScore");
        Play();

        // Explicitly drive Update at a captured delta, not wall-clock time.
        Time.captureDeltaTime = 0.02f;
        float before = (float)Property(score, "CurrentScore");
        Call(score, "Update");
        Assert.That((float)Property(score, "CurrentScore") - before,
            Is.EqualTo(0.08f).Within(0.0005f),
            "4 points/second * 0.02 seconds must award 0.08 points.");

        Call(state, "SetState", Enum.Parse(GameType("GameState"), "Paused"));
        float paused = (float)Property(score, "CurrentScore");
        Call(score, "Update");
        Assert.That((float)Property(score, "CurrentScore"), Is.EqualTo(paused));

        Call(state, "SetState", Enum.Parse(GameType("GameState"), "GameOver"));
        Call(score, "Update");
        Assert.That((float)Property(score, "CurrentScore"), Is.EqualTo(paused));
        Call(score, "ResetScore");
        Assert.That((float)Property(score, "CurrentScore"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator DifficultyCurveHonorsFixedMilestonesSpeedCapAndReset()
    {
        OpenGame();
        yield return new EnterPlayMode();
        yield return null;

        // The live spawner can otherwise update the viewport-dependent speed cap.
        ((Behaviour)Find("SpikeGenerator")).enabled = false;
        var score = Manager("ScoreManager");
        var speed = Manager("SpeedManager");
        Play();

        Assert.That((float)Read(speed, "minSpeed"), Is.EqualTo(5f));
        Assert.That((float)Read(speed, "maxSpeed"), Is.EqualTo(90f));
        Assert.That((float)Read(speed, "progressToMax"), Is.EqualTo(600f));
        Assert.That((bool)Read(speed, "useScoreForProgress"), Is.True);

        Call(score, "UpdateScore", 0f);
        Call(speed, "Update");
        Assert.That((float)Property(speed, "CurrentSpeed"), Is.EqualTo(5f).Within(0.001f));

        Call(score, "UpdateScore", 300f);
        Call(speed, "Update");
        Assert.That((float)Property(speed, "CurrentNormalized"), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That((float)Property(speed, "CurrentSpeed"),
            Is.EqualTo(34.75f).Within(0.01f),
            "Halfway through progress, the configured curve samples 0.35.");

        Call(score, "UpdateScore", 600f);
        Call(speed, "Update");
        Assert.That((float)Property(speed, "CurrentSpeed"), Is.EqualTo(90f).Within(0.01f));

        Call(speed, "SetGameplaySpeedLimit", 20f);
        Assert.That((float)Property(speed, "CurrentSpeed"), Is.EqualTo(20f).Within(0.001f));

        Call(speed, "ResetSpeed");
        Assert.That((float)Property(speed, "CurrentSpeed"), Is.EqualTo(5f).Within(0.001f));
        Assert.That(float.IsPositiveInfinity((float)Property(speed, "GameplaySpeedLimit")), Is.True);
        Assert.That((float)Property(speed, "CurrentNormalized"), Is.Zero);
    }
}
