using System;

namespace SlayInspiredPrototype;

public enum DayNightPhase
{
    Day,
    Night
}

public sealed class DayNightCycle
{
    public DayNightPhase Phase { get; private set; } = DayNightPhase.Day;
    public float DayDuration { get; set; } = 45f;
    public float NightDuration { get; set; } = 75f;
    public float PhaseTimer { get; private set; }
    public float TotalTime { get; private set; }
    public float Blend { get; private set; }
    public bool JustBecameDay { get; private set; }
    public bool JustBecameNight { get; private set; }

    public bool IsDay => Phase == DayNightPhase.Day;
    public bool IsNight => Phase == DayNightPhase.Night;
    public float CurrentPhaseDuration => IsNight ? NightDuration : DayDuration;
    public float RemainingTime => MathF.Max(0f, CurrentPhaseDuration - PhaseTimer);
    public float PhaseProgress => CurrentPhaseDuration <= 0f ? 1f : MathF.Min(1f, PhaseTimer / CurrentPhaseDuration);
    public float Darkness => 0.08f + Blend * 0.68f;

    public void Reset(bool startAtDay)
    {
        Phase = startAtDay ? DayNightPhase.Day : DayNightPhase.Night;
        PhaseTimer = 0f;
        TotalTime = 0f;
        Blend = startAtDay ? 0f : 1f;
        JustBecameDay = false;
        JustBecameNight = false;
    }

    public void Tick(float dt)
    {
        JustBecameDay = false;
        JustBecameNight = false;
        PhaseTimer += dt;
        TotalTime += dt;

        float duration = CurrentPhaseDuration;
        if (PhaseTimer >= duration)
        {
            PhaseTimer -= duration;
            if (IsDay)
            {
                Phase = DayNightPhase.Night;
                JustBecameNight = true;
            }
            else
            {
                Phase = DayNightPhase.Day;
                JustBecameDay = true;
            }
        }

        float targetBlend = IsNight ? 1f : 0f;
        Blend += (targetBlend - Blend) * MathF.Min(1f, dt * 3.2f);
    }

    public void ApplyNetworkState(bool isNight, float phaseTimer, float totalTime, float blend)
    {
        Phase = isNight ? DayNightPhase.Night : DayNightPhase.Day;
        PhaseTimer = MathF.Max(0f, phaseTimer);
        TotalTime = MathF.Max(0f, totalTime);
        Blend = Math.Clamp(blend, 0f, 1f);
        JustBecameDay = false;
        JustBecameNight = false;
    }
}