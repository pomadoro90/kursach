using UnityEngine;

namespace FlareSystem
{
    public class FlameController : MonoBehaviour
    {
        public Transform flameRoot;
        public ParticleSystem flameParticles;
        public Light flameLight;
        public Renderer flameRenderer;

        public Material normalMaterial;
        public Material warningMaterial;
        public Material dangerMaterial;
        public Material particleMaterial;

        [Header("Scale")]
        public Vector3 normalScale = Vector3.one;
        public Vector3 warningScale = new Vector3(1.15f, 1.25f, 1.15f);
        public Vector3 dangerScale = new Vector3(1.35f, 1.55f, 1.35f);
        public Vector3 alarmScale = new Vector3(1.55f, 1.8f, 1.55f);

        [Header("Colors")]
        public Color normalColor = new Color(0.24f, 1f, 0.72f, 0.9f);
        public Color warningColor = new Color(1f, 0.78f, 0.18f, 0.95f);
        public Color dangerColor = new Color(1f, 0.24f, 0.08f, 1f);

        public string CurrentFlameState { get; private set; } = "Normal";

        private RiskLevel currentLevel = RiskLevel.Normal;
        private Vector3 baseScale = Vector3.one;
        private Material runtimeParticleMaterial;

        private void Awake()
        {
            EnsureEffect();
            baseScale = flameRoot != null ? flameRoot.localScale : Vector3.one;
            SetNormal();
        }

