using System;

namespace SpaceBurst
{
    public sealed class DeterministicRngState
    {
        public uint State { get; private set; }

        public DeterministicRngState(uint seed)
        {
            State = seed == 0 ? 1u : seed;
        }

        public void Restore(uint state)
        {
            State = state == 0 ? 1u : state;
        }

        public uint NextUInt()
        {
            uint value = State;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            State = value == 0 ? 1u : value;
            return State;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;

            uint range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % range);
        }

        public float NextFloat(float minInclusive, float maxInclusive)
        {
            if (maxInclusive <= minInclusive)
                return minInclusive;

            return minInclusive + (NextUInt() / (float)uint.MaxValue) * (maxInclusive - minInclusive);
        }

        public double NextDouble()
        {
            return NextUInt() / (double)uint.MaxValue;
        }
    }
}
