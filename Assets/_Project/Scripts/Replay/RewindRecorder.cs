using System.Collections.Generic;
using UnityEngine;

namespace ReAction.Replay
{
    /// <summary>되감기 대상 하나: 위치와 보이는 스프라이트를 기록한다.</summary>
    public sealed class RewindTrack
    {
        public RewindTrack(Transform root, SpriteRenderer renderer, Animator animator)
        {
            Root = root;
            Renderer = renderer;
            Animator = animator;
        }

        public Transform Root { get; }
        public SpriteRenderer Renderer { get; }
        public Animator Animator { get; }
    }

    /// <summary>
    /// 한 시도 동안 틱마다 보이는 상태(위치·스프라이트·좌우)를 기록한다. 사망 시 되감기 연출에만 쓰이고,
    /// 게임 판정에는 영향을 주지 않는다.
    /// </summary>
    public sealed class RewindRecorder
    {
        public struct Sample
        {
            public Vector3 Position;
            public Sprite Sprite;
            public bool FlipX;
        }

        private readonly List<RewindTrack> _tracks = new List<RewindTrack>();
        private readonly List<Sample[]> _frames = new List<Sample[]>();

        public IReadOnlyList<RewindTrack> Tracks => _tracks;
        public int FrameCount => _frames.Count;

        public void AddTrack(RewindTrack track) => _tracks.Add(track);

        public void Record()
        {
            var frame = new Sample[_tracks.Count];
            for (int i = 0; i < _tracks.Count; i++)
            {
                var t = _tracks[i];
                frame[i] = new Sample
                {
                    Position = t.Root.position,
                    Sprite = t.Renderer.sprite,
                    FlipX = t.Renderer.flipX,
                };
            }
            _frames.Add(frame);
        }

        public void Apply(int frameIndex)
        {
            var frame = _frames[Mathf.Clamp(frameIndex, 0, _frames.Count - 1)];
            for (int i = 0; i < _tracks.Count; i++)
            {
                var t = _tracks[i];
                t.Root.position = frame[i].Position;
                t.Renderer.sprite = frame[i].Sprite;
                t.Renderer.flipX = frame[i].FlipX;
            }
        }

        public void Clear() => _frames.Clear();
    }
}
