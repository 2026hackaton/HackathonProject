using System.Collections.Generic;
using UnityEngine;

namespace Hackathon.WebPort
{
    public sealed class WebPortEffectRenderer
    {
        private const int MaxEffects = 8;
        private const int MaxSweeps = 4;
        private const int MaxShockwaves = 3;
        private const int MaxBursts = 4;
        private const int ParticlesPerBurst = 12;
        private const float DeliverParticleGravity = 260f;

        private readonly Transform _root;
        private readonly MeshRenderer[] _rings = new MeshRenderer[MaxEffects];
        private readonly Transform[] _sweeps = new Transform[MaxSweeps];
        private readonly MeshRenderer[] _shockwaves = new MeshRenderer[MaxShockwaves];
        private readonly MeshFilter[] _shockwaveFilters = new MeshFilter[MaxShockwaves];
        private readonly EffectEvent[] _boundShockwaves = new EffectEvent[MaxShockwaves];
        private readonly MeshRenderer[] _burstRings = new MeshRenderer[MaxBursts];
        private readonly MeshRenderer[] _particles = new MeshRenderer[MaxBursts * ParticlesPerBurst];
        private readonly ParticleSeed[][] _seeds = new ParticleSeed[MaxBursts][];
        private readonly EffectEvent[] _boundBursts = new EffectEvent[MaxBursts];

        public WebPortEffectRenderer(Transform parent, bool useParentAsRoot = false)
        {
            _root = useParentAsRoot ? parent : new GameObject("Effects").transform;
            if (!useParentAsRoot)
                _root.SetParent(parent, false);
            CreateRings();
            CreateSweeps();
            CreateShockwaves();
            CreateDeliverBursts();
        }

        public void Update(IReadOnlyList<EffectEvent> effects, float now)
        {
            UpdateRings(effects, now);
            UpdateSweeps(effects, now);
            UpdateShockwaves(effects, now);
            UpdateDeliverBursts(effects, now);
        }

        private void CreateRings()
        {
            Mesh ringMesh = WebPortVisuals.CreateRingMesh(0.7f, 1f, 24);
            for (int i = 0; i < _rings.Length; i++)
            {
                GameObject obj = new($"Ring Effect {i}");
                obj.transform.SetParent(_root, false);
                obj.AddComponent<MeshFilter>().sharedMesh = ringMesh;
                MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = WebPortVisuals.CreateUnlit(Color.white.WithAlphaCompat(0f), true);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                obj.SetActive(false);
                _rings[i] = renderer;
            }
        }

        private void CreateSweeps()
        {
            for (int i = 0; i < _sweeps.Length; i++)
            {
                GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = $"Sweep {i}";
                obj.transform.SetParent(_root, false);
                Object.Destroy(obj.GetComponent<Collider>());
                MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = WebPortVisuals.CreateUnlit(Color.white.WithAlphaCompat(0f), true);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                obj.SetActive(false);
                _sweeps[i] = obj.transform;
            }
        }

        private void CreateShockwaves()
        {
            for (int i = 0; i < _shockwaves.Length; i++)
            {
                GameObject obj = new($"Shockwave {i}");
                obj.transform.SetParent(_root, false);
                _shockwaveFilters[i] = obj.AddComponent<MeshFilter>();
                _shockwaveFilters[i].sharedMesh = WebPortVisuals.CreateRingMesh(0.9f, 1f, 28);
                MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = WebPortVisuals.CreateUnlit(WebPortVisuals.Orange.WithAlphaCompat(0f), true);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                obj.SetActive(false);
                _shockwaves[i] = renderer;
            }
        }

