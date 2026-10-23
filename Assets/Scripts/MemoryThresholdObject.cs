using UnityEngine;

namespace EchoDelete
{
    public sealed class MemoryThresholdObject : MonoBehaviour
    {
        private MemoryManager memory;
        private GameObject target;
        private int threshold;

        public void Initialize(MemoryManager manager, GameObject objectToHide, int hideAtOrBelow)
        {
            if (memory != null) memory.RamChanged -= Refresh;
            memory = manager;
            target = objectToHide;
            threshold = hideAtOrBelow;
            memory.RamChanged += Refresh;
            Refresh(memory.RemainingRam);
        }

        private void Refresh(int ram) => target.SetActive(ram > threshold);
        private void OnDestroy()
        {
            if (memory != null) memory.RamChanged -= Refresh;
        }
    }
}
