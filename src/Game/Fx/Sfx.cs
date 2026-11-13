using System;
using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The game's sounds: synth-style placeholders generated at startup, and recorded ones (audio/sfx), played flat or
/// positionally. All of them go through the Sfx bus, heard as through water: a low-pass filter takes the treble off,
/// more the deeper she is (SetDepth). Her light thrown is a pop (PlayThrow: one of the recorded pops at random), and
/// her light striking anything it can harm a squishy pop (PlayStrike). Recorded too, and heard whatever OnlyToggle says:
/// a sealed wall unlocking (PlaySecret), a shell gathered (PlayGather: its chime with one of two gathers at random), and
/// the roar of her beam's fire while it burns a creature (Burning: a loop, eased in and out).
/// </summary>
public partial class Sfx : Node
{
    const int Rate = 44100;
    const string Bus = "Sfx";
    /// <summary>The water's low-pass: its cutoff in the Shallows and at the deepest (Hz).</summary>
    const float ShallowCutoff = 7000f, DeepCutoff = 1500f;
    /// <summary>The recorded pops (audio/sfx/pop1–pop10.wav) and the strike (squishpop.mp3).</summary>
    const int Pops = 10;
    const string StrikePath = "res://audio/sfx/squishpop.mp3";

    const string SecretPath = "res://audio/sfx/secret.wav", GatherPath = "res://audio/sfx/gather.wav", FirePath = "res://audio/sfx/fire.ogg";
    static readonly string[] GatherVariants = { "res://audio/sfx/gather2.ogg", "res://audio/sfx/gather3.ogg" };
    /// <summary>Gathers no closer together than this (s): a handful of shells at once is one gather, not a pile of them.</summary>
    const float GatherGap = 0.07f;
    /// <summary>The beam's fire: its loudness at full (dB), and how fast it swells in and dies away (per second, of its 0..1).</summary>
    const float FireDb = -6f, FireIn = 6f, FireOut = 3f;

    AudioStream? _secret, _gather, _fire;
    readonly List<AudioStream> _gathers = new();
    AudioStreamPlayer? _firePlayer;
    float _fireOn;
    ulong _lastGather;

    AudioEffectLowPassFilter _water = null!;
    readonly List<AudioStream> _pops = new();
    AudioStream? _strike;
    int _lastPop = -1;
    readonly List<AudioStreamPlayer> _recorded = new();
    int _nextRecorded;

    readonly Dictionary<string, AudioStreamWav> _sounds = new();
    readonly List<AudioStreamPlayer> _flat = new();
    readonly List<AudioStreamPlayer3D> _spatial = new();
    readonly Random _random = new(3);
    int _nextFlat, _nextSpatial;