        private void Update()
        {
            if (flameRoot == null)
            {
                return;
            }

            if (currentLevel == RiskLevel.Alarm)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 7f) * 0.18f;
                flameRoot.localScale = Vector3.Scale(baseScale, alarmScale * pulse);
                SetLight(dangerColor, 3f + Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 4f);
            }
            else if (currentLevel == RiskLevel.Danger)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.08f;
                flameRoot.localScale = Vector3.Scale(baseScale, dangerScale * pulse);
            }
            else if (currentLevel == RiskLevel.Warning)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 2.5f) * 0.04f;
                flameRoot.localScale = Vector3.Scale(baseScale, warningScale * pulse);
            }
        }

        public void EnsureEffect()
        {
            if (flameRoot == null)
            {
                Transform existing = transform.Find("FlameEffect");
                flameRoot = existing != null ? existing : new GameObject("FlameEffect").transform;
                flameRoot.SetParent(transform, false);
                flameRoot.localPosition = Vector3.up * 5f;
            }

            if (flameParticles == null)
            {
                flameParticles = flameRoot.GetComponent<ParticleSystem>();
                if (flameParticles == null)
                {
                    flameParticles = flameRoot.gameObject.AddComponent<ParticleSystem>();
                }
            }

            ConfigureParticleSystem();

            if (flameLight == null)
            {
                flameLight = flameRoot.GetComponentInChildren<Light>(true);
                if (flameLight == null)
                {
                    GameObject lightObject = new GameObject("Flame Point Light");
                    lightObject.transform.SetParent(flameRoot, false);
                    lightObject.transform.localPosition = Vector3.up * 0.6f;
                    flameLight = lightObject.AddComponent<Light>();
                    flameLight.type = LightType.Point;
                    flameLight.range = 14f;
                }
            }

            if (flameRenderer == null)
            {
                flameRenderer = flameRoot.GetComponentInChildren<MeshRenderer>(true);
            }
        }

        public void SetNormal()
        {
            currentLevel = RiskLevel.Normal;
            CurrentFlameState = "Normal";
            ApplyState(normalColor, normalScale, 42f, 0.10f, 1.5f, normalMaterial);
        }

        public void SetWarning()
        {
            currentLevel = RiskLevel.Warning;
            CurrentFlameState = "Warning";
            ApplyState(warningColor, warningScale, 54f, 0.14f, 2.3f, warningMaterial);
        }

        public void SetDanger()
        {
            currentLevel = RiskLevel.Danger;
            CurrentFlameState = "Danger";
            ApplyState(dangerColor, dangerScale, 68f, 0.16f, 3.6f, dangerMaterial);
        }

        public void SetAlarm()
        {
            currentLevel = RiskLevel.Alarm;
            CurrentFlameState = "Alarm";
            ApplyState(dangerColor, alarmScale, 82f, 0.18f, 5.5f, dangerMaterial);
        }

        public void SetByProbability(float probability)
        {
            switch (FlareConstants.ProbabilityToRiskLevel(probability))
            {
                case RiskLevel.Alarm:
                    SetAlarm();
                    break;
                case RiskLevel.Danger:
                    SetDanger();
                    break;
                case RiskLevel.Warning:
                    SetWarning();
                    break;
                default:
                    SetNormal();
                    break;
            }
        }

        public void SetByRiskLevel(RiskLevel level)
        {
            switch (level)
            {
                case RiskLevel.Alarm:
                    SetAlarm();
                    break;
                case RiskLevel.Danger:
                    SetDanger();
                    break;
                case RiskLevel.Warning:
                    SetWarning();
                    break;
                default:
                    SetNormal();
                    break;
            }
        }

        private void ConfigureParticleSystem()
        {
            var main = flameParticles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.85f;
            main.startSpeed = 1.8f;
            main.startSize = 0.38f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = flameParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = 30f;

            var shape = flameParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 17f;
            shape.radius = 0.24f;

            var colorOverLifetime = flameParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(normalColor, 0f),
                    new GradientColorKey(new Color(1f, 0.96f, 0.55f), 0.45f),
                    new GradientColorKey(new Color(0.2f, 0.22f, 0.24f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.75f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var renderer = flameParticles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 1f;
            renderer.minParticleSize = 0.004f;
            renderer.maxParticleSize = 0.045f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = particleMaterial != null ? particleMaterial : GetRuntimeParticleMaterial();

            if (!flameParticles.isPlaying)
            {
                flameParticles.Play();
            }
        }

        private void ApplyState(Color color, Vector3 scale, float rate, float particleSize, float lightIntensity, Material material)
        {
            EnsureEffect();

            if (flameRoot != null)
            {
                flameRoot.localScale = Vector3.Scale(baseScale, scale);
            }

            var main = flameParticles.main;
            main.startColor = color;
            main.startSize = particleSize;

            var emission = flameParticles.emission;
            emission.rateOverTime = rate;

            var shape = flameParticles.shape;
            shape.angle = currentLevel == RiskLevel.Normal ? 14f : 22f;
            ApplyLifetimeGradient(color);

            SetLight(color, lightIntensity);

            if (flameRenderer != null && material != null)
            {
                flameRenderer.sharedMaterial = material;
            }
        }

        private void SetLight(Color color, float intensity)
        {
            if (flameLight == null)
            {
                return;
            }

            flameLight.color = color;
            flameLight.intensity = intensity;
            flameLight.enabled = true;
        }

        private void ApplyLifetimeGradient(Color color)
        {
            if (flameParticles == null)
            {
                return;
            }

            var colorOverLifetime = flameParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;

            Color core = Color.Lerp(Color.white, color, 0.42f);
            Color smoke = Color.Lerp(color, new Color(0.06f, 0.06f, 0.06f), 0.82f);
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(core, 0f),
                    new GradientColorKey(color, 0.45f),
                    new GradientColorKey(smoke, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.86f, 0f),
                    new GradientAlphaKey(0.68f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;
        }

        private Material GetRuntimeParticleMaterial()
        {
            if (runtimeParticleMaterial != null)
            {
                return runtimeParticleMaterial;
            }

            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Particles/Additive");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            runtimeParticleMaterial = new Material(shader)
            {
                name = "Runtime_FlameParticles",
                hideFlags = HideFlags.DontSave
            };
            runtimeParticleMaterial.color = Color.white;
            return runtimeParticleMaterial;
        }
    }
}
