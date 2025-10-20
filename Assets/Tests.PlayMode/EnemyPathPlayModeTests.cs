using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using Platformer.Mechanics;

public class EnemyPathPlayModeTests
{
    // helper to make ground boxes
    GameObject MakeGroundBox(string name, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(center.x, center.y, 0);
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        return go;
    }

    // helper to make wall boxes
    GameObject MakeWallBox(string name, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(center.x, center.y, 0);
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        return go;
    }

    // helper to make a PatrolPath
    PatrolPath MakePath(Transform parent, Vector2 startLocal, Vector2 endLocal)
    {
        var go = new GameObject("PatrolPath");
        if (parent) go.transform.SetParent(parent, false);
        var path = go.AddComponent<PatrolPath>();
        path.startPosition = startLocal;
        path.endPosition = endLocal;
        return path;
    }

    // 
    struct PathValidationOptions
    {
        public float sampleStep;       // world distance between samples along the path
        public float groundProbe;      // how far to raycast "down" to find ground
        public float wallProbeRadius;  // small radius to detect overlaps with walls
        public Vector2 downDirection;  // optional override (defaults to Vector2.down)
    }

    class PathValidator
    {
        readonly List<Collider2D> _walls;
        readonly List<Collider2D> _grounds;
        readonly PathValidationOptions _opt;

        public PathValidator(List<Collider2D> walls, List<Collider2D> grounds, PathValidationOptions opt)
        {
            _walls = walls; _grounds = grounds; _opt = opt;
        }

        public bool Validate(PatrolPath path, out string reason)
        {
            // endpoints in world space
            Vector2 a = path.transform.TransformPoint(path.startPosition);
            Vector2 b = path.transform.TransformPoint(path.endPosition);
            float length = Vector2.Distance(a, b);
            if (length <= Mathf.Epsilon)
            {
                reason = "Path has zero length.";
                return false;
            }

            int samples = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(0.01f, _opt.sampleStep)));
            Vector2 down = (_opt.downDirection == Vector2.zero ? Vector2.down : _opt.downDirection).normalized;

            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector2 p = Vector2.Lerp(a, b, t);

                // 1) walls: use OverlapCircleAll and see if any hit is in our wall set
                var hits = Physics2D.OverlapCircleAll(p, _opt.wallProbeRadius);
                for (int h = 0; h < hits.Length; h++)
                {
                    var c = hits[h];
                    if (c != null && _walls.Contains(c))
                    {
                        reason = $"Path intersects wall near {p}.";
                        return false;
                    }
                }

                // 2) ground below: small upward offset to avoid embedded starts
                var origin = p - down * 0.02f;
                var ray = Physics2D.Raycast(origin, down, _opt.groundProbe);
                bool hasGround = ray.collider != null && _grounds.Contains(ray.collider);

                if (!hasGround)
                {
                    reason = $"No ground beneath path near {p}.";
                    return false;
                }
            }

            reason = "";
            return true;
        }
    }

    // default options for path validation
    readonly PathValidationOptions _opt = new PathValidationOptions
    {
        sampleStep = 0.2f,
        groundProbe = 2.0f,
        wallProbeRadius = 0.05f,
        downDirection = Vector2.down
    };

    // 1) Happy path: straight line over continuous ground, no walls
    [UnityTest]
    public System.Collections.IEnumerator Path_HasContinuousGround_And_NoWalls_IsValid()
    {
        var ground = MakeGroundBox("Ground", new Vector2(0, -0.5f), new Vector2(10f, 1f));
        var walls = new List<Collider2D>();
        var grounds = new List<Collider2D> { ground.GetComponent<Collider2D>() };

        var path = MakePath(null, new Vector2(-2, 0), new Vector2(2, 0));
        yield return null;

        var validator = new PathValidator(walls, grounds, _opt);
        Assert.IsTrue(validator.Validate(path, out var reason), reason);
    }

    // 2) Wall overlap should invalidate path
    [UnityTest]
    public System.Collections.IEnumerator Path_Intersecting_Wall_IsInvalid()
    {
        var ground = MakeGroundBox("Ground", new Vector2(0, -0.5f), new Vector2(10f, 1f));
        var wall = MakeWallBox("Wall", new Vector2(0, 0.5f), new Vector2(0.5f, 2f));
        var walls = new List<Collider2D> { wall.GetComponent<Collider2D>() };
        var grounds = new List<Collider2D> { ground.GetComponent<Collider2D>() };

        var path = MakePath(null, new Vector2(-2, 0), new Vector2(2, 0));
        yield return null;

        var validator = new PathValidator(walls, grounds, _opt);
        Assert.IsFalse(validator.Validate(path, out var reason), "Expected wall intersection to fail.");
        Assert.That(reason.ToLower(), Does.Contain("wall"));
    }

    // 3) Gap in ground beneath should invalidate path
    [UnityTest]
    public System.Collections.IEnumerator Path_Over_Gap_IsInvalid()
    {
        var left = MakeGroundBox("GroundLeft", new Vector2(-1.5f, -0.5f), new Vector2(2f, 1f));
        var right = MakeGroundBox("GroundRight", new Vector2(1.5f, -0.5f), new Vector2(2f, 1f));
        var grounds = new List<Collider2D> { left.GetComponent<Collider2D>(), right.GetComponent<Collider2D>() };
        var walls = new List<Collider2D>();

        var path = MakePath(null, new Vector2(-2, 0), new Vector2(2, 0)); // crosses the gap near x≈0
        yield return null;

        var validator = new PathValidator(walls, grounds, _opt);
        Assert.IsFalse(validator.Validate(path, out var reason), "Expected missing ground to fail.");
        Assert.That(reason.ToLower(), Does.Contain("no ground"));
    }

    // 4) Degenerate path (start == end) should be rejected
    [Test]
    public void Path_Zero_Length_IsInvalid()
    {
        var path = MakePath(null, Vector2.zero, Vector2.zero);
        var validator = new PathValidator(new List<Collider2D>(), new List<Collider2D>(), _opt);
        Assert.IsFalse(validator.Validate(path, out var reason));
        Assert.That(reason.ToLower(), Does.Contain("zero length"));
    }

    // 5) Platform edge guard: if platform ends before path end, validator should fail near the edge
    [UnityTest]
    public System.Collections.IEnumerator Path_Extends_Beyond_Platform_Fails()
    {
        var ground = MakeGroundBox("Ground", new Vector2(-0.5f, -0.5f), new Vector2(3f, 1f)); // spans x ~ [-2, +1]
        var grounds = new List<Collider2D> { ground.GetComponent<Collider2D>() };
        var walls = new List<Collider2D>();

        var path = MakePath(null, new Vector2(-2, 0), new Vector2(2, 0)); // extends past +1
        yield return null;

        var validator = new PathValidator(walls, grounds, _opt);
        Assert.IsFalse(validator.Validate(path, out var reason), "Expected end of path to overhang platform.");
        Assert.That(reason.ToLower(), Does.Contain("no ground"));
    }
}