    public override void _Ready()
    {
        // The water's bus: everything heard muffled by it.
        if (AudioServer.GetBusIndex(Bus) == -1)
        {
            AudioServer.AddBus();
            int idx = AudioServer.BusCount - 1;
            AudioServer.SetBusName(idx, Bus);
            AudioServer.SetBusSend(idx, "Master");
            AudioServer.AddBusEffect(idx, new AudioEffectLowPassFilter { CutoffHz = ShallowCutoff, Resonance = 0.6f });
        }
        _water = (AudioEffectLowPassFilter)AudioServer.GetBusEffect(AudioServer.GetBusIndex(Bus), 0);
        for (int i = 1; i <= Pops; i++)
        {
            string path = $"res://audio/sfx/pop{i}.wav";
            if (ResourceLoader.Exists(path)) _pops.Add(GD.Load<AudioStream>(path));
        }
        _strike = ResourceLoader.Exists(StrikePath) ? GD.Load<AudioStream>(StrikePath) : null;
        _secret = ResourceLoader.Exists(SecretPath) ? GD.Load<AudioStream>(SecretPath) : null;
        _gather = ResourceLoader.Exists(GatherPath) ? GD.Load<AudioStream>(GatherPath) : null;
        foreach (string path in GatherVariants)
            if (ResourceLoader.Exists(path)) _gathers.Add(GD.Load<AudioStream>(path));
        _fire = ResourceLoader.Exists(FirePath) ? GD.Load<AudioStream>(FirePath) : null;
        if (_fire is AudioStreamOggVorbis ogg) ogg.Loop = true;
        if (_fire is not null)
        {
            _firePlayer = new AudioStreamPlayer { Bus = Bus, Stream = _fire, VolumeDb = -80f };
            AddChild(_firePlayer);
        }
        for (int i = 0; i < 8; i++)
        {
            var p = new AudioStreamPlayer { Bus = Bus };
            AddChild(p);
            _recorded.Add(p);
        }
        _sounds["shot"] = Make(0.11f, (t, n) => Sweep(t, 0.11f, 620f, 170f) * Env(t, 0.004f, 0.11f) * 0.5f + n * Env(t, 0.002f, 0.05f) * 0.25f);
        _sounds["hit"] = Make(0.16f, (t, n) => Sweep(t, 0.16f, 320f, 80f) * Env(t, 0.002f, 0.16f) * 0.7f + n * Env(t, 0.001f, 0.06f) * 0.5f);
        _sounds["splat"] = Make(0.12f, (t, n) => n * Env(t, 0.002f, 0.12f) * 0.3f);
        _sounds["dash"] = Make(0.38f, (t, n) => n * Env(t, 0.03f, 0.38f) * 0.45f + Sweep(t, 0.38f, 140f, 60f) * Env(t, 0.01f, 0.3f) * 0.3f);
        _sounds["jet"] = Make(0.24f, (t, n) => n * Env(t, 0.02f, 0.24f) * 0.18f + Sweep(t, 0.24f, 110f, 70f) * Env(t, 0.01f, 0.2f) * 0.15f);
        _sounds["enemy_shot"] = Make(0.22f, (t, n) => MathF.Sign(Sweep(t, 0.22f, 950f, 300f)) * Env(t, 0.003f, 0.22f) * 0.22f);
        _sounds["hurt"] = Make(0.3f, (t, n) => Sweep(t, 0.3f, 140f, 45f) * Env(t, 0.002f, 0.3f) * 0.9f + n * Env(t, 0.001f, 0.08f) * 0.4f);
        _sounds["die"] = Make(0.45f, (t, n) => MathF.Sin(MathF.Tau * (300f + 400f * MathF.Floor(t * 14f % 3f)) * t) * Env(t, 0.003f, 0.45f) * 0.4f);
        _sounds["land"] = Make(0.14f, (t, n) => (Sweep(t, 0.14f, 90f, 50f) * 0.6f + n * 0.3f) * Env(t, 0.002f, 0.14f) * 0.5f);
        _sounds["boom"] = Make(0.5f, (t, n) => (Sweep(t, 0.5f, 120f, 30f) * 0.8f + n * 0.7f) * Env(t, 0.002f, 0.5f) * 0.8f);
        _sounds["zap"] = Make(0.14f, (t, n) => MathF.Sign(MathF.Sin(MathF.Tau * (900f + 600f * n) * t)) * Env(t, 0.001f, 0.14f) * 0.2f);
        _sounds["bounce"] = Make(0.08f, (t, n) => Sweep(t, 0.08f, 500f, 900f) * Env(t, 0.002f, 0.08f) * 0.3f);
        _sounds["freeze"] = Make(0.3f, (t, n) => MathF.Sin(MathF.Tau * 2200f * t) * MathF.Sin(MathF.Tau * 31f * t) * Env(t, 0.003f, 0.3f) * 0.25f);
        _sounds["beam"] = Make(0.42f, (t, n) => (MathF.Sin(MathF.Tau * 330f * t) + 0.5f * MathF.Sin(MathF.Tau * 661f * t)) * Env(t, 0.01f, 0.42f) * 0.3f);
        _sounds["item"] = Make(0.6f, (t, n) => MathF.Sin(MathF.Tau * (523f * (t < 0.15f ? 1f : t < 0.3f ? 1.26f : 1.5f)) * t) * Env(t, 0.005f, 0.6f) * 0.35f);
        _sounds["synergy"] = Make(0.9f, (t, n) => (MathF.Sin(MathF.Tau * 523f * t) + MathF.Sin(MathF.Tau * 659f * t) + MathF.Sin(MathF.Tau * 784f * t)) * Env(t, 0.01f, 0.9f) * 0.2f);
        _sounds["active"] = Make(0.35f, (t, n) => (Sweep(t, 0.35f, 200f, 600f) * 0.5f + n * 0.3f) * Env(t, 0.005f, 0.35f) * 0.5f);
        // A soft "bloop" as a bubble leaves the tentacle, and a bright pop when one bursts.
        _sounds["throw"] = Make(0.13f, (t, n) => Sweep(t, 0.13f, 260f, 620f) * Env(t, 0.006f, 0.13f) * 0.5f + n * Env(t, 0.002f, 0.03f) * 0.08f);
        _sounds["pop"] = Make(0.07f, (t, n) => (Sweep(t, 0.07f, 1500f, 700f) * 0.5f + n * 0.5f) * Env(t, 0.001f, 0.07f) * 0.45f);
        // A tiny starfish whizzing off, and a ninja popping out of its anemone.
        _sounds["star"] = Make(0.18f, (t, n) => (Sweep(t, 0.18f, 2400f, 900f) * 0.35f + n * 0.3f) * Env(t, 0.003f, 0.18f) * 0.5f);
        _sounds["emerge"] = Make(0.22f, (t, n) => (Sweep(t, 0.22f, 220f, 520f) * 0.5f + n * 0.25f) * Env(t, 0.01f, 0.22f) * 0.55f);
        _sounds["coin"] = Make(0.16f, (t, n) => (MathF.Sin(MathF.Tau * 1320f * t) + 0.6f * MathF.Sin(MathF.Tau * (t < 0.05f ? 990f : 1760f) * t)) * Env(t, 0.002f, 0.16f) * 0.22f);
        _sounds["chime"] = Make(0.5f, (t, n) => (MathF.Sin(MathF.Tau * 880f * t) + MathF.Sin(MathF.Tau * 1318f * t) * 0.6f) * Env(t, 0.01f, 0.5f) * 0.18f);
        // Creature calls: a barracuda's hiss, a pufferling's squeak, clicks for the hard-shelled, a gurgle for the soft.
        _sounds["hiss"] = Make(0.5f, (t, n) => n * Env(t, 0.05f, 0.5f) * 0.55f + Sweep(t, 0.5f, 1800f, 3200f) * Env(t, 0.05f, 0.5f) * 0.12f);
        _sounds["squeak"] = Make(0.2f, (t, n) => Sweep(t, 0.2f, 900f, 2200f) * Env(t, 0.004f, 0.2f) * 0.4f);
        _sounds["click"] = Make(0.2f, (t, n) => MathF.Sign(MathF.Sin(MathF.Tau * 260f * t)) * Env(t, 0.001f, 0.03f) * 0.3f + MathF.Sign(MathF.Sin(MathF.Tau * 330f * (t - 0.09f))) * (t > 0.09f ? Env(t - 0.09f, 0.001f, 0.03f) : 0f) * 0.3f);
        _sounds["gurgle"] = Make(0.35f, (t, n) => MathF.Sin(MathF.Tau * (180f + 60f * MathF.Sin(t * 40f)) * t) * Env(t, 0.01f, 0.35f) * 0.4f + n * Env(t, 0.005f, 0.1f) * 0.1f);
        // Light bubbles: a soft glassy tone as two merge (played higher the more it holds), and a struck bell when a big
        // one frees a creature.
        _sounds["merge"] = Make(0.42f, (t, n) => (MathF.Sin(MathF.Tau * 660f * t) + 0.45f * MathF.Sin(MathF.Tau * 1320f * t) + 0.2f * MathF.Sin(MathF.Tau * 1980f * t)) * Env(t, 0.012f, 0.42f) * 0.16f);
        _sounds["lamp_switch"] = LampSwitch();
        _sounds["bell_ring"] = Make(1.6f, (t, n) => (MathF.Sin(MathF.Tau * 523f * t) * 0.5f + MathF.Sin(MathF.Tau * 1046f * t) * 0.3f * MathF.Exp(-t * 2f)
            + MathF.Sin(MathF.Tau * 1255f * t) * 0.22f * MathF.Exp(-t * 3f) + MathF.Sin(MathF.Tau * 1568f * t) * 0.14f * MathF.Exp(-t * 4.5f)) * MathF.Min(1f, t / 0.003f) * MathF.Exp(-t * 2.2f) * 0.42f);
        // The achievement banner: a rising four-note chime (C, E, G, C) and a soft whoosh as it leaves in smoke.
        _sounds["ach_chime"] = Make(1.1f, (t, n) =>
        {
            float[] notes = { 523f, 659f, 784f, 1047f };
            float v = 0f;
            for (int i = 0; i < 4; i++)
            {
                float s0 = i * 0.11f;
                if (t >= s0) v += (MathF.Sin(MathF.Tau * notes[i] * (t - s0)) + 0.3f * MathF.Sin(MathF.Tau * notes[i] * 2f * (t - s0))) * Env(t - s0, 0.004f, 1.1f - s0);
            }
            return v * 0.13f;
        });
        _sounds["whoosh"] = Make(0.5f, (t, n) => n * Env(t, 0.12f, 0.5f) * 0.3f + Sweep(t, 0.5f, 300f, 900f) * Env(t, 0.1f, 0.45f) * 0.05f);
        // The curse den: a low beating drone as the dark closes round her; at an empty end, a laugh with no one behind it
        // — a hollow "ha-ha-ha" falling in pitch, echoing back down the tunnel; the fountain's gulp and clink; and the
        // rushing swell of being sent on.
        _sounds["curse_enter"] = Make(1.8f, (t, n) => (MathF.Sin(MathF.Tau * 55f * t) + MathF.Sin(MathF.Tau * 58.5f * t) + 0.4f * MathF.Sin(MathF.Tau * 110f * t)) * MathF.Min(1f, t / 0.5f) * MathF.Max(0f, 1f - t / 1.8f) * 0.22f + n * Env(t, 0.6f, 1.8f) * 0.12f);
        float[] delays = { 0f, 0.42f, 0.88f, 1.4f };
        float[] gains = { 1f, 0.5f, 0.28f, 0.15f };
        var voices = new float[delays.Length][,];
        for (int k = 0; k < delays.Length; k++) voices[k] = LaughVoice(1f - 0.05f * k);
        _sounds["curse_laugh"] = Make(3.2f, (t, n) =>
        {
            float v = 0f;
            for (int k = 0; k < delays.Length; k++) v += Laugh(t - delays[k], 1f - 0.05f * k, n, voices[k]) * gains[k];
            return v * (0.8f + 0.2f * MathF.Sin(MathF.Tau * 3.1f * t)) * 0.5f;
        });
        _sounds["spout"] = Make(0.32f, (t, n) => Sweep(t, 0.2f, 170f, 520f) * Env(t, 0.008f, 0.2f) * 0.45f + n * Env(t, 0.002f, 0.06f) * 0.15f
            + (t > 0.1f ? MathF.Sin(MathF.Tau * 1760f * (t - 0.1f)) * Env(t - 0.1f, 0.002f, 0.22f) * 0.18f : 0f));
        // The sand over the Crack draining away: a long low rumble with a hiss of pouring grains.
        _sounds["sand_sink"] = Make(3.2f, (t, n) => (MathF.Sin(MathF.Tau * (48f - 6f * t) * t) * 0.5f + n * 0.6f) * MathF.Min(1f, t / 0.25f) * MathF.Max(0f, 1f - t / 3.2f) * 0.55f
            + n * MathF.Abs(MathF.Sin(t * 31f)) * Env(t, 0.4f, 3.2f) * 0.2f);
        // Pistol Shrimp Claw: a bubble collapsing, a sharp crack over a short low thump.
        _sounds["snap"] = Make(0.32f, (t, n) => n * MathF.Min(1f, t / 0.0008f) * MathF.Exp(-t * 90f) * 0.9f
            + MathF.Sin(MathF.Tau * (95f - 120f * t) * t) * MathF.Exp(-t * 14f) * 0.55f);
        _sounds["nope"] = Make(0.12f, (t, n) => MathF.Sign(MathF.Sin(MathF.Tau * 140f * t)) * Env(t, 0.002f, 0.12f) * 0.15f);

        for (int i = 0; i < 10; i++)
        {
            var p = new AudioStreamPlayer { Bus = Bus };
            AddChild(p);
            _flat.Add(p);
        }
        for (int i = 0; i < 14; i++)
        {
            var p = new AudioStreamPlayer3D { Bus = Bus, MaxDistance = 45f, UnitSize = 6f, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance };
            AddChild(p);
            _spatial.Add(p);
        }
    }

