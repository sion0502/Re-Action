using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReAction.Fx
{
    public enum Sfx
    {
        Slash,
        Hit,
        DoorKick,
        DoorOpen,
        Glass,
        Death,
        Rewind,
        Clear,
        Submit,
        Error,
        Climb,
    }

    /// <summary>
    /// 코드로 만드는 레트로풍 효과음 (사운드 에셋이 없을 때의 대용).
    /// 나중에 실제 사운드로 바꾸려면 Sfx마다 AudioClip을 지정하면 된다 (GameFeedback의 Overrides).
    /// 노이즈는 고정 시드라 매번 같은 소리가 난다.
    /// </summary>
    public static class ProceduralSfx
    {
        private const int Rate = 44100;

        public static Dictionary<Sfx, AudioClip> CreateAll()
        {
            return new Dictionary<Sfx, AudioClip>
            {
                { Sfx.Slash, Make("Slash", 0.14f, (t, d, rnd) => Noise(rnd) * Env(t, d, 0.005f) * (0.4f + 0.6f * (1f - t / d))) },
                { Sfx.Hit, Make("Hit", 0.18f, (t, d, rnd) => (Sine(Sweep(t, d, 160f, 50f), t) * 0.9f + Noise(rnd) * 0.3f * Mathf.Exp(-t * 60f)) * Env(t, d, 0.002f)) },
                { Sfx.DoorKick, Make("DoorKick", 0.25f, (t, d, rnd) => (Sine(Sweep(t, d, 110f, 45f), t) + Noise(rnd) * 0.5f * Mathf.Exp(-t * 30f)) * Env(t, d, 0.002f) * 0.9f) },
                { Sfx.DoorOpen, Make("DoorOpen", 0.22f, (t, d, rnd) => Square(Sweep(t, d, 220f, 180f), t) * 0.25f * Env(t, d, 0.01f)) },
                { Sfx.Glass, Make("Glass", 0.45f, (t, d, rnd) => Glass(t, d, rnd)) },
                { Sfx.Death, Make("Death", 0.7f, (t, d, rnd) => Square(Sweep(t, d, 440f, 70f), t) * 0.35f * Env(t, d, 0.01f)) },
                { Sfx.Rewind, Make("Rewind", 0.9f, (t, d, rnd) => Saw(300f + 180f * Mathf.Sin(t * 40f) - 150f * t / d, t) * 0.18f * Env(t, d, 0.03f) + Noise(rnd) * 0.04f) },
                { Sfx.Clear, Make("Clear", 0.5f, (t, d, rnd) => Arpeggio(t)) },
                { Sfx.Submit, Make("Submit", 0.06f, (t, d, rnd) => Square(880f, t) * 0.2f * Env(t, d, 0.002f)) },
                { Sfx.Error, Make("Error", 0.16f, (t, d, rnd) => Square(150f, t) * 0.25f * Env(t, d, 0.005f)) },
                { Sfx.Climb, Make("Climb", 0.08f, (t, d, rnd) => Noise(rnd) * 0.25f * Env(t, d, 0.002f)) },
            };
        }

        private static AudioClip Make(string name, float seconds, Func<float, float, System.Random, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            var random = new System.Random(name.GetHashCode());
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                data[i] = Mathf.Clamp(sample(t, seconds, random), -1f, 1f);
            }
            var clip = AudioClip.Create($"Sfx_{name}", count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 짧게 올라가서 끝까지 줄어드는 감쇠
        private static float Env(float t, float d, float attack) =>
            Mathf.Clamp01(t / attack) * Mathf.Clamp01(1f - t / d);

        private static float Sweep(float t, float d, float from, float to) => Mathf.Lerp(from, to, t / d);
        private static float Sine(float freq, float t) => Mathf.Sin(2f * Mathf.PI * freq * t);
        private static float Square(float freq, float t) => Mathf.Repeat(freq * t, 1f) < 0.5f ? 1f : -1f;
        private static float Saw(float freq, float t) => Mathf.Repeat(freq * t, 1f) * 2f - 1f;
        private static float Noise(System.Random random) => (float)random.NextDouble() * 2f - 1f;

        private static float Glass(float t, float d, System.Random random)
        {
            float pings = Sine(3100f, t) * Mathf.Exp(-t * 18f) + Sine(4300f, t) * Mathf.Exp(-t * 25f)
                          + Sine(2500f, t + 0.03f) * Mathf.Exp(-t * 14f);
            return (pings * 0.3f + Noise(random) * 0.35f * Mathf.Exp(-t * 20f)) * Env(t, d, 0.002f);
        }

        // 도-미-솔-도 (클리어)
        private static float Arpeggio(float t)
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            const float step = 0.1f;
            int index = Mathf.Min(notes.Length - 1, (int)(t / step));
            float local = t - index * step;
            float length = index == notes.Length - 1 ? 0.2f : step;
            return Square(notes[index], t) * 0.18f * Mathf.Clamp01(1f - local / length);
        }
    }
}
