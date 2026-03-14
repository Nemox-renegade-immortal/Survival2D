using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using ImGuiNET;

namespace SlayInspiredPrototype;

public sealed partial class Game
{
    private enum WorldParticleKind
    {
        Spark,
        Blood,
        Smoke,
        Ring,
        Ember
    }

    private sealed class WorldParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Lifetime;
        public float MaxLifetime;
        public float StartSize;
        public float EndSize;
        public float Drag;
        public float Gravity;
        public float Rotation;
        public float Spin;
        public float Trail;
        public Color StartColor;
        public Color EndColor;
        public WorldParticleKind Kind;
    }

    private readonly List<WorldParticle> _worldParticles = new();

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Saturate(float value) => Math.Clamp(value, 0f, 1f);

    private static float InverseLerp(float a, float b, float value)
    {
        if (MathF.Abs(b - a) <= 0.0001f)
        {
            return 0f;
        }

        return Saturate((value - a) / (b - a));
    }

    private void AddParticle(
        WorldParticleKind kind,
        Vector2 position,
        Vector2 velocity,
        float lifetime,
        float startSize,
        float endSize,
        Color startColor,
        Color endColor,
        float drag = 0f,
        float gravity = 0f,
        float trail = 0f,
        float rotation = 0f,
        float spin = 0f)
    {
        _worldParticles.Add(new WorldParticle
        {
            Kind = kind,
            Position = position,
            Velocity = velocity,
            Lifetime = lifetime,
            MaxLifetime = lifetime,
            StartSize = startSize,
            EndSize = endSize,
            StartColor = startColor,
            EndColor = endColor,
            Drag = drag,
            Gravity = gravity,
            Trail = trail,
            Rotation = rotation,
            Spin = spin
        });
    }

    private void UpdateWorldParticles(float dt)
    {
        for (int i = _worldParticles.Count - 1; i >= 0; i--)
        {
            WorldParticle particle = _worldParticles[i];
            particle.Lifetime -= dt;
            if (particle.Lifetime <= 0f)
            {
                _worldParticles.RemoveAt(i);
                continue;
            }

            if (particle.Gravity != 0f)
            {
                particle.Velocity += new Vector2(0f, particle.Gravity * dt);
            }

            if (particle.Drag > 0f)
            {
                particle.Velocity *= 1f / (1f + particle.Drag * dt);
            }

            particle.Position += particle.Velocity * dt;
            particle.Rotation += particle.Spin * dt;
        }
    }

    private void SpawnRicochetBurst(Vector2 position, Vector2 impactVelocity, bool heavy = false)
    {
        Vector2 dir = Phys.NormalizeSafe(impactVelocity);
        if (dir.LengthSquared() < 0.0001f)
        {
            dir = Phys.FromAngle((float)(_rng.NextDouble() * Math.PI * 2.0));
        }

        float baseAngle = MathF.Atan2(-dir.Y, -dir.X);
        int sparkCount = heavy ? 11 + _rng.Next(6) : 6 + _rng.Next(4);
        AddParticle(WorldParticleKind.Ring, position, Vector2.Zero, heavy ? 0.22f : 0.16f, heavy ? 7f : 5f, heavy ? 30f : 22f, Color.FromArgb(210, 255, 238, 188), Color.FromArgb(0, 255, 186, 82));

        for (int i = 0; i < sparkCount; i++)
        {
            float angle = baseAngle + ((float)_rng.NextDouble() - 0.5f) * (heavy ? 2.2f : 1.6f);
            float speed = heavy ? 90f + (float)_rng.NextDouble() * 250f : 80f + (float)_rng.NextDouble() * 170f;
            Vector2 velocity = Phys.FromAngle(angle) * speed + dir * 24f;
            AddParticle(
                WorldParticleKind.Spark,
                position + velocity * 0.008f,
                velocity,
                0.15f + (float)_rng.NextDouble() * 0.13f,
                2.4f + (float)_rng.NextDouble() * 1.8f,
                0.8f,
                Color.FromArgb(232, 255, 226, 152),
                Color.FromArgb(0, 255, 118, 46),
                drag: 5.4f,
                trail: heavy ? 16f : 12f,
                spin: ((float)_rng.NextDouble() - 0.5f) * 12f);
        }

        int smokeCount = heavy ? 4 : 2;
        for (int i = 0; i < smokeCount; i++)
        {
            Vector2 drift = new Vector2(((float)_rng.NextDouble() - 0.5f) * 40f, -10f - (float)_rng.NextDouble() * 45f);
            AddParticle(
                WorldParticleKind.Smoke,
                position,
                drift,
                0.28f + (float)_rng.NextDouble() * 0.22f,
                heavy ? 5f : 4f,
                heavy ? 18f : 12f,
                Color.FromArgb(90, 216, 208, 198),
                Color.FromArgb(0, 48, 42, 38),
                drag: 2.6f,
                spin: ((float)_rng.NextDouble() - 0.5f) * 2.8f);
        }
    }

    private void SpawnZombieBloodBurst(Vector2 position, Vector2 hitVelocity, Color tint, bool fatal)
    {
        Vector2 dir = Phys.NormalizeSafe(hitVelocity);
        if (dir.LengthSquared() < 0.0001f)
        {
            dir = new Vector2(1f, 0f);
        }

        int count = fatal ? 15 + _rng.Next(8) : 8 + _rng.Next(5);
        AddParticle(WorldParticleKind.Ring, position, Vector2.Zero, fatal ? 0.24f : 0.18f, fatal ? 9f : 6f, fatal ? 34f : 24f, Color.FromArgb(150, 230, 54, 46), Color.FromArgb(0, 78, 10, 10));

        for (int i = 0; i < count; i++)
        {
            float angle = MathF.Atan2(dir.Y, dir.X) + ((float)_rng.NextDouble() - 0.5f) * (fatal ? 2.4f : 1.6f);
            float speed = (fatal ? 90f : 55f) + (float)_rng.NextDouble() * (fatal ? 170f : 110f);
            Vector2 velocity = Phys.FromAngle(angle) * speed;
            velocity.Y -= 12f + (float)_rng.NextDouble() * 26f;
            Color start = Mix(Color.FromArgb(236, tint), Color.FromArgb(255, 214, 28, 28), fatal ? 0.34f : 0.22f);
            Color end = fatal ? Color.FromArgb(0, 56, 8, 8) : Color.FromArgb(0, 92, 14, 14);
            AddParticle(
                WorldParticleKind.Blood,
                position + velocity * 0.01f,
                velocity,
                0.24f + (float)_rng.NextDouble() * (fatal ? 0.28f : 0.18f),
                fatal ? 4.2f : 3.2f,
                1.2f,
                start,
                end,
                drag: fatal ? 3.2f : 4.0f,
                gravity: 240f + (float)_rng.NextDouble() * 110f,
                spin: ((float)_rng.NextDouble() - 0.5f) * 8f);
        }

        if (fatal)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 smoke = new Vector2(((float)_rng.NextDouble() - 0.5f) * 28f, -10f - (float)_rng.NextDouble() * 24f);
                AddParticle(
                    WorldParticleKind.Smoke,
                    position,
                    smoke,
                    0.22f + (float)_rng.NextDouble() * 0.16f,
                    4f,
                    10f,
                    Color.FromArgb(62, 124, 36, 36),
                    Color.FromArgb(0, 28, 10, 10),
                    drag: 2.1f,
                    spin: ((float)_rng.NextDouble() - 0.5f) * 2.2f);
            }
        }
    }

    private void SpawnCrateImpactBurst(Vector2 position, int damage, bool destroyed)
    {
        int count = destroyed ? 12 + _rng.Next(5) : 5 + _rng.Next(4);
        AddParticle(WorldParticleKind.Ring, position, Vector2.Zero, destroyed ? 0.20f : 0.14f, destroyed ? 7f : 4f, destroyed ? 28f : 18f, Color.FromArgb(144, 212, 174, 126), Color.FromArgb(0, 104, 72, 44));
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            float speed = destroyed ? 70f + (float)_rng.NextDouble() * 180f : 40f + (float)_rng.NextDouble() * 90f;
            Vector2 velocity = Phys.FromAngle(angle) * speed;
            AddParticle(
                WorldParticleKind.Ember,
                position,
                velocity,
                destroyed ? 0.28f + (float)_rng.NextDouble() * 0.16f : 0.16f + (float)_rng.NextDouble() * 0.08f,
                destroyed ? 4.4f : 3.2f,
                1.1f,
                Color.FromArgb(220, 188, 144, 104),
                Color.FromArgb(0, 94, 62, 36),
                drag: 4.0f,
                gravity: 210f + (float)_rng.NextDouble() * 80f,
                trail: destroyed ? 8f : 4f,
                spin: ((float)_rng.NextDouble() - 0.5f) * 10f);
        }

        if (destroyed)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 smoke = new Vector2(((float)_rng.NextDouble() - 0.5f) * 35f, -8f - (float)_rng.NextDouble() * 30f);
                AddParticle(WorldParticleKind.Smoke, position, smoke, 0.28f + (float)_rng.NextDouble() * 0.18f, 5f, 13f, Color.FromArgb(74, 142, 118, 88), Color.FromArgb(0, 42, 36, 30), drag: 2.4f);
            }
        }
    }

    private void SpawnExplosionBurst(Vector2 position, float radius, Color tint)
    {
        int sparkCount = Math.Clamp((int)(radius / 10f), 8, 24);
        AddParticle(WorldParticleKind.Ring, position, Vector2.Zero, 0.32f, radius * 0.18f, radius * 0.94f, Color.FromArgb(210, Mix(tint, Color.White, 0.22f)), Color.FromArgb(0, tint));

        for (int i = 0; i < sparkCount; i++)
        {
            float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            float speed = 120f + (float)_rng.NextDouble() * (radius * 2.2f);
            AddParticle(
                WorldParticleKind.Spark,
                position,
                Phys.FromAngle(angle) * speed,
                0.18f + (float)_rng.NextDouble() * 0.18f,
                3.6f,
                1f,
                Color.FromArgb(240, Mix(tint, Color.White, 0.48f)),
                Color.FromArgb(0, Mix(tint, Color.FromArgb(255, 126, 42), 0.35f)),
                drag: 3.6f,
                trail: 18f);
        }

        int smokeCount = Math.Clamp((int)(radius / 18f), 4, 12);
        for (int i = 0; i < smokeCount; i++)
        {
            float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            float speed = 22f + (float)_rng.NextDouble() * (radius * 0.55f);
            AddParticle(
                WorldParticleKind.Smoke,
                position,
                Phys.FromAngle(angle) * speed + new Vector2(0f, -22f),
                0.32f + (float)_rng.NextDouble() * 0.28f,
                radius * 0.08f,
                radius * 0.28f,
                Color.FromArgb(96, 236, 182, 122),
                Color.FromArgb(0, 38, 32, 28),
                drag: 1.8f,
                spin: ((float)_rng.NextDouble() - 0.5f) * 1.8f);
        }
    }

    private void DrawWorldParticles(Graphics g)
    {
        for (int i = 0; i < _worldParticles.Count; i++)
        {
            WorldParticle particle = _worldParticles[i];
            float life = particle.MaxLifetime <= 0.0001f ? 0f : particle.Lifetime / particle.MaxLifetime;
            float t = 1f - life;
            float size = Lerp(particle.StartSize, particle.EndSize, t);
            if (!IsVisible(particle.Position, size + particle.Trail + 18f))
            {
                continue;
            }

            Color color = Mix(particle.StartColor, particle.EndColor, t);
            float alphaScale = life <= 0.2f ? life / 0.2f : 1f;
            int alpha = (int)(color.A * Saturate(alphaScale));
            if (alpha <= 2)
            {
                continue;
            }

            Color drawColor = Color.FromArgb(alpha, color);
            switch (particle.Kind)
            {
                case WorldParticleKind.Spark:
                {
                    Vector2 dir = Phys.NormalizeSafe(particle.Velocity);
                    if (dir.LengthSquared() < 0.0001f)
                    {
                        dir = new Vector2(1f, 0f);
                    }

                    float tailLength = size + particle.Trail * life;
                    Vector2 tail = particle.Position - dir * tailLength;
                    using Pen outer = new Pen(drawColor, Math.Max(1f, size * 0.55f));
                    using Pen inner = new Pen(Color.FromArgb(Math.Min(255, alpha), 255, 250, 238), Math.Max(1f, size * 0.24f));
                    g.DrawLine(outer, tail.X, tail.Y, particle.Position.X, particle.Position.Y);
                    g.DrawLine(inner, tail.X, tail.Y, particle.Position.X, particle.Position.Y);
                    using SolidBrush core = new SolidBrush(Color.FromArgb(Math.Min(255, alpha), 255, 248, 236));
                    g.FillEllipse(core, particle.Position.X - size * 0.45f, particle.Position.Y - size * 0.45f, size * 0.9f, size * 0.9f);
                    break;
                }
                case WorldParticleKind.Blood:
                case WorldParticleKind.Ember:
                {
                    using SolidBrush fill = new SolidBrush(drawColor);
                    g.FillEllipse(fill, particle.Position.X - size * 0.5f, particle.Position.Y - size * 0.5f, size, size);
                    break;
                }
                case WorldParticleKind.Smoke:
                {
                    using SolidBrush fill = new SolidBrush(drawColor);
                    g.FillEllipse(fill, particle.Position.X - size * 0.5f, particle.Position.Y - size * 0.5f, size, size);
                    break;
                }
                case WorldParticleKind.Ring:
                {
                    using Pen ring = new Pen(drawColor, Math.Max(1f, 1.2f + size * 0.05f));
                    g.DrawEllipse(ring, particle.Position.X - size * 0.5f, particle.Position.Y - size * 0.5f, size, size);
                    break;
                }
            }
        }
    }

    private void DrawImGuiWorldParticles(ImDrawListPtr draw)
    {
        if (!_worldParticlesEnabled)
        {
            return;
        }

        for (int i = 0; i < _worldParticles.Count; i++)
        {
            WorldParticle particle = _worldParticles[i];
            float life = particle.MaxLifetime <= 0.0001f ? 0f : particle.Lifetime / particle.MaxLifetime;
            float t = 1f - life;
            float size = Lerp(particle.StartSize, particle.EndSize, t);
            if (!IsVisible(particle.Position, size + particle.Trail + 18f))
            {
                continue;
            }

            Color color = Mix(particle.StartColor, particle.EndColor, t);
            float alphaScale = life <= 0.2f ? life / 0.2f : 1f;
            int alpha = (int)(color.A * Saturate(alphaScale));
            if (alpha <= 2)
            {
                continue;
            }

            uint col = ToU32(Color.FromArgb(alpha, color));
            Vector2 p = WorldToScreen(particle.Position);
            switch (particle.Kind)
            {
                case WorldParticleKind.Spark:
                {
                    Vector2 dir = Phys.NormalizeSafe(particle.Velocity);
                    if (dir.LengthSquared() < 0.0001f)
                    {
                        dir = new Vector2(1f, 0f);
                    }

                    Vector2 tail = p - dir * (size + particle.Trail * life);
                    draw.AddLine(tail, p, col, Math.Max(1f, size * 0.55f));
                    draw.AddLine(tail, p, ToU32(Color.FromArgb(Math.Min(255, alpha), 255, 248, 238)), Math.Max(1f, size * 0.24f));
                    draw.AddCircleFilled(p, Math.Max(1f, size * 0.26f), ToU32(Color.FromArgb(Math.Min(255, alpha), 255, 248, 236)), 12);
                    break;
                }
                case WorldParticleKind.Blood:
                case WorldParticleKind.Ember:
                case WorldParticleKind.Smoke:
                    draw.AddCircleFilled(p, Math.Max(1f, size * 0.5f), col, 14);
                    break;
                case WorldParticleKind.Ring:
                    draw.AddCircle(p, Math.Max(1f, size * 0.5f), col, 24, Math.Max(1f, 1.2f + size * 0.05f));
                    break;
            }
        }
    }

    private void DrawEnhancedScreenShaderDetails(Graphics g, Size clientSize, bool fast)
    {
        float darkness = (_phase == GamePhase.Playing || _phase == GamePhase.Paused || _phase == GamePhase.GameOver) ? _dayNight.Darkness : 0f;
        float sweep = (_backgroundPulse * 92f) % (clientSize.Width + clientSize.Height * 0.5f + 220f);
        int beamCount = fast ? 2 : 3;
        for (int i = 0; i < beamCount; i++)
        {
            float offset = (sweep + i * 220f) % (clientSize.Width + clientSize.Height * 0.5f + 220f) - 110f;
            using Pen beam = new Pen(Color.FromArgb((int)(8f + (1f - darkness) * 12f), 186, 228, 255), fast ? 22f : 30f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(beam, offset, -20f, offset + clientSize.Height * 0.62f, clientSize.Height + 20f);
        }

        int barCount = fast ? 2 : 4;
        for (int i = 0; i < barCount; i++)
        {
            float y = (_backgroundPulse * (18f + i * 3f) + i * 137f) % Math.Max(1, clientSize.Height + 40);
            int alpha = (int)(4f + darkness * 6f + i);
            using SolidBrush bar = new SolidBrush(Color.FromArgb(alpha, 210, 230, 255));
            g.FillRectangle(bar, 0f, y - 0.5f, clientSize.Width, 1.5f);
        }

        int edgeGlowAlpha = (int)(10f + darkness * 20f + 4f * (0.5f + 0.5f * MathF.Sin(_backgroundPulse * 2.4f)));
        using LinearGradientBrush leftGlow = new LinearGradientBrush(new RectangleF(0f, 0f, Math.Max(32f, clientSize.Width * 0.12f), clientSize.Height), Color.FromArgb(edgeGlowAlpha, 80, 138, 196), Color.FromArgb(0, 80, 138, 196), LinearGradientMode.Horizontal);
        using LinearGradientBrush rightGlow = new LinearGradientBrush(new RectangleF(clientSize.Width - Math.Max(32f, clientSize.Width * 0.12f), 0f, Math.Max(32f, clientSize.Width * 0.12f), clientSize.Height), Color.FromArgb(0, 80, 138, 196), Color.FromArgb(edgeGlowAlpha, 80, 138, 196), LinearGradientMode.Horizontal);
        g.FillRectangle(leftGlow, 0f, 0f, Math.Max(32f, clientSize.Width * 0.12f), clientSize.Height);
        g.FillRectangle(rightGlow, clientSize.Width - Math.Max(32f, clientSize.Width * 0.12f), 0f, Math.Max(32f, clientSize.Width * 0.12f), clientSize.Height);
    }

    private void DrawImGuiAtmosphereShaderDetails(ImDrawListPtr draw, Vector2 screen)
    {
        float darkness = (_phase == GamePhase.Playing || _phase == GamePhase.Paused || _phase == GamePhase.GameOver) ? _dayNight.Darkness : 0f;
        float sweep = (_backgroundPulse * 92f) % (screen.X + screen.Y * 0.5f + 220f);
        for (int i = 0; i < 3; i++)
        {
            float offset = (sweep + i * 220f) % (screen.X + screen.Y * 0.5f + 220f) - 110f;
            Vector2 a = new Vector2(offset, -28f);
            Vector2 b = new Vector2(offset + screen.Y * 0.62f, screen.Y + 28f);
            draw.AddLine(a, b, ToU32(Color.FromArgb((int)(8f + (1f - darkness) * 12f), 186, 228, 255)), 22f);
        }

        int barCount = 4;
        for (int i = 0; i < barCount; i++)
        {
            float y = (_backgroundPulse * (18f + i * 3f) + i * 137f) % Math.Max(1f, screen.Y + 40f);
            draw.AddRectFilled(new Vector2(0f, y), new Vector2(screen.X, y + 1.5f), ToU32(Color.FromArgb((int)(4f + darkness * 6f + i), 210, 230, 255)));
        }

        float band = Math.Max(40f, screen.X * 0.12f);
        draw.AddRectFilledMultiColor(Vector2.Zero, new Vector2(band, screen.Y), ToU32(Color.FromArgb((int)(10f + darkness * 20f), 80, 138, 196)), ToU32(Color.FromArgb(0, 80, 138, 196)), ToU32(Color.FromArgb(0, 80, 138, 196)), ToU32(Color.FromArgb((int)(10f + darkness * 20f), 80, 138, 196)));
        draw.AddRectFilledMultiColor(new Vector2(screen.X - band, 0f), new Vector2(screen.X, screen.Y), ToU32(Color.FromArgb(0, 80, 138, 196)), ToU32(Color.FromArgb((int)(10f + darkness * 20f), 80, 138, 196)), ToU32(Color.FromArgb((int)(10f + darkness * 20f), 80, 138, 196)), ToU32(Color.FromArgb(0, 80, 138, 196)));
    }
}