    /// <summary>How deep she is (as shown, 0 the Shallows … 5 the Abyss): the deeper, the more the water muffles what she hears.</summary>
    public void SetDepth(int depth)
    {
        if (_water is null) return;
        float k = Mathf.Clamp(depth / 5f, 0f, 1f);
        // Falling off geometrically, as the ear hears it.
        _water.CutoffHz = ShallowCutoff * Mathf.Pow(DeepCutoff / ShallowCutoff, k);
    }

    /// <summary>
    /// Her light thrown: one of the recorded pops, picked at random (never the same twice running), at a pitch a little
    /// apart each time. Louder for a stronger volley (volumeDb).
    /// </summary>
    public void PlayThrow(float volumeDb = 0f)
    {
        if (OnlyToggle) return;
        if (_pops.Count == 0) return;
        int pick = _random.Next(_pops.Count);
        if (pick == _lastPop && _pops.Count > 1) pick = (pick + 1 + _random.Next(_pops.Count - 1)) % _pops.Count;
        _lastPop = pick;
        PlayRecorded(_pops[pick], volumeDb, 0.92f + (float)_random.NextDouble() * 0.18f);
    }

    /// <summary>Her light striking something it can harm: a squishy pop, a touch apart in pitch each time.</summary>
    public void PlayStrike(float volumeDb = 0f)
    {
        if (OnlyToggle) return;
        if (_strike is not null) PlayRecorded(_strike, volumeDb, 0.9f + (float)_random.NextDouble() * 0.2f);
    }