        private void CreateDeliverBursts()
        {
            Mesh ringMesh = WebPortVisuals.CreateRingMesh(0.8f, 1f, 32);
            for (int burst = 0; burst < MaxBursts; burst++)
            {
                GameObject ring = new($"Deliver Ring {burst}");
                ring.transform.SetParent(_root, false);
                ring.AddComponent<MeshFilter>().sharedMesh = ringMesh;
                MeshRenderer ringRenderer = ring.AddComponent<MeshRenderer>();
                ringRenderer.sharedMaterial = WebPortVisuals.CreateUnlit(WebPortVisuals.Yellow.WithAlphaCompat(0f), true);
                ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ringRenderer.receiveShadows = false;
                ring.SetActive(false);
                _burstRings[burst] = ringRenderer;

                for (int i = 0; i < ParticlesPerBurst; i++)
                {
                    GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    particle.name = $"Deliver Particle {burst}-{i}";
                    particle.transform.SetParent(_root, false);
                    particle.transform.localScale = Vector3.one * 6f;
                    Object.Destroy(particle.GetComponent<Collider>());
                    MeshRenderer renderer = particle.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = WebPortVisuals.CreateUnlit(WebPortVisuals.Yellow.WithAlphaCompat(0f), true);
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    particle.SetActive(false);
                    _particles[burst * ParticlesPerBurst + i] = renderer;
                }
            }
        }

        private void UpdateRings(IReadOnlyList<EffectEvent> effects, float now)
        {
            int slot = 0;
            for (int i = 0; i < effects.Count && slot < _rings.Length; i++)
            {
                EffectEvent e = effects[i];
                if (e.Kind is EffectKind.Sweep or EffectKind.Shockwave or EffectKind.Deliver)
                    continue;

                MeshRenderer renderer = _rings[slot++];
                float p = Mathf.Clamp01((now - e.StartedAt) / e.Duration);
                float alpha = Mathf.Max(1f - p, 0f);
                float scale = e.Kind == EffectKind.Explosion ? 20f + p * 130f : 10f + p * 30f;

                renderer.gameObject.SetActive(true);
                renderer.transform.position = e.Position + Vector3.up * 20f;
                renderer.transform.localScale = Vector3.one * Mathf.Max(scale, 1f);
                WebPortVisuals.SetMaterialColor(renderer.sharedMaterial, ColorFor(e.Kind).WithAlphaCompat(alpha));
            }

            for (; slot < _rings.Length; slot++)
                _rings[slot].gameObject.SetActive(false);
        }

        private void UpdateSweeps(IReadOnlyList<EffectEvent> effects, float now)
        {
            int slot = 0;
            for (int i = 0; i < effects.Count && slot < _sweeps.Length; i++)
            {
                EffectEvent e = effects[i];
                if (e.Kind != EffectKind.Sweep)
                    continue;

                Transform sweep = _sweeps[slot++];
                float p = (now - e.StartedAt) / e.Duration;
                if (p >= 1f)
                {
                    sweep.gameObject.SetActive(false);
                    continue;
                }

                float length = Mathf.Max(e.Length, 1f);
                float distance = length * Mathf.Min(p * 2.4f, 1f);
                sweep.gameObject.SetActive(true);
                sweep.position = e.Position + new Vector3(Mathf.Cos(e.Angle) * distance * 0.5f, 22f, Mathf.Sin(e.Angle) * distance * 0.5f);
                sweep.rotation = Quaternion.Euler(0f, -e.Angle * Mathf.Rad2Deg, 0f);
                sweep.localScale = new Vector3(Mathf.Max(distance, 1f), 5f, 12f);
                WebPortVisuals.SetMaterialColor(sweep.GetComponent<MeshRenderer>().sharedMaterial, Color.white.WithAlphaCompat(Mathf.Max(1f - p, 0f) * 0.9f));
            }

            for (; slot < _sweeps.Length; slot++)
                _sweeps[slot].gameObject.SetActive(false);
        }

        private void UpdateShockwaves(IReadOnlyList<EffectEvent> effects, float now)
        {
            int slot = 0;
            for (int i = 0; i < effects.Count && slot < _shockwaves.Length; i++)
            {
                EffectEvent e = effects[i];
                if (e.Kind != EffectKind.Shockwave)
                    continue;

                MeshRenderer renderer = _shockwaves[slot];
                float p = (now - e.StartedAt) / e.Duration;
                if (p >= 1f)
                {
                    renderer.gameObject.SetActive(false);
                    _boundShockwaves[slot] = null;
                    slot++;
                    continue;
                }

                if (_boundShockwaves[slot] != e)
                {
                    _boundShockwaves[slot] = e;
                    _shockwaveFilters[slot].sharedMesh = WebPortVisuals.CreateRingMesh(0.9f, 1f, 28, e.Angle - WebPortConstants.PushArcHalfAngle, WebPortConstants.PushArcHalfAngle * 2f);
                }

                float radius = Mathf.Max(e.Radius * Mathf.Min(p * 1.6f, 1f), 1f);
                renderer.gameObject.SetActive(true);
                renderer.transform.position = e.Position + Vector3.up * 4f;
                renderer.transform.localScale = Vector3.one * radius;
                WebPortVisuals.SetMaterialColor(renderer.sharedMaterial, WebPortVisuals.Orange.WithAlphaCompat(Mathf.Max(1f - p, 0f) * 0.85f));
                slot++;
            }

            for (; slot < _shockwaves.Length; slot++)
            {
                _shockwaves[slot].gameObject.SetActive(false);
                _boundShockwaves[slot] = null;
            }
        }

