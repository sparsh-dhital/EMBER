using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the small, mobile-friendly particle effects used around the game.
public static class EmberFX
{
    public static ParticleSystem Create(string name, Transform parent, Vector3 localPos, string material, bool worldSpace, int maxParticles)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.maxParticles = maxParticles;
        main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var shape = ps.shape;
        shape.enabled = false;
        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = EmberArt.Load(material);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    static ParticleSystem.MinMaxGradient Fade(Color c, float peakAlpha = 1f)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.15f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    static void ColorOverLife(ParticleSystem ps, Color c, float peakAlpha = 1f)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Fade(c, peakAlpha);
    }

    static void SizeOverLife(ParticleSystem ps, float start, float end)
    {
        var s = ps.sizeOverLifetime;
        s.enabled = true;
        s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, start, 1f, end));
    }

    public static ParticleSystem Embers(Transform parent, Vector3 pos, Color color, float rate, float size)
    {
        var ps = Create("Embers", parent, pos, "P_Spark", true, 24);
        var main = ps.main;
        main.playOnAwake = true;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.gravityModifier = -0.04f;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.01f;
        sh.rotation = new Vector3(-90f, 0f, 0f);
        ColorOverLife(ps, Color.white);
        SizeOverLife(ps, 1f, 0.2f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.15f; noise.frequency = 1.5f;
        ps.Play();
        return ps;
    }

    public static ParticleSystem Smoke(Transform parent, Vector3 pos, int count)
    {
        var ps = Create("Smoke", parent, pos, "P_Smoke", true, count);
        var main = ps.main;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startColor = new Color(0.55f, 0.55f, 0.58f, 0.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.gravityModifier = -0.05f;
        var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 20f; sh.radius = 0.03f;
        sh.rotation = new Vector3(-90f, 0f, 0f);
        ColorOverLife(ps, Color.white, 0.6f);
        SizeOverLife(ps, 0.6f, 3f);
        return ps;
    }

    public static ParticleSystem Burst(Transform parent, Vector3 pos, Color color, int count, float speed, float size, string name = "Burst")
    {
        var ps = Create(name, parent, pos, "P_Spark", true, count + 1);
        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.gravityModifier = 0.25f;
        var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.05f;
        ColorOverLife(ps, Color.white);
        SizeOverLife(ps, 1f, 0.1f);

        // A quick flash of glow at the centre.
        var flash = Create("Flash", ps.transform, Vector3.zero, "P_Glow", true, 1);
        var fm = flash.main;
        fm.loop = false; fm.duration = 0.3f;
        fm.startLifetime = 0.35f; fm.startSpeed = 0f; fm.startSize = size * 30f;
        fm.startColor = new Color(color.r, color.g, color.b, 0.7f);
        var fe = flash.emission; fe.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        ColorOverLife(flash, Color.white);
        SizeOverLife(flash, 0.6f, 1.3f);
        return ps;
    }

    // A single soft billboard glow that always faces the camera.
    public static ParticleSystem GlowSprite(Transform parent, Vector3 pos, Color color, float size)
    {
        var ps = Create("Glow", parent, pos, "P_Glow", false, 1);
        var main = ps.main;
        main.playOnAwake = true;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = 100000f;
        main.startSpeed = 0f;
        main.startSize = size;
        main.startColor = color;
        var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        ps.Play();
        return ps;
    }

    public static ParticleSystem Motes(Transform parent, Vector3 pos, Color color, float rate, float radius, float size, int max, string name = "Motes")
    {
        var ps = Create(name, parent, pos, "P_Spark", true, max);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.gravityModifier = -0.02f;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius;
        ColorOverLife(ps, Color.white);
        return ps;
    }

    public static ParticleSystem Ring(Transform parent, Vector3 pos, Color color, float endSize, float lifetime, string name = "Ring")
    {
        var ps = Create(name, parent, pos, "P_Ring", true, 3);
        var main = ps.main;
        main.loop = false;
        main.duration = lifetime;
        main.startLifetime = lifetime;
        main.startSpeed = 0f;
        main.startSize = endSize;
        main.startColor = color;
        var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        ColorOverLife(ps, Color.white);
        SizeOverLife(ps, 0.05f, 1f);
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        return ps;
    }
}