    /// <summary>A sealed wall unlocks (its seal opened).</summary>
    public void PlaySecret()
    {
        if (_secret is not null) PlayRecorded(_secret, -2f, 1f);
    }

    /// <summary>A shell gathered: its chime and one of the two gathers at random, a touch apart in pitch each time.</summary>
    public void PlayGather()
    {
        ulong now = Time.GetTicksMsec();
        if (now - _lastGather < (ulong)(GatherGap * 1000f)) return;
        _lastGather = now;
        float pitch = 0.95f + (float)_random.NextDouble() * 0.1f;
        if (_gather is not null) PlayRecorded(_gather, -6f, pitch);
        if (_gathers.Count > 0) PlayRecorded(_gathers[_random.Next(_gathers.Count)], -4f, pitch);
    }

    /// <summary>Her beam's fire: roaring while it burns a creature (on), swelling in and dying away; paused, it stops.</summary>
    public void Burning(bool on, float dt)
    {
        if (_firePlayer is null) return;
        _fireOn = Mathf.MoveToward(_fireOn, on ? 1f : 0f, dt * (on ? FireIn : FireOut));
        if (_fireOn <= 0.001f)
        {
            if (_firePlayer.Playing) _firePlayer.Stop();
            return;
        }
        if (!_firePlayer.Playing) _firePlayer.Play();
        _firePlayer.VolumeDb = FireDb + Mathf.LinearToDb(_fireOn);
    }

