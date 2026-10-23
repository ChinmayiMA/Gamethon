using System;
using UnityEngine;

namespace EchoDelete
{
    public sealed class MemoryManager : MonoBehaviour
    {
        [SerializeField, Min(1)] private int startingRam = 100;
        public int RemainingRam { get; private set; }
        public event Action<int> RamChanged;

        private void Awake() => RemainingRam = Mathf.Max(1, startingRam);

        public bool TrySpend(int amount)
        {
            if (amount <= 0 || RemainingRam < amount) return false;
            RemainingRam -= amount;
            RamChanged?.Invoke(RemainingRam);
            return true;
        }
    }
}