        private void UpdateDeliverBursts(IReadOnlyList<EffectEvent> effects, float now)
        {
            int slot = 0;
            for (int i = 0; i < effects.Count && slot < MaxBursts; i++)
            {
                EffectEvent e = effects[i];
                if (e.Kind != EffectKind.Deliver)
                    continue;

                if (_boundBursts[slot] != e)
                {
                    _boundBursts[slot] = e;
                    _seeds[slot] = CreateSeeds();
                }

                float p = (now - e.StartedAt) / e.Duration;
                if (p >= 1f)
                {
                    DisableBurst(slot);
                    slot++;
                    continue;
                }

                MeshRenderer ring = _burstRings[slot];
                ring.gameObject.SetActive(true);
                ring.transform.position = e.Position + Vector3.up * 6f;
                ring.transform.localScale = Vector3.one * (10f + p * 90f);
                WebPortVisuals.SetMaterialColor(ring.sharedMaterial, WebPortVisuals.Yellow.WithAlphaCompat(Mathf.Max(1f - p, 0f) * 0.9f));

                float t = p * e.Duration;
                for (int j = 0; j < ParticlesPerBurst; j++)
                {
                    MeshRenderer particle = _particles[slot * ParticlesPerBurst + j];
                    ParticleSeed seed = _seeds[slot][j];
                    particle.gameObject.SetActive(true);
                    particle.transform.position = e.Position + new Vector3(seed.Velocity.x * t, 20f + seed.Velocity.y * t - 0.5f * DeliverParticleGravity * t * t, seed.Velocity.z * t);
                    particle.transform.Rotate(12f, 9f, 0f, Space.Self);
                    WebPortVisuals.SetMaterialColor(particle.sharedMaterial, seed.Color.WithAlphaCompat(Mathf.Max(1f - p, 0f)));
                }

                slot++;
            }

            for (; slot < MaxBursts; slot++)
                DisableBurst(slot);
        }

        private void DisableBurst(int slot)
        {
            _burstRings[slot].gameObject.SetActive(false);
            _boundBursts[slot] = null;
            for (int i = 0; i < ParticlesPerBurst; i++)
                _particles[slot * ParticlesPerBurst + i].gameObject.SetActive(false);
        }

        private static ParticleSeed[] CreateSeeds()
        {
            Color[] colors =
            {
                WebPortVisuals.Yellow,
                WebPortVisuals.GoalGreen,
                Color.white,
                WebPortVisuals.Html("#f39c12"),
            };

            ParticleSeed[] seeds = new ParticleSeed[ParticlesPerBurst];
            for (int i = 0; i < seeds.Length; i++)
            {
                seeds[i] = new ParticleSeed
                {
                    Velocity = new Vector3(Random.Range(-110f, 110f), Random.Range(140f, 280f), Random.Range(-110f, 110f)),
                    Color = colors[Random.Range(0, colors.Length)],
                };
            }

            return seeds;
        }

        private static Color ColorFor(EffectKind kind)
        {
            return kind switch
            {
                EffectKind.Explosion => WebPortVisuals.Html("#e67e22"),
                EffectKind.GrabFlash => WebPortVisuals.TextDark,
                EffectKind.Impact => WebPortVisuals.Red,
                EffectKind.Swing => Color.white,
                _ => Color.white,
            };
        }

        private struct ParticleSeed
        {
            public Vector3 Velocity;
            public Color Color;
        }
    }
}
