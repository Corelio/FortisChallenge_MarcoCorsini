using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

static class TestAnimatorUtil
{
    static RuntimeAnimatorController _cached;
    public static RuntimeAnimatorController GetOrCreate()
    {
#if UNITY_EDITOR
        if (_cached == null)
        {
            var ac = new AnimatorController();
            ac.AddLayer("Base");
            var sm = ac.layers[0].stateMachine;
            var idle = sm.AddState("Idle");
            sm.defaultState = idle;  // <- critical: must have a default state
            
            // Add the parameters used by game code
            ac.AddParameter("hurt", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("dead", AnimatorControllerParameterType.Bool);
            ac.AddParameter("grounded", AnimatorControllerParameterType.Bool);
            ac.AddParameter("velocityX", AnimatorControllerParameterType.Float);

            _cached = ac;
        }
        return _cached;
#else
        return null;
#endif
    }

    public static void EnsureAnimatorHasController(Animator animator)
    {
        if (animator != null && animator.runtimeAnimatorController == null)
            animator.runtimeAnimatorController = GetOrCreate();
    }
}

public class SanityCheck
{
    [UnityTest]
    public IEnumerator SanityCheckPlayModeRunsOneFrame()
    {
        // Wait a frame to confirm the engine loop is running
        yield return null;

        // If we reached here, PlayMode is running fine
        Assert.Pass("PlayMode environment OK!");
    }
}
