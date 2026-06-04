using System;
using System.Threading;

namespace AssetStudio
{
    public static class Progress
    {
        private const int InstanceCount = 2;
        private static readonly AsyncLocal<State> CurrentState = new AsyncLocal<State>();

        public static int MaxCount => InstanceCount;

        public static IProgress<int> Default //alias
        {
            get => StateForCurrentFlow.Instances[0];
            set => SetInstance(0, value);
        }

        public static void Reset(int index = 0)
        {
            ValidateIndex(index);
            var state = StateForCurrentFlow;
            state.PreValues[index] = 0;
            state.Instances[index].Report(0);
        }

        public static void Report(int current, int total, int index = 0)
        {
            var value = (int)(current * 100f / total);
            _Report(value, index);
        }

        private static void _Report(int value, int index)
        {
            ValidateIndex(index);
            var state = StateForCurrentFlow;
            if (value > state.PreValues[index])
            {
                state.PreValues[index] = value;
                state.Instances[index].Report(value);
            }
        }

        public static void SetInstance(int index, IProgress<int> progress)
        {
            if (progress == null)
                throw new ArgumentNullException(nameof(progress));
            ValidateIndex(index);

            StateForCurrentFlow.Instances[index] = progress;
        }

        public static IProgress<int> GetInstance(int index)
        {
            ValidateIndex(index);

            return StateForCurrentFlow.Instances[index];
        }

        private static State StateForCurrentFlow
        {
            get
            {
                var state = CurrentState.Value;
                if (state == null)
                {
                    state = new State();
                    CurrentState.Value = state;
                }
                return state;
            }
        }

        private static void ValidateIndex(int index)
        {
            if (index < 0 || index >= MaxCount)
                throw new ArgumentOutOfRangeException(nameof(index));
        }

        private sealed class State
        {
            public IProgress<int>[] Instances { get; } =
            {
                new Progress<int>(),
                new Progress<int>(),
            };

            public int[] PreValues { get; } = new int[InstanceCount];
        }
    }
}
