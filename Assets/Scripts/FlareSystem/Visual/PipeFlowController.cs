using System.Collections.Generic;
using UnityEngine;

namespace FlareSystem
{
    public class PipeFlowController : MonoBehaviour
    {
        public List<PipeFlowPath> flowPaths = new List<PipeFlowPath>();
        public float globalIntensity = 1f;
        public bool playOnStart = true;
        public bool createVisibleMarkers = true;
        public int markersPerPath = 10;
        public Material reliefGasMaterial;
        public Material purgeGasMaterial;
        public Material steamMaterial;

        private readonly List<RuntimeFlow> runtimeFlows = new List<RuntimeFlow>();
        private readonly List<FlowParticleMover> visibleMarkers = new List<FlowParticleMover>();
        private bool isFlowing;

        private void Awake()
        {
            BuildRuntimeFlows();
            if (playOnStart)
            {
                StartFlow();
            }
        }

        private void Update()
        {
            if (!isFlowing)
            {
                return;
            }

            for (int i = 0; i < runtimeFlows.Count; i++)
            {
                RuntimeFlow flow = runtimeFlows[i];
                if (flow.Path == null || flow.System == null)
                {
                    continue;
                }

                float length = Mathf.Max(flow.Path.GetLength(), 0.1f);
                flow.Progress += Time.deltaTime * flow.Path.speed / length;
                if (!flow.Path.loop)
                {
                    flow.Progress = Mathf.Clamp01(flow.Progress);
                }

                flow.System.transform.position = flow.Path.Evaluate(flow.Progress);
            }
        }

        public void BuildRuntimeFlows()
        {
            runtimeFlows.Clear();
            visibleMarkers.Clear();
            foreach (PipeFlowPath path in flowPaths)
            {
                if (path == null)
                {
                    continue;
                }

                ClearGeneratedMarkers(path.transform);

                ParticleSystem system = path.GetComponentInChildren<ParticleSystem>(true);
                if (system == null)
                {
                    GameObject systemObject = new GameObject(path.flowName + "_ParticleSystem");
                    systemObject.transform.SetParent(path.transform, false);
                    system = systemObject.AddComponent<ParticleSystem>();
                }

                ConfigureSystem(system, path);
                runtimeFlows.Add(new RuntimeFlow(path, system));
                EnsureLineRenderer(path);
                if (createVisibleMarkers)
                {
                    EnsureVisibleMarkers(path);
                }
            }
        }

        public void StartFlow()
        {
            if (runtimeFlows.Count == 0)
            {
                BuildRuntimeFlows();
            }

            isFlowing = true;
            foreach (RuntimeFlow flow in runtimeFlows)
            {
                if (flow.System != null && !flow.System.isPlaying)
                {
                    flow.System.Play();
                }
            }

            foreach (FlowParticleMover marker in visibleMarkers)
            {
                if (marker != null)
                {
                    marker.gameObject.SetActive(true);
                }
            }
        }

        public void StopFlow()
        {
            isFlowing = false;
            foreach (RuntimeFlow flow in runtimeFlows)
            {
                if (flow.System != null)
                {
                    flow.System.Stop();
                }
            }

            foreach (FlowParticleMover marker in visibleMarkers)
            {
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                }
            }
        }

        public void SetIntensity(float intensity)
        {
            globalIntensity = Mathf.Max(0f, intensity);
            foreach (RuntimeFlow flow in runtimeFlows)
            {
                if (flow.System == null || flow.Path == null)
                {
                    continue;
                }

                var emission = flow.System.emission;
                emission.rateOverTime = 12f * globalIntensity;

                var main = flow.System.main;
                main.startSize = Mathf.Max(0.02f, flow.Path.particleSize * Mathf.Lerp(0.65f, 1.4f, Mathf.Clamp01(globalIntensity)));
            }

            foreach (FlowParticleMover marker in visibleMarkers)
            {
                if (marker == null)
                {
                    continue;
                }

                marker.speedMultiplier = Mathf.Lerp(0.45f, 1.75f, Mathf.Clamp01(globalIntensity));
                marker.transform.localScale = Vector3.one * Mathf.Lerp(0.028f, 0.055f, Mathf.Clamp01(globalIntensity));
            }
        }

