using System;
using System.Collections.Generic;
using ReAction.Actions;
using ReAction.Player;
using UnityEngine;

namespace ReAction.Core
{
    /// <summary>고정 틱마다 진행되는 월드 요소 (적 AI 등). 실행 중에만 호출된다.</summary>
    public interface ISimulated
    {
        /// <summary>물리 진행 전: 이동 결정, 공격 판정.</summary>
        void PreTick(int tick);

        /// <summary>물리 진행 후: 위치에 따른 후처리.</summary>
        void PostTick(int tick);
    }

    /// <summary>시도(run)마다 초기 상태로 되돌려야 하는 월드 요소. 플레이어 지식(안개 해제)은 여기에 넣지 않는다.</summary>
    public interface IResettable
    {
        void CaptureInitialState();
        void ResetToInitialState();
    }

    /// <summary>월드 상태 리셋. 리셋 범위는 등록된 IResettable뿐이다 (ZoneKnowledge는 포함하지 않음).</summary>
    public sealed class WorldResetter
    {
        private readonly List<IResettable> _targets = new List<IResettable>();

        public void Register(IResettable target)
        {
            if (target != null && !_targets.Contains(target)) _targets.Add(target);
        }

        public void CaptureAll()
        {
            foreach (var t in _targets) t.CaptureInitialState();
        }

        public void ResetAll()
        {
            foreach (var t in _targets) t.ResetToInitialState();
        }
    }

    /// <summary>
    /// 한 틱의 진행 순서를 정의한다: 플레이어 행동 → 월드 요소 PreTick → 물리 → PostTick.
    /// GameController와 결정론 테스트가 같은 코드를 쓴다.
    /// </summary>
    public sealed class WorldSimulation
    {
        private readonly List<ISimulated> _simulated = new List<ISimulated>();

        private readonly PhysicsScene2D _physics;

        public WorldSimulation(PlayerMotor player)
        {
            Player = player;
            _physics = player.PhysicsScene;
        }

        public PlayerMotor Player { get; }
        public ActionExecutor Executor { get; } = new ActionExecutor();
        public int Tick { get; private set; }

        /// <summary>틱이 끝날 때마다 (틱 번호).</summary>
        public event Action<int> Ticked;

        public void Register(ISimulated simulated)
        {
            if (simulated != null && !_simulated.Contains(simulated)) _simulated.Add(simulated);
        }

        public void Step()
        {
            if (Executor.IsRunning && !Player.IsDead) Executor.Tick(Player);
            foreach (var s in _simulated) s.PreTick(Tick);

            _physics.Simulate(DeterministicClock.TickDuration);
            Tick++;

            foreach (var s in _simulated) s.PostTick(Tick);
            Ticked?.Invoke(Tick);
        }

        public void ResetTicks() => Tick = 0;
    }
}
