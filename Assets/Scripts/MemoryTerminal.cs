using UnityEngine;

namespace EchoDelete
{
    public sealed class MemoryTerminal : MonoBehaviour
    {
        public const int Cost = 25;
        public string Status { get; private set; } = "Terminal ready.";
        public void Interact(MemoryManager memory)
        {
            Status = memory.TrySpend(Cost)
                ? "Memory deleted. World integrity reduced."
                : "Insufficient RAM. Restart Play Mode to reset.";
        }
    }
}
