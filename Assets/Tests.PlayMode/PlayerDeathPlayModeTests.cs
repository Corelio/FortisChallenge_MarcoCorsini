using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Cinemachine;

using Platformer.Core;       // Simulation
using Platformer.Gameplay;   // events
using Platformer.Mechanics;  // PlayerController, Health, EnemyController

public class PlayerDeathPlayModeTests
{
    // Ensure only one AudioListener exists in the scene to avoid warnings
    void EnsureSingleAudioListener()
    {
        var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        for (int i = 1; i < listeners.Length; i++) listeners[i].enabled = false;
        if (listeners.Length == 0) new GameObject("TestAudioListener").AddComponent<AudioListener>();
    }

    // Drive Simulation.Tick() so scheduled events execute each frame
    private class TestSimulationDriver : MonoBehaviour
    {
        void Update() => Simulation.Tick();
    }

    // Ensure a TestSimulationDriver exists in the scene
    void EnsureSimulationDriver()
    {
        if (Object.FindFirstObjectByType<TestSimulationDriver>() == null)
            new GameObject("TestSimulationDriver").AddComponent<TestSimulationDriver>();
    }

    // Create a barebones PlayerController with required components at pos
    PlayerController CreateBarePlayer(Vector3 pos)
    {
        // Create GameObject
        var go = new GameObject("Player");
        go.transform.position = pos;

        // Components used by death code
        go.AddComponent<SpriteRenderer>();
        var animator = go.AddComponent<Animator>();           // <-- add Animator BEFORE PlayerController
        TestAnimatorUtil.EnsureAnimatorHasController(animator);              // avoid warnings

        go.AddComponent<BoxCollider2D>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        go.AddComponent<Health>(); 

        var pc = go.AddComponent<PlayerController>(); // Awake runs here and caches Animator
        pc.enabled = false;                           // avoid Update() (input/anim spam)
        pc.controlEnabled = true;

        // Wire the shared model like the game does
        var model = Platformer.Core.Simulation.GetModel<Platformer.Model.PlatformerModel>();
        model.player = pc;

        // Add a minimal Cinemachine vcam so PlayerDeath can clear Follow/LookAt safely
        var vcamGO = new GameObject("VCam");
        var vcam = vcamGO.AddComponent<CinemachineCamera>();
        model.virtualCamera = vcam;

        return pc;
    }

    // Create a barebones EnemyController with required components at pos
    EnemyController CreateEnemy(Vector3 pos, Vector2 size)
    {
        // Create GameObject
        var go = new GameObject("Enemy");
        go.transform.position = pos;

        go.AddComponent<SpriteRenderer>();
        var animator = go.AddComponent<Animator>();            // <-- add Animator BEFORE PlayerController
        TestAnimatorUtil.EnsureAnimatorHasController(animator);
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = false;
        col.size = size;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        return go.AddComponent<EnemyController>(); 
    }

    // 1) Death via "entered death zone" path — schedule the event directly (more reliable in blank scenes)
    [UnityTest]
    public IEnumerator Player_Entered_DeathZone_Schedules_Death_And_Disables_Control()
    {
        EnsureSimulationDriver();
        EnsureSingleAudioListener();

        var player = CreateBarePlayer(Vector3.zero);

        // Simulate DeathZone trigger: schedule the gameplay event directly
        Simulation.Schedule<PlayerEnteredDeathZone>();

        yield return null; // Simulation.Tick executes -> PlayerDeath

        Assert.IsFalse(player.health.IsAlive, "Player should be dead after entering a death zone.");
        Assert.IsFalse(player.controlEnabled, "Player control should be disabled on death.");
    }

    // 2) Death via enemy body collision (not a stomp) — schedule the event directly
    [UnityTest]
    public IEnumerator Player_Colliding_With_Enemy_Body_Dies()
    {
        EnsureSimulationDriver();
        EnsureSingleAudioListener();

        var player = CreateBarePlayer(Vector3.zero);
        var enemy = CreateEnemy(new Vector3(0, 0.6f, 0), new Vector2(1f, 1f));
        yield return null; // Let physics register colliders

        var ev = Platformer.Core.Simulation.Schedule<PlayerEnemyCollision>();
        ev.player = player;
        ev.enemy = enemy;

        yield return null; // Simulation.Tick executes -> PlayerDeath

        Assert.IsFalse(player.health.IsAlive);
        Assert.IsFalse(player.controlEnabled);
    }

    // 3) Health reaches zero: player IsAlive becomes false.
    [UnityTest]
    public IEnumerator Player_Health_To_Zero_Marks_NotAlive()
    {
        EnsureSimulationDriver();
        EnsureSingleAudioListener();

        var player = CreateBarePlayer(Vector3.zero);

        Assert.IsTrue(player.health.IsAlive, "Player should start alive.");

        // Drive HP to zero regardless of defaults
        int safety = 10;
        while (player.health.IsAlive && safety-- > 0)
            player.health.Decrement();

        // Let the queue process HealthIsZero -> (PlayerDeath scheduled but will early-exit)
        yield return null;
        yield return null;

        Assert.IsFalse(player.health.IsAlive, "HP should be zero after decrements.");
    }

    // 4) Explicit PlayerDeath event: disables control regardless of IsAlive start state.
    [UnityTest]
    public IEnumerator Explicit_PlayerDeath_Event_Disables_Control()
    {
        EnsureSimulationDriver();
        EnsureSingleAudioListener();

        var player = CreateBarePlayer(Vector3.zero);
        Assert.IsTrue(player.health.IsAlive);

        // Schedule PlayerDeath while IsAlive == true → Execute() runs and disables control.
        Platformer.Core.Simulation.Schedule<Platformer.Gameplay.PlayerDeath>();

        yield return null;

        Assert.IsFalse(player.health.IsAlive, "Die() is called in PlayerDeath.");
        Assert.IsFalse(player.controlEnabled, "Control should be disabled by PlayerDeath.");
    }
}