using System.Collections.Generic;
using UnityEngine;

namespace FlareSystem
{
    public enum FieldFluidKind
    {
        Gas,
        Steam,
        Water,
        OilProduct
    }

    public struct FieldPipeRoute
    {
        public readonly string Name;
        public readonly FieldFluidKind FluidKind;
        public readonly Vector3[] Points;

        public FieldPipeRoute(string name, FieldFluidKind fluidKind, Vector3[] points)
        {
            Name = name;
            FluidKind = fluidKind;
            Points = points;
        }
    }

    public static class FieldModelConstants
    {
        public const string DefaultCsvFileName = "variant_3_4.csv";
        public const string FieldRootName = "PetroleumFieldFlareSystem";
        public const string PipeFlowRootName = "FieldPipeFlows";
        public const string FlameAnchorName = "FieldFlareFlameAnchor";

        public static readonly Vector3 DNSPosition = new Vector3(-6.3f, -1.9f, 2.03f);
        public static readonly Vector3 UPSVPosition = new Vector3(3.3f, -0.7f, 1.83f);
        public static readonly Vector3 UPNPosition = new Vector3(14.5f, 1.0f, 2.0f);
        public static readonly Vector3 KNSPosition = new Vector3(11.35f, 12.3f, 0.95f);
        public static readonly Vector3 BKNSPosition = new Vector3(-5.18f, 12.0f, 0.95f);
        public static readonly Vector3 SeparatorPosition = new Vector3(21.25f, 17.14f, 4.55f);
        public static readonly Vector3 FlareStackBasePosition = new Vector3(30.49f, 22.14f, 6.10f);
        public static readonly Vector3 FlareTipPosition = new Vector3(31.14f, 22.14f, 38.10f);

        public static readonly FieldPipeRoute[] Routes =
        {
            new FieldPipeRoute("Gas_line_DNS_UPSV", FieldFluidKind.Gas, new[]
            {
                new Vector3(-6.3f, -1.9f, 2.03f),
                new Vector3(-6.3f, -1.9f, 2.30f),
                new Vector3(-6.3f, -0.7f, 2.30f),
                new Vector3(3.3f, -0.7f, 2.30f),
                new Vector3(3.3f, -0.7f, 1.83f)
            }),
            new FieldPipeRoute("Gas_line_UPSV_UPN", FieldFluidKind.Gas, new[]
            {
                new Vector3(3.3f, -0.7f, 2.30f),
                new Vector3(3.3f, 3.35f, 2.30f),
                new Vector3(14.5f, 3.35f, 2.30f),
                new Vector3(14.5f, 1.0f, 2.30f),
                new Vector3(14.5f, 1.0f, 2.00f)
            }),
            new FieldPipeRoute("Gas_line_UPN_to_flare_sep", FieldFluidKind.Gas, new[]
            {
                new Vector3(14.5f, 3.35f, 2.30f),
                new Vector3(14.5f, 3.35f, 5.50f),
                new Vector3(19.27f, 3.35f, 5.50f),
                new Vector3(19.27f, 17.14f, 5.50f),
                new Vector3(19.27f, 17.14f, 4.55f)
            }),
            new FieldPipeRoute("Gas_line_sep_vent_to_stack", FieldFluidKind.Gas, new[]
            {
                new Vector3(23.24f, 17.14f, 4.55f),
                new Vector3(23.24f, 17.14f, 5.50f),
                new Vector3(30.49f, 17.14f, 5.50f),
                new Vector3(30.49f, 22.14f, 5.50f),
                new Vector3(30.49f, 22.14f, 6.10f)
            }),
            new FieldPipeRoute("Steam_line_stack_to_KNS", FieldFluidKind.Steam, new[]
            {
                new Vector3(30.49f, 22.14f, 0.60f),
                new Vector3(30.49f, 12.30f, 0.60f),
                new Vector3(11.35f, 12.30f, 0.60f),
                new Vector3(11.35f, 12.30f, 0.95f)
            }),
            new FieldPipeRoute("Produced_water_UPSV_to_KNS", FieldFluidKind.Water, new[]
            {
                new Vector3(8.10f, 0.90f, 1.09f),
                new Vector3(8.80f, 0.90f, 1.09f),
                new Vector3(8.80f, 3.50f, 1.09f),
                new Vector3(5.52f, 3.50f, 1.09f),
                new Vector3(5.52f, 12.4f, 1.09f)
            }),
            new FieldPipeRoute("KNS_to_BKNS_water", FieldFluidKind.Water, new[]
            {
                new Vector3(10.32f, 12.30f, 0.62f),
                new Vector3(11.35f, 12.30f, 0.62f),
                new Vector3(11.35f, 12.30f, 0.95f),
                new Vector3(11.35f, 13.85f, 0.95f),
                new Vector3(-5.18f, 13.85f, 0.95f),
                new Vector3(-5.18f, 12.0f, 0.95f)
            }),
            new FieldPipeRoute("BKNS_to_injection_well", FieldFluidKind.Water, new[]
            {
                new Vector3(-2.78f, 12.0f, 1.05f),
                new Vector3(-2.78f, 15.0f, 1.05f),
                new Vector3(-0.20f, 15.0f, 1.05f)
            }),
            new FieldPipeRoute("UPSV_separator_inlet", FieldFluidKind.OilProduct, new[]
            {
                new Vector3(-0.3f, -0.9f, 1.09f),
                new Vector3(0.5f, -0.9f, 1.20f),
                new Vector3(2.0f, -0.9f, 1.20f),
                new Vector3(2.0f, 1.00f, 1.20f),
                new Vector3(1.62f, 1.00f, 1.20f)
            }),
            new FieldPipeRoute("UPN_tank_B_to_pump_suction", FieldFluidKind.OilProduct, new[]
            {
                new Vector3(22.13f, 1.00f, 1.225f),
                new Vector3(22.13f, 1.00f, 0.55f),
                new Vector3(22.13f, -0.50f, 0.55f),
                new Vector3(18.02f, -0.50f, 0.55f),
                new Vector3(18.02f, -2.40f, 0.55f)
            }),
            new FieldPipeRoute("UPN_pump_to_export", FieldFluidKind.OilProduct, new[]
            {
                new Vector3(20.42f, -2.40f, 0.62f),
                new Vector3(20.42f, -2.40f, 1.20f),
                new Vector3(29.00f, -2.40f, 1.20f)
            })
        };

