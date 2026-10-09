using System;
using System.Collections.Generic;
using ReAction.Actions;
using ReAction.Core;

namespace ReAction.Replay
{
    /// <summary>
    /// FINAL RUN: 기록된 프롬프트들을 기록된 틱에 그대로 다시 실행한다.
    /// 월드는 고정 틱으로만 진행하고 같은 틱에 같은 행동을 넣으므로, 원래 시도와 똑같이 재현된다.
    /// 계획 중이던 느린 시간도 같은 틱 수만큼 정상 속도로 흘러간다 (멈춤 없이 연속 재생).
    /// </summary>
    public sealed class RunReplayer
    {
        private readonly IReadOnlyList<PlanRecord> _plans;
        private readonly Func<ActionRequest, PlayerAction> _createAction;
        private int _next;

        public RunReplayer(RunRecord record, Func<ActionRequest, PlayerAction> createAction)
        {
            _plans = new List<PlanRecord>(record.Plans);
            _createAction = createAction;
        }

        /// <summary>지금 재생 중인(가장 최근에 시작한) 프롬프트. 없으면 null.</summary>
        public PlanRecord Current { get; private set; }

        public bool AllStarted => _next >= _plans.Count;

        /// <summary>마지막 프롬프트가 시작되는 틱 (재생이 너무 길어지는지 판단용).</summary>
        public int LastPlanTick => _plans.Count > 0 ? _plans[_plans.Count - 1].Tick : 0;

        /// <summary>
        /// 틱을 진행하기 직전에 부른다. 이번 틱에 시작해야 하는 프롬프트가 있으면 실행기에 넣는다.
        /// 넣지 못했으면 false (재현이 어긋났다는 뜻).
        /// </summary>
        public bool BeforeStep(WorldSimulation sim)
        {
            while (_next < _plans.Count && _plans[_next].Tick <= sim.Tick)
            {
                var plan = _plans[_next++];
                if (plan.Tick < sim.Tick) return false;

                var actions = new List<PlayerAction>(plan.Actions.Count);
                foreach (var request in plan.Actions)
                {
                    var action = _createAction(request);
                    if (action == null) return false;
                    actions.Add(action);
                }
                if (!sim.Executor.TryStart(actions)) return false;
                Current = plan;
            }
            return true;
        }
    }
}
