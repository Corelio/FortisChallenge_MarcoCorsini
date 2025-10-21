using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Platformer.Mechanics;

// Helper class to create simple test sprites
static class TestSprites
{
    // Create an array of simple white sprites for testing
    public static Sprite[] MakeFrames(int count)
    {
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            int w = 2, h = 2;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var colors = new Color[w * h];
            for (int p = 0; p < colors.Length; p++) colors[p] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            frames[i] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
        return frames;
    }
}

public class TokenPlaymodeTests
{   
    // Helper to create a silent AudioClip to avoid null references
    static AudioClip MakeSilentClip(float seconds = 0.25f, int sampleRate = 44100)
    {
        int length = Mathf.Max(1, Mathf.RoundToInt(seconds * sampleRate));
        return AudioClip.Create("TestSilent", length, 1, sampleRate, false);
    }

    // Ensure only one AudioListener exists in the scene to avoid warnings
    void EnsureSingleAudioListener()
    {
        var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        for (int i = 1; i < listeners.Length; i++) listeners[i].enabled = false;
        if (listeners.Length == 0) new GameObject("TestAudioListener").AddComponent<AudioListener>();
    }

    // Helper to create a TokenInstance at given position, with specified idle/collected frame counts
    TokenInstance CreateToken(Vector3 pos, out SpriteRenderer sr, int idleFrames = 3, int collectedFrames = 3)
    {
        var go = new GameObject("Token");
        go.transform.position = pos;

        sr = go.AddComponent<SpriteRenderer>();
        var animator = go.AddComponent<Animator>();           // <-- add Animator BEFORE PlayerController
        TestAnimatorUtil.EnsureAnimatorHasController(animator);              // avoid warnings
        var col = go.AddComponent<CircleCollider2D>(); col.isTrigger = true;

        var token = go.AddComponent<TokenInstance>();
        token.idleAnimation = TestSprites.MakeFrames(idleFrames);
        token.collectedAnimation = TestSprites.MakeFrames(collectedFrames);
        sr.sprite = token.idleAnimation[0];

        token.tokenCollectAudio = MakeSilentClip();

        return token;
    }

    // Simple non-player GameObject (Enemy/Alien) to test collisions
    GameObject CreateNonPlayer(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.AddComponent<BoxCollider2D>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        return go;
    }

    // Simple player GameObject with PlayerController to trigger token collection
    GameObject CreatePlayer(Vector3 pos)
    {
        var go = new GameObject("Player");
        go.tag = "Player";
        go.transform.position = pos;
        go.AddComponent<BoxCollider2D>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        go.AddComponent<SpriteRenderer>();
        var animator = go.AddComponent<Animator>();           // <-- add Animator BEFORE PlayerController
        TestAnimatorUtil.EnsureAnimatorHasController(animator);              // avoid warnings
        var pc = go.AddComponent<Platformer.Mechanics.PlayerController>(); // presence is enough for TokenInstance to detect
        pc.enabled = false; // disable to avoid side effects
        return go;
    }

    // 1) Token exists and idles (sprite is assigned)
    [UnityTest]
    public IEnumerator Token_Spawns_With_Idle_Sprite()
    {
        EnsureSingleAudioListener();    

        var token = CreateToken(Vector3.zero, out var sr, idleFrames: 2, collectedFrames: 2);
        yield return null; // allow Awake/Start
        Assert.IsNotNull(sr.sprite, "Token should have an idle sprite assigned.");
        Assert.AreSame(token.idleAnimation[0], sr.sprite, "Token should start on first idle frame.");
    }

    // 2) Enemy/Alien collisions do nothing: token remains active and sprite stays idle
    [UnityTest]
    public IEnumerator NonPlayer_Collision_Does_Not_Collect()
    {
        EnsureSingleAudioListener();

        // Enemy case
        {
            var token = CreateToken(Vector3.zero, out var sr, idleFrames: 2, collectedFrames: 2);
            yield return null;
            var firstIdle = sr.sprite;

            var enemy = CreateNonPlayer("Enemy", Vector3.zero);
            yield return new WaitForFixedUpdate();

            // Verify token still active and sprite unchanged
            Assert.IsTrue(token.gameObject.activeInHierarchy);
            Assert.AreSame(firstIdle, sr.sprite);
            // Clean up
            Object.Destroy(enemy);
            Object.Destroy(token.gameObject);
        }

        // Alien case
        {
            var token = CreateToken(Vector3.zero, out var sr, idleFrames: 2, collectedFrames: 2);
            yield return null;
            var firstIdle = sr.sprite;

            var alien = CreateNonPlayer("Alien", Vector3.zero);
            yield return new WaitForFixedUpdate();

            // Verify token still active and sprite unchanged
            Assert.IsTrue(token.gameObject.activeInHierarchy);
            Assert.AreSame(firstIdle, sr.sprite);
            // Clean up
            Object.Destroy(alien);
            Object.Destroy(token.gameObject);
        }
    }

    // 3) Player overlap collects: internal state switches to collected (sprites -> collectedAnimation, frame -> 0)
    [UnityTest]
    public IEnumerator Player_Collision_Switches_To_Collected_State()
    {
        EnsureSingleAudioListener();

        // Add a listener to silence audio warnings during tests
        new GameObject("TestAudioListener").AddComponent<AudioListener>();

        var token = CreateToken(Vector3.zero, out var sr, idleFrames: 2, collectedFrames: 3);
        yield return null;

        // Overlap player with token to trigger OnTriggerEnter2D -> OnPlayerEnter
        CreatePlayer(Vector3.zero);
        yield return new WaitForFixedUpdate();

        // Use reflection to read TokenInstance internals:
        var t = token.GetType();
        var spritesField = t.GetField("sprites", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var frameField = t.GetField("frame", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var collectedField = t.GetField("collected", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // Collect internal state values
        var sprites = (Sprite[])spritesField.GetValue(token);
        var frame = (int)frameField.GetValue(token);
        var collected = (bool)collectedField.GetValue(token);

        // On player enter: frame must reset to 0 and sprites must switch to collectedAnimation.
        Assert.AreEqual(0, frame, "On player collision, token should reset frame to 0.");
        Assert.AreSame(token.collectedAnimation, sprites, "On player collision, token should switch to collectedAnimation.");
        Assert.IsFalse(collected, "Without a controller, 'collected' remains false by design.");
    }
}