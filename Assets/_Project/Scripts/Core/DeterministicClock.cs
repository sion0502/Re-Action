namespace ReAction.Core
{
    /// <summary>
    /// 고정 틱 시뮬레이션 시계. 프레임 시간을 틱 수로 바꾼다 (틱 번호는 WorldSimulation이 센다).
    /// </summary>
    public sealed class DeterministicClock
    {
        public const int TicksPerSecond = 60;
        public const float TickDuration = 1f / TicksPerSecond;

        // 프레임이 크게 밀려도 한 프레임에 너무 많은 틱을 돌리지 않는다.
        private const int MaxTicksPerFrame = 8;

        private float _accumulator;

        /// <summary>프레임 시간을 누적하고, 이번 프레임에 돌릴 틱 수를 반환한다.</summary>
        public int Advance(float deltaTime)
        {
            _accumulator += deltaTime;
            int steps = 0;
            while (_accumulator >= TickDuration && steps < MaxTicksPerFrame)
            {
                _accumulator -= TickDuration;
                steps++;
            }
            if (steps == MaxTicksPerFrame) _accumulator = 0f;
            return steps;
        }

        /// <summary>정지할 때 남은 누적 시간을 버려서 재개 시 틱이 몰리지 않게 한다.</summary>
        public void Pause() => _accumulator = 0f;

        public static int SecondsToTicks(float seconds) =>
            (int)System.Math.Round(seconds * TicksPerSecond);
    }
}
