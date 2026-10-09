using System;
using ReAction.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReAction.Replay
{
    /// <summary>
    /// 사망 후 되감기 연출. 기록된 시도를 역순으로 빠르게 보여준다.
    /// 길이는 시도 길이와 상관없이 최대 _maxSeconds로 압축하고, 클릭이나 키 입력으로 건너뛸 수 있다.
    /// </summary>
    public sealed class RewindPlayer : MonoBehaviour
    {
        [SerializeField] private float _maxSeconds = 1.5f;
        [Tooltip("시도가 짧을 때 되감기가 너무 빨리 끝나지 않게 하는 최소 길이 (초)")]
        [SerializeField] private float _minSeconds = 0.4f;

        private RewindRecorder _recorder;
        private Action _onFinished;
        private float _elapsed;
        private float _duration;

        public bool IsPlaying => _recorder != null;

        public void Play(RewindRecorder recorder, Action onFinished)
        {
            _onFinished = onFinished;
            if (recorder.FrameCount == 0)
            {
                Finish();
                return;
            }

            _recorder = recorder;
            _elapsed = 0f;
            float natural = recorder.FrameCount * DeterministicClock.TickDuration;
            _duration = Mathf.Clamp(natural, _minSeconds, _maxSeconds);
            foreach (var track in recorder.Tracks) track.Animator.enabled = false;
            recorder.Apply(recorder.FrameCount - 1);
        }

        private void Update()
        {
            if (_recorder == null) return;

            if (SkipPressed())
            {
                Finish();
                return;
            }

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            int last = _recorder.FrameCount - 1;
            _recorder.Apply(Mathf.RoundToInt(last * (1f - t)));
            if (t >= 1f) Finish();
        }

        /// <summary>외부(테스트·검증)에서 즉시 끝낼 때.</summary>
        public void Skip()
        {
            if (_recorder != null) Finish();
        }

        private void Finish()
        {
            if (_recorder != null)
            {
                _recorder.Apply(0);
                foreach (var track in _recorder.Tracks) track.Animator.enabled = true;
            }
            _recorder = null;
            var callback = _onFinished;
            _onFinished = null;
            callback?.Invoke();
        }

        private static bool SkipPressed()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                   (keyboard != null && keyboard.anyKey.wasPressedThisFrame);
        }
    }
}
