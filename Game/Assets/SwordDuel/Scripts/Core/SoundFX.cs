using UnityEngine;

// Preprosti zvočni učinki, ki jih ustvarimo kar s kodo (sintetizirani valovi),
// zato projekt ne potrebuje nobenih zvočnih datotek.
public static class SoundFX
{
    public enum Sound { Swing, Hit, Click, Beep, Go, Victory, Defeat, Coin, Dash, Block, ShieldBreak }

    const int Rate = 44100;
    static AudioClip[] clips;
    static AudioSource source;

    public static void Play(Sound sound, float volume = 1f, float pitchVariation = 0f)
    {
        if (source == null)
        {
            var go = new GameObject("SoundFX");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.ignoreListenerPause = true; // kliki v meniju pavze se še vedno slišijo
            AudioListener.volume = SaveManager.Volume;
        }
        if (clips == null) clips = BuildClips();

        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.PlayOneShot(clips[(int)sound], volume);
    }

    static AudioClip[] BuildClips()
    {
        var result = new AudioClip[System.Enum.GetValues(typeof(Sound)).Length];
        result[(int)Sound.Swing] = Make("Swing", 0.28f, Whoosh(0.28f, 0.5f));
        result[(int)Sound.Dash] = Make("Dash", 0.22f, Whoosh(0.22f, 0.25f));
        result[(int)Sound.Hit] = Make("Hit", 0.35f, Clang);
        result[(int)Sound.Click] = Make("Click", 0.05f, t => Sine(1800, t) * Decay(t, 60) * 0.4f);
        result[(int)Sound.Beep] = Make("Beep", 0.18f, t => Sine(660, t) * Decay(t, 14) * 0.5f);
        result[(int)Sound.Go] = Make("Go", 0.5f, t => (Sine(990, t) + 0.5f * Sine(1485, t)) * Decay(t, 6) * 0.45f);
        result[(int)Sound.Coin] = Make("Coin", 0.45f, t => Sine(t < 0.08f ? 1320 : 1760, t) * Decay(t < 0.08f ? t : t - 0.08f, 9) * 0.4f);
        result[(int)Sound.Victory] = Make("Victory", 1.3f, t => Notes(t, new[] { 523f, 659f, 784f, 1047f }, 0.14f));
        result[(int)Sound.Defeat] = Make("Defeat", 1.4f, t => Notes(t, new[] { 392f, 311f, 262f }, 0.32f));
        result[(int)Sound.Block] = Make("Block", 0.4f, ShieldThud);
        result[(int)Sound.ShieldBreak] = Make("ShieldBreak", 0.7f, ShieldCrack);
        return result;
    }

    delegate float Wave(float t);

    static AudioClip Make(string name, float length, Wave wave)
    {
        int count = Mathf.CeilToInt(length * Rate);
        var data = new float[count];
        for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(wave(i / (float)Rate), -1f, 1f);
        var clip = AudioClip.Create(name, count, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Sine(float freq, float t) => Mathf.Sin(2f * Mathf.PI * freq * t);
    static float Decay(float t, float speed) => Mathf.Exp(-t * speed);

    // "Šum" meča skozi zrak: filtriran beli šum z mehkim začetkom in koncem
    static Wave Whoosh(float length, float loudness)
    {
        var random = new System.Random(7);
        float low = 0f;
        return t =>
        {
            float noise = (float)random.NextDouble() * 2f - 1f;
            float cutoff = Mathf.Lerp(0.05f, 0.35f, Mathf.Sin(Mathf.PI * t / length));
            low += (noise - low) * cutoff;
            float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / length));
            return low * envelope * envelope * loudness * 2.2f;
        };
    }

    // Kovinski udarec: nekaj neharmoničnih tonov + kratek šum + nizek "tump"
    static float Clang(float t)
    {
        float metal = Sine(820, t) * 0.5f + Sine(1290, t) * 0.35f + Sine(1950, t) * 0.25f + Sine(2710, t) * 0.15f;
        float thud = Sine(110, t) * Decay(t, 25);
        float noise = (Mathf.PerlinNoise(t * 9000f, 0.5f) * 2f - 1f) * Decay(t, 80);
        return (metal * Decay(t, 14) * 0.45f + thud * 0.6f + noise * 0.5f) * 0.8f;
    }

    // Udarec v ščit: globok lesen "tump" in kratek kovinski zven
    static float ShieldThud(float t)
    {
        float wood = (Sine(180, t) * 0.7f + Sine(260, t) * 0.4f) * Decay(t, 18);
        float ring = (Sine(1150, t) * 0.3f + Sine(1720, t) * 0.2f) * Decay(t, 22);
        float noise = (Mathf.PerlinNoise(t * 6000f, 1.5f) * 2f - 1f) * Decay(t, 60);
        return (wood + ring + noise * 0.6f) * 0.8f;
    }

    // Zlom ščita: daljši hrupen "pok" z nizkim padajočim tonom
    static float ShieldCrack(float t)
    {
        float crack = (Mathf.PerlinNoise(t * 9000f, 3.5f) * 2f - 1f) * Decay(t, 9);
        float drop = Sine(Mathf.Lerp(220f, 90f, t / 0.7f), t) * Decay(t, 5);
        return (crack * 0.8f + drop * 0.6f + ShieldThud(t) * 0.5f) * 0.8f;
    }

    static float Notes(float t, float[] freqs, float step)
    {
        int index = Mathf.Min((int)(t / step), freqs.Length - 1);
        float local = t - index * step;
        float decay = index == freqs.Length - 1 ? 3f : 10f;
        float tone = Sine(freqs[index], t) * 0.6f + Sine(freqs[index] * 2f, t) * 0.2f;
        return tone * Decay(local, decay) * Mathf.Clamp01(local * 200f) * 0.45f;
    }
}