    void PlayRecorded(AudioStream stream, float volumeDb, float pitch)
    {
        var p = _recorded[_nextRecorded];
        _nextRecorded = (_nextRecorded + 1) % _recorded.Count;
        p.Stream = stream;
        p.VolumeDb = volumeDb;
        p.PitchScale = pitch;
        p.Play();
    }

    /// <summary>
    /// The game's sounds are off for now but her glow's pull-chain switch (the only one that plays while this holds).
    /// </summary>
    public static bool OnlyToggle = true;

    static bool Muted(string name) => OnlyToggle && name != "lamp_switch";

    public void Play(string name, float volumeDb = 0f, float pitchJitter = 0.06f)
    {
        if (Muted(name)) return;
        var p = _flat[_nextFlat];
        _nextFlat = (_nextFlat + 1) % _flat.Count;
        p.Stream = _sounds[name];
        p.VolumeDb = volumeDb;
        p.PitchScale = 1f + ((float)_random.NextDouble() * 2f - 1f) * pitchJitter;
        p.Play();
    }

    /// <summary>A flat sound at a set pitch (1 is as made), no jitter.</summary>
    public void PlayPitched(string name, float pitch, float volumeDb = 0f)
    {
        if (Muted(name)) return;
        var p = _flat[_nextFlat];
        _nextFlat = (_nextFlat + 1) % _flat.Count;
        p.Stream = _sounds[name];
        p.VolumeDb = volumeDb;
        p.PitchScale = Mathf.Clamp(pitch, 0.25f, 4f);
        p.Play();
    }

    public void PlayAt(string name, Vector3 position, float volumeDb = 0f, float pitchJitter = 0.06f)
    {
        if (Muted(name)) return;
        var p = _spatial[_nextSpatial];
        _nextSpatial = (_nextSpatial + 1) % _spatial.Count;
        p.Stream = _sounds[name];
        p.VolumeDb = volumeDb;
        p.PitchScale = 1f + ((float)_random.NextDouble() * 2f - 1f) * pitchJitter;
        p.GlobalPosition = position;
        p.Play();
    }

    /// <summary>The telegraph tone rises over exactly the telegraph time, so the ear can time the dodge.</summary>
    public void PlayTelegraph(Vector3 position, float duration)
    {
        string key = $"telegraph_{Mathf.RoundToInt(duration * 100f)}";
        if (!_sounds.ContainsKey(key))
        {
            float d = Mathf.Max(duration, 0.05f);
            _sounds[key] = Make(d, (t, n) =>
                MathF.Sin(MathF.Tau * (420f * t + 0.5f * (1300f - 420f) / d * t * t)) * (0.6f + 0.4f * MathF.Sin(t * 70f)) * MathF.Min(1f, t / 0.03f) * 0.45f);
        }
        PlayAt(key, position, 2f, 0f);
    }

    AudioStreamWav Make(float seconds, Func<float, float, float> sample)
    {
        int count = (int)(seconds * Rate);
        var data = new byte[count * 2];
        float lowpassed = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            lowpassed += ((float)_random.NextDouble() * 2f - 1f - lowpassed) * 0.25f;
            float v = Math.Clamp(sample(t, lowpassed), -1f, 1f);
            short s = (short)(v * 32000f);
            data[i * 2] = (byte)(s & 0xff);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
    }

    /// <summary>
    /// A hollow laugh: five "ha"s falling in pitch, each a buzzing voice shaped by an "ah" vowel's formants, with a
    /// breath of noise. Zero outside its 1.3 s.
    /// </summary>
    static readonly float[] LaughStarts = { 0f, 0.21f, 0.41f, 0.6f, 0.78f };
    const int LaughHarmonics = 16;

    /// <summary>Each "ha"'s harmonics weighted by the "ah" vowel's formants (about 760, 1220 and 2600 Hz).</summary>
    static float[,] LaughVoice(float pitch)
    {
        var w = new float[LaughStarts.Length, LaughHarmonics];
        for (int i = 0; i < LaughStarts.Length; i++)
        for (int h = 1; h <= LaughHarmonics; h++)
        {
            float f = h * (205f - 13f * i) * pitch;
            w[i, h - 1] = MathF.Exp(-MathF.Pow((f - 760f) / 230f, 2f)) + 0.6f * MathF.Exp(-MathF.Pow((f - 1220f) / 300f, 2f)) + 0.25f * MathF.Exp(-MathF.Pow((f - 2600f) / 500f, 2f));
        }
        return w;
    }

    static float Laugh(float t, float pitch, float noise, float[,] voices)
    {
        if (t < 0f || t > 1.3f) return 0f;
        float v = 0f;
        for (int i = 0; i < LaughStarts.Length; i++)
        {
            float s = t - LaughStarts[i];
            if (s < 0f || s > 0.45f) continue;
            float f0 = (205f - 13f * i) * pitch * (1f + 0.015f * MathF.Sin(s * 38f));
            float env = MathF.Min(1f, s / 0.012f) * MathF.Exp(-s * (i == LaughStarts.Length - 1 ? 5f : 10f));
            float voice = 0f;
            for (int h = 1; h <= LaughHarmonics; h++) voice += MathF.Sin(MathF.Tau * h * f0 * s) * voices[i, h - 1];
            v += (voice * 0.5f + noise * 0.35f) * env;
        }
        return v;
    }

    /// <summary>
    /// A lamp's pull-chain switch, heard under water: the chain's few beads rattling as it is drawn, the switch's crisp
    /// click with the knock of its body, the chain falling back; then all of it through water: the highs gone (two
    /// low-pass poles near 950 Hz), the lowest rumble cut (a high-pass at 140 Hz), and two soft echoes off the reef.
    /// </summary>
    AudioStreamWav LampSwitch()
    {
        const float Seconds = 0.5f;
        int count = (int)(Seconds * Rate);
        var raw = new float[count];
        var rng = new Random(11);
        float Noise() => (float)rng.NextDouble() * 2f - 1f;
        void Tick(float at, float hz, float amp, float decay)
        {
            int start = (int)(at * Rate);
            for (int i = start; i < count && i < start + (int)(0.05f * Rate); i++)
            {
                float t = (i - start) / (float)Rate;
                raw[i] += amp * (Noise() * MathF.Exp(-t / 0.0035f) * 0.7f + MathF.Sin(MathF.Tau * hz * t) * MathF.Exp(-t / decay));
            }
        }
        // The chain drawn: beads ticking.
        Tick(0.000f, 4300f, 0.22f, 0.010f);
        Tick(0.032f, 5100f, 0.18f, 0.009f);
        Tick(0.058f, 3900f, 0.20f, 0.011f);
        Tick(0.081f, 4700f, 0.16f, 0.008f);
        // The switch: a crisp click and its body's knock.
        int click = (int)(0.115f * Rate);
        for (int i = click; i < count; i++)
        {
            float t = (i - click) / (float)Rate;
            raw[i] += Noise() * MathF.Exp(-t / 0.0025f) * 0.95f
                + MathF.Sin(MathF.Tau * 2300f * t) * MathF.Exp(-t / 0.018f) * 0.5f
                + MathF.Sin(MathF.Tau * 690f * t) * MathF.Exp(-t / 0.035f) * 0.45f;
        }
        // The chain falls back.
        Tick(0.190f, 4500f, 0.12f, 0.008f);
        Tick(0.218f, 5300f, 0.09f, 0.007f);
        Tick(0.247f, 4100f, 0.07f, 0.007f);
        // Under water: low-pass twice, high-pass once, two echoes.
        float lp = 1f - MathF.Exp(-MathF.Tau * 950f / Rate);
        float hp = MathF.Exp(-MathF.Tau * 140f / Rate);
        float a = 0f, b = 0f, prevIn = 0f, prevOut = 0f;
        var wet = new float[count];
        for (int i = 0; i < count; i++)
        {
            a += (raw[i] - a) * lp;
            b += (a - b) * lp;
            float o = hp * (prevOut + b - prevIn);
            prevIn = b;
            prevOut = o;
            wet[i] = o;
        }
        int e1 = (int)(0.047f * Rate), e2 = (int)(0.093f * Rate);
        float peak = 1e-4f;
        var mixed = new float[count];
        for (int i = 0; i < count; i++)
        {
            mixed[i] = wet[i] + (i >= e1 ? wet[i - e1] * 0.32f : 0f) + (i >= e2 ? wet[i - e2] * 0.16f : 0f);
            peak = MathF.Max(peak, MathF.Abs(mixed[i]));
        }
        var data = new byte[count * 2];
        for (int i = 0; i < count; i++)
        {
            short v = (short)(Math.Clamp(mixed[i] / peak * 0.85f, -1f, 1f) * 32000f);
            data[i * 2] = (byte)(v & 0xff);
            data[i * 2 + 1] = (byte)((v >> 8) & 0xff);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
    }

    static float Sweep(float t, float duration, float from, float to)
    {
        float k = (to - from) / duration;
        return MathF.Sin(MathF.Tau * (from * t + 0.5f * k * t * t));
    }

    static float Env(float t, float attack, float duration) =>
        MathF.Min(1f, t / attack) * MathF.Max(0f, 1f - t / duration) * MathF.Max(0f, 1f - t / duration);
}
