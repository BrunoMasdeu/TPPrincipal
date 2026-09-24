using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public class ExamplePlayModeTests
{
    [UnityTest]
    public IEnumerator Test_Coroutine()
    {
        yield return null;

        Assert.Pass();
    }
}