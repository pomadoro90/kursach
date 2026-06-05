using System.Collections.Generic;
using UnityEngine;

namespace FlareSystem
{
    public enum FieldCoordinateMode
    {
        XyzAsUnity,
        XyzToUnityYUp
    }

    public class FieldPipeFlowController : PipeFlowController
    {
        [Header("Field Model")]
        public Transform fieldFlowRoot;
        public FieldCoordinateMode coordinateMode = FieldCoordinateMode.XyzAsUnity;
        public bool rebuildRoutesOnAwake = true;
        public bool includeWaterRoutes = true;
        public bool includeOilProductRoutes = true;

        [Header("Field Flow Tuning")]
        public float gasSpeed = 4.5f;
        public float steamSpeed = 3.5f;
        public float waterSpeed = 2.1f;
        public float oilProductSpeed = 1.8f;

        private void Awake()
        {
            if (rebuildRoutesOnAwake)
            {
                BuildFieldRoutes();
            }
        }

        public void BuildFieldRoutes()
        {
            Transform root = EnsureFieldFlowRoot();
            ClearRouteChildren(root);
            flowPaths.Clear();

            for (int i = 0; i < FieldModelConstants.Routes.Length; i++)
            {
                FieldPipeRoute route = FieldModelConstants.Routes[i];
                if (!ShouldInclude(route.FluidKind))
                {
                    continue;
                }

                PipeFlowPath path = CreatePath(root, route);
                flowPaths.Add(path);
            }

            BuildRuntimeFlows();
            if (playOnStart)
            {
                StartFlow();
            }
        }

        public void ApplyRecord(FlareDataRecord record)
        {
            if (record == null)
            {
                StopFlow();
                return;
            }

            StartFlow();

            float gasIntensity = Mathf.Max(3f, record.Q_flare * 5.5f + record.Q_purge * 0.25f);
            float steamIntensity = Mathf.Max(2f, record.Steam_Q * 0.65f);
            float waterIntensity = Mathf.Lerp(4f, 18f, Mathf.Clamp01(record.Q_flare / 10f));
            float productIntensity = Mathf.Lerp(3f, 12f, Mathf.Clamp01(record.P_flare / 0.03f));

            SetPathIntensity("ReliefGas", gasIntensity);
            SetPathIntensity("Steam", steamIntensity);
            SetPathIntensity("Water", waterIntensity);
            SetPathIntensity("OilProduct", productIntensity);

            float global = Mathf.Clamp01((record.Q_flare + record.Q_purge * 0.08f + record.Steam_Q * 0.035f) / 12f);
            SetIntensity(Mathf.Lerp(0.45f, 1.9f, global));
        }

        private Transform EnsureFieldFlowRoot()
        {
            if (fieldFlowRoot != null)
            {
                return fieldFlowRoot;
            }

            Transform existing = transform.Find(FieldModelConstants.PipeFlowRootName);
            if (existing != null)
            {
                fieldFlowRoot = existing;
                return fieldFlowRoot;
            }

            GameObject root = new GameObject(FieldModelConstants.PipeFlowRootName);
            root.transform.SetParent(transform, false);
            fieldFlowRoot = root.transform;
            return fieldFlowRoot;
        }

        private PipeFlowPath CreatePath(Transform root, FieldPipeRoute route)
        {
            GameObject pathObject = new GameObject(route.Name);
            pathObject.transform.SetParent(root, false);

            PipeFlowPath path = pathObject.AddComponent<PipeFlowPath>();
            path.flowName = FieldModelConstants.GetFlowName(route.FluidKind);
            path.flowColor = FieldModelConstants.GetRouteColor(route.FluidKind);
            path.speed = GetSpeed(route.FluidKind);
            path.particleSize = GetParticleSize(route.FluidKind);
            path.loop = true;

            for (int i = 0; i < route.Points.Length; i++)
            {
                GameObject waypoint = new GameObject("WP_" + (i + 1).ToString("00"));
                waypoint.transform.SetParent(pathObject.transform, false);
                waypoint.transform.position = ConvertPoint(route.Points[i]);
                path.waypoints.Add(waypoint.transform);
            }

            return path;
        }

        private bool ShouldInclude(FieldFluidKind kind)
        {
            if (kind == FieldFluidKind.Water)
            {
                return includeWaterRoutes;
            }

            if (kind == FieldFluidKind.OilProduct)
            {
                return includeOilProductRoutes;
            }

            return true;
        }

        private Vector3 ConvertPoint(Vector3 point)
        {
            return coordinateMode == FieldCoordinateMode.XyzToUnityYUp
                ? FieldModelConstants.XyzToUnityYUp(point)
                : point;
        }

        private float GetSpeed(FieldFluidKind kind)
        {
            switch (kind)
            {
                case FieldFluidKind.Steam:
                    return steamSpeed;
                case FieldFluidKind.Water:
                    return waterSpeed;
                case FieldFluidKind.OilProduct:
                    return oilProductSpeed;
                default:
                    return gasSpeed;
            }
        }

        private static float GetParticleSize(FieldFluidKind kind)
        {
            switch (kind)
            {
                case FieldFluidKind.Water:
                    return 0.07f;
                case FieldFluidKind.OilProduct:
                    return 0.075f;
                case FieldFluidKind.Steam:
                    return 0.085f;
                default:
                    return 0.08f;
            }
        }

        private static void ClearRouteChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