        public static IEnumerable<FieldPipeRoute> GasRoutes => FilterRoutes(FieldFluidKind.Gas);
        public static IEnumerable<FieldPipeRoute> SteamRoutes => FilterRoutes(FieldFluidKind.Steam);
        public static IEnumerable<FieldPipeRoute> WaterRoutes => FilterRoutes(FieldFluidKind.Water);
        public static IEnumerable<FieldPipeRoute> OilProductRoutes => FilterRoutes(FieldFluidKind.OilProduct);

        public static Color GetRouteColor(FieldFluidKind kind)
        {
            switch (kind)
            {
                case FieldFluidKind.Steam:
                    return new Color(0.72f, 0.95f, 1f, 0.9f);
                case FieldFluidKind.Water:
                    return new Color(0.1f, 0.45f, 1f, 0.9f);
                case FieldFluidKind.OilProduct:
                    return new Color(0.95f, 0.72f, 0.18f, 0.9f);
                default:
                    return new Color(0.68f, 0.92f, 0.72f, 0.9f);
            }
        }

        public static string GetFlowName(FieldFluidKind kind)
        {
            switch (kind)
            {
                case FieldFluidKind.Steam:
                    return "Steam";
                case FieldFluidKind.Water:
                    return "Water";
                case FieldFluidKind.OilProduct:
                    return "OilProduct";
                default:
                    return "ReliefGas";
            }
        }

        public static Vector3 XyzToUnityYUp(Vector3 point)
        {
            return new Vector3(point.x, point.z, point.y);
        }

        private static IEnumerable<FieldPipeRoute> FilterRoutes(FieldFluidKind kind)
        {
            for (int i = 0; i < Routes.Length; i++)
            {
                if (Routes[i].FluidKind == kind)
                {
                    yield return Routes[i];
                }
            }
        }
    }
}
