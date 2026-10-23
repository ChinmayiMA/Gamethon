using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EchoDelete;

public static class MemoryTestSetup
{
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("MemoryTest").AddComponent<MemoryTestController>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MemoryTest.unity");
        var host = new GameObject("RAM validation");
        var memory = host.AddComponent<MemoryManager>();
        // Awake is invoked explicitly because this check runs outside Play Mode.
        memory.SendMessage("Awake");
        int events = 0;
        memory.RamChanged += _ => events++;
        var fragment = new GameObject("Validation fragment");
        host.AddComponent<MemoryThresholdObject>().Initialize(memory, fragment, 75);
        Require(memory.RemainingRam == 100 && fragment.activeSelf, "Initial state");
        Require(!memory.TrySpend(0) && !memory.TrySpend(-25) && !memory.TrySpend(101), "Invalid spend");
        Require(memory.TrySpend(25) && memory.RemainingRam == 75 && !fragment.activeSelf, "Threshold boundary");
        Require(memory.TrySpend(75) && memory.RemainingRam == 0, "Exact depletion");
        Require(!memory.TrySpend(25) && events == 2, "Depletion and events");
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(fragment);
        Debug.Log("MEMORY_TEST_VALIDATION_PASSED");
    }
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new System.Exception("MemoryTest failed: " + label);
    }
}
