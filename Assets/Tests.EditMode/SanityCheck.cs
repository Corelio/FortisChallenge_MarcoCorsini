// Sanity check to ensure EditMode tests run correctly

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SanityCheck
{
    [Test]
    public void SanitCheckEditModeWorks()
    {
        // Just a basic truth check
        Assert.AreEqual(2, 1 + 1, "Math still works!");
    }
}