        public void SetColor(Color color)
        {
            foreach (RuntimeFlow flow in runtimeFlows)
            {
                if (flow.System == null)
                {
                    continue;
                }

                var main = flow.System.main;
                main.startColor = color;
            }

            foreach (FlowParticleMover marker in visibleMarkers)
            {
                if (marker != null)
                {
                    marker.SetColor(color);
                }
            }
        }

        private void EnsureLineRenderer(PipeFlowPath path)
        {
            LineRenderer line = path.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = path.gameObject.AddComponent<LineRenderer>();
            }

            line.useWorldSpace = true;
            line.positionCount = path.waypoints != null ? path.waypoints.Count : 0;
            float width = Mathf.Max(0.008f, path.particleSize * 0.55f);
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = path.flowColor;
            line.endColor = path.flowColor;
            line.material = GetFlowMaterial(path);

            for (int i = 0; i < line.positionCount; i++)
            {
                line.SetPosition(i, path.waypoints[i].position);
            }
        }

        private void EnsureVisibleMarkers(PipeFlowPath path)
        {
            int count = Mathf.Clamp(markersPerPath, 3, 32);
            for (int i = 0; i < count; i++)
            {
                GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                markerObject.name = path.flowName + "_FlowParticle_" + (i + 1).ToString("00");
                markerObject.transform.SetParent(path.transform, false);
                markerObject.transform.localScale = Vector3.one * Mathf.Clamp(path.particleSize * 1.1f, 0.025f, 0.055f);

                Collider collider = markerObject.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroyImmediateSafe(collider);
                }

                Renderer renderer = markerObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material material = GetFlowMaterial(path);
                    if (material != null)
                    {
                        renderer.sharedMaterial = material;
                    }
                    else
                    {
                        renderer.material.color = path.flowColor;
                    }
                }

                FlowParticleMover mover = markerObject.AddComponent<FlowParticleMover>();
                mover.path = path;
                mover.offset = (float)i / count;
                mover.markerRenderer = renderer;
                markerObject.transform.position = path.Evaluate(mover.offset);
                visibleMarkers.Add(mover);
            }
        }

        private Material GetFlowMaterial(PipeFlowPath path)
        {
            string flowName = path.flowName.ToLowerInvariant();
            if (flowName.Contains("purge"))
            {
                return purgeGasMaterial;
            }

            if (flowName.Contains("steam"))
            {
                return steamMaterial;
            }

            return reliefGasMaterial;
        }

        private static void DestroyImmediateSafe(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static void ClearGeneratedMarkers(Transform pathRoot)
        {
            if (pathRoot == null)
            {
                return;
            }

            for (int i = pathRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = pathRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.GetComponent<FlowParticleMover>() != null || child.name.Contains("_FlowParticle_"))
                {
                    DestroyImmediateSafe(child.gameObject);
                }
            }
        }

        public void SetPathIntensity(string flowName, float intensity)
        {
            foreach (RuntimeFlow flow in runtimeFlows)
            {
                if (flow.Path == null || flow.System == null || flow.Path.flowName != flowName)
                {
                    continue;
                }

                var emission = flow.System.emission;
                emission.rateOverTime = Mathf.Max(0f, intensity);
            }
        }

        private static void ConfigureSystem(ParticleSystem system, PipeFlowPath path)
        {
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.5f;
            main.startSpeed = 0.3f;
            main.startSize = path.particleSize;
            main.startColor = path.flowColor;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 12f;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private class RuntimeFlow
        {
            public readonly PipeFlowPath Path;
            public readonly ParticleSystem System;
            public float Progress;

            public RuntimeFlow(PipeFlowPath path, ParticleSystem system)
            {
                Path = path;
                System = system;
                Progress = Random.value;
            }
        }
    }
}
