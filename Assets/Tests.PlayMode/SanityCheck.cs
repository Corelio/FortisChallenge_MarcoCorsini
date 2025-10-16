using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
