using System.Collections.Generic;
using UnityEngine;

namespace ReAction.Fx
{
    /// <summary>
    /// 스프라이트 잔상 (트레일·고스팅·색수차 잔상). 연출 전용.
    /// 프롬프트를 입력하는 계획 중에 실제로 움직인 만큼만 잔상이 남는다 (가만히 있으면 잔상 없음).
    /// 실행 중의 움직임은 잔상으로 남기지 않고, 계획을 벗어나는 순간(실행·사망·되감기) 잔상을 모두 지운다.
    /// 지나간 자리에 남은 잔상은 빨강/청록으로 벌어지며, 먼저 생긴 것부터 사라진다.
    /// 잔상은 원본보다 바로 아래 그리기 순서를 써서, 안개(위)에 가려진 적의 잔상이 새어 나오지 않는다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class AfterimageTrail : MonoBehaviour
    {
        private const int PixelsPerUnit = 32;
        private const int PoolSize = 24;

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
        private Transform _ghostRoot;
        private TimeFxMode _lastMode;
        private Sample _lastGhostPose;

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

            if (mode != _lastMode)
            {
                if (_lastMode == TimeFxMode.Planning) HideAll();
                // 계획에 들어온 순간의 자세가 기준점이다. 여기서부터 움직인 만큼만 잔상이 남는다.
                if (mode == TimeFxMode.Planning) _lastGhostPose = CurrentPose();
                _lastMode = mode;
            }

            if (mode == TimeFxMode.Planning) EmitOnMove(settings);
            UpdateGhosts(dt, settings.GhostSplitPixels, settings.GhostHoldFraction);
        }

        private Sample CurrentPose() =>
            new Sample { Position = transform.position, Sprite = _source.sprite, FlipX = _source.flipX };

        /// <summary>계획 중 일정 거리(정수 픽셀) 이상 움직이면, 떠나온 자세를 잔상으로 남긴다.</summary>
        private void EmitOnMove(TimeFxSettings settings)
        {
            float minMove = Mathf.Max(1, settings.GhostMinMovePixels) / (float)PixelsPerUnit;
            if ((transform.position - _lastGhostPose.Position).sqrMagnitude < minMove * minMove) return;

            Spawn(_lastGhostPose, settings.PlanningGhostSeconds, settings.GhostAlpha);
            _lastGhostPose = CurrentPose();
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

        private void UpdateGhosts(float dt, int splitPixels, float holdFraction)
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

                // 수명 앞부분은 진하게 유지하고, 그 뒤로 서서히 사라진다
                float fade = t <= holdFraction ? 0f : (t - holdFraction) / (1f - holdFraction);
                float alpha = ghost.StartAlpha * (1f - fade);
                // 빨강/청록이 시간이 지날수록 정수 픽셀 단위로 벌어진다 (픽셀 아트가 번지지 않게)
                float split = Mathf.Round(t * splitPixels + 1f) / PixelsPerUnit;
                ghost.Red.transform.localPosition = new Vector3(-split, 0f, 0f);
                ghost.Cyan.transform.localPosition = new Vector3(split, 0f, 0f);
                ghost.Main.color = new Color(1f, 1f, 1f, alpha * 0.6f);
                ghost.Red.color = new Color(RedTint.r, RedTint.g, RedTint.b, alpha);
                ghost.Cyan.color = new Color(CyanTint.r, CyanTint.g, CyanTint.b, alpha);
            }
        }

        private void HideAll()
        {
            foreach (var ghost in _pool)
            {
                ghost.Active = false;
                ghost.Root.gameObject.SetActive(false);
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
