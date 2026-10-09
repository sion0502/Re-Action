using System.Collections.Generic;
using UnityEngine;

namespace ReAction.Fx
{
    /// <summary>
    /// 스프라이트 잔상 (트레일·고스팅·색수차 잔상). 연출 전용.
    /// - 실행 중: 최근 움직임을 기억만 해 두고, 계획으로 들어가는 순간 그 궤적을 잔상으로 남긴다.
    /// - 계획 중: 현재 자세에서 잔상이 주기적으로 피어나며 빨강/청록으로 벌어지면서 사라진다.
    /// - 되감기 중: 촘촘한 잔상이 짧게 남는다.
    /// 잔상은 원본보다 바로 아래 그리기 순서를 써서, 안개(위)에 가려진 적의 잔상이 새어 나오지 않는다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class AfterimageTrail : MonoBehaviour
    {
        private const int PixelsPerUnit = 32;
        private const int PoolSize = 16;
        private const float MinMoveForHistory = 0.01f;

        private static readonly Color RedTint = new Color(1f, 0.25f, 0.25f, 1f);
        private static readonly Color CyanTint = new Color(0.25f, 0.85f, 1f, 1f);

        private struct Sample
        {
            public Vector3 Position;
            public Sprite Sprite;
            public bool FlipX;
        }

        private sealed class Ghost
        {
            public Transform Root;
            public SpriteRenderer Main;
            public SpriteRenderer Red;
            public SpriteRenderer Cyan;
            public float Age;
            public float Life;
            public float StartAlpha;
            public bool Active;
        }

        private SpriteRenderer _source;
        private readonly List<Ghost> _pool = new List<Ghost>();
        private readonly Queue<Sample> _history = new Queue<Sample>();
        private Transform _ghostRoot;
        private TimeFxMode _lastMode;
        private float _timer;
        private Vector3 _lastHistoryPosition;

        private void Awake()
        {
            _source = GetComponent<SpriteRenderer>();
            _ghostRoot = new GameObject($"{name}_Afterimages").transform;
            for (int i = 0; i < PoolSize; i++) _pool.Add(CreateGhost(i));
        }

        private void OnDestroy()
        {
            if (_ghostRoot != null) Destroy(_ghostRoot.gameObject);
        }

        private void LateUpdate()
        {
            var settings = TimeDilationFx.Settings;
            if (settings == null) return;

            var mode = TimeDilationFx.Mode;
            float dt = Time.unscaledDeltaTime;
            _timer += dt;

            if (mode != _lastMode)
            {
                if (mode == TimeFxMode.Planning && _lastMode == TimeFxMode.None) EmitHistory(settings);
                if (mode != TimeFxMode.None) _history.Clear();
                _lastMode = mode;
                _timer = 0f;
            }

            switch (mode)
            {
                case TimeFxMode.None:
                    RecordHistory(settings);
                    break;
                case TimeFxMode.Planning:
                    EmitEvery(settings.PlanningGhostInterval, settings.PlanningGhostSeconds, settings.GhostAlpha);
                    break;
                case TimeFxMode.Rewind:
                    EmitEvery(settings.RewindGhostInterval, settings.RewindGhostSeconds, settings.GhostAlpha * 0.8f);
                    break;
            }

            UpdateGhosts(dt, settings.GhostSplitPixels);
        }

        private void RecordHistory(TimeFxSettings settings)
        {
            if (_timer < settings.HistoryInterval) return;
            _timer = 0f;
            if ((transform.position - _lastHistoryPosition).sqrMagnitude < MinMoveForHistory * MinMoveForHistory) return;

            _lastHistoryPosition = transform.position;
            _history.Enqueue(new Sample { Position = transform.position, Sprite = _source.sprite, FlipX = _source.flipX });
            while (_history.Count > settings.HistoryGhosts) _history.Dequeue();
        }

        /// <summary>계획으로 들어가는 순간: 방금까지의 움직임 궤적이 얼어붙은 잔상으로 남는다.</summary>
        private void EmitHistory(TimeFxSettings settings)
        {
            int i = 0;
            int count = _history.Count;
            foreach (var sample in _history)
            {
                float recency = (i + 1f) / count; // 최근일수록 진하다
                Spawn(sample, settings.HistoryGhostSeconds, settings.GhostAlpha * recency);
                i++;
            }
            _history.Clear();
        }

        private void EmitEvery(float interval, float life, float alpha)
        {
            if (_timer < interval) return;
            _timer = 0f;
            Spawn(new Sample { Position = transform.position, Sprite = _source.sprite, FlipX = _source.flipX }, life, alpha);
        }

        private void Spawn(Sample sample, float life, float alpha)
        {
            if (!_source.enabled || !gameObject.activeInHierarchy || sample.Sprite == null) return;

            var ghost = _pool.Find(g => !g.Active) ?? OldestGhost();
            ghost.Active = true;
            ghost.Age = 0f;
            ghost.Life = Mathf.Max(0.01f, life);
            ghost.StartAlpha = alpha;
            ghost.Root.position = sample.Position;
            ghost.Root.gameObject.SetActive(true);
            foreach (var r in new[] { ghost.Main, ghost.Red, ghost.Cyan })
            {
                r.sprite = sample.Sprite;
                r.flipX = sample.FlipX;
                r.sortingLayerID = _source.sortingLayerID;
            }
            ghost.Main.sortingOrder = _source.sortingOrder - 1;
            ghost.Red.sortingOrder = _source.sortingOrder - 2;
            ghost.Cyan.sortingOrder = _source.sortingOrder - 2;
        }

        private void UpdateGhosts(float dt, int splitPixels)
        {
            foreach (var ghost in _pool)
            {
                if (!ghost.Active) continue;
                ghost.Age += dt;
                float t = ghost.Age / ghost.Life;
                if (t >= 1f)
                {
                    ghost.Active = false;
                    ghost.Root.gameObject.SetActive(false);
                    continue;
                }

                float alpha = ghost.StartAlpha * (1f - t);
                // 빨강/청록이 시간이 지날수록 정수 픽셀 단위로 벌어진다 (픽셀 아트가 번지지 않게)
                float split = Mathf.Round(t * splitPixels + 1f) / PixelsPerUnit;
                ghost.Red.transform.localPosition = new Vector3(-split, 0f, 0f);
                ghost.Cyan.transform.localPosition = new Vector3(split, 0f, 0f);
                ghost.Main.color = new Color(1f, 1f, 1f, alpha * 0.6f);
                ghost.Red.color = new Color(RedTint.r, RedTint.g, RedTint.b, alpha);
                ghost.Cyan.color = new Color(CyanTint.r, CyanTint.g, CyanTint.b, alpha);
            }
        }

        private Ghost OldestGhost()
        {
            Ghost oldest = _pool[0];
            foreach (var g in _pool)
            {
                if (g.Age / g.Life > oldest.Age / oldest.Life) oldest = g;
            }
            return oldest;
        }

        private Ghost CreateGhost(int index)
        {
            var root = new GameObject($"Ghost{index}").transform;
            root.SetParent(_ghostRoot, false);
            root.localScale = transform.lossyScale;
            var ghost = new Ghost
            {
                Root = root,
                Main = CreateLayer(root, "Main"),
                Red = CreateLayer(root, "Red"),
                Cyan = CreateLayer(root, "Cyan"),
            };
            root.gameObject.SetActive(false);
            return ghost;
        }

        private SpriteRenderer CreateLayer(Transform parent, string layerName)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = _source.sharedMaterial;
            return renderer;
        }
    }
}
