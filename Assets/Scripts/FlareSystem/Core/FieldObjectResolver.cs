using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FlareSystem
{
    public class FieldObjectResolver : FlareObjectResolver
    {
        private static readonly string[] DnsNames = { "dns", "днс" };
        private static readonly string[] UpsvNames = { "upsv", "упсв" };
        private static readonly string[] UpnNames = { "upn", "упн" };
        private static readonly string[] KnsNames = { "kns", "кнс" };
        private static readonly string[] BknsNames = { "bkns", "бкнс" };
        private static readonly string[] FieldSeparatorNames = { "separator", "sep", "сепаратор" };
        private static readonly string[] FieldFlareNames = { "flare", "stack", "fakel", "факел", "свеча", "труба" };
        private static readonly string[] FieldFlameNames = { "flame", "tip", "fire", "пламя", FieldModelConstants.FlameAnchorName };
        private static readonly string[] GasPipeNames = { "gas", "flare", "vent", "сброс", "газ" };
        private static readonly string[] SteamPipeNames = { "steam", "пар" };
        private static readonly string[] WaterPipeNames = { "water", "kns", "bkns", "вода", "вод" };
        private static readonly string[] OilPipeNames = { "oil", "product", "tank", "pump", "нефть", "продукт" };

        public Transform FindDNS() => FindFirst(DnsNames, "DNS");
        public Transform FindUPSV() => FindFirst(UpsvNames, "UPSV");
        public Transform FindUPN() => FindFirst(UpnNames, "UPN");
        public Transform FindKNS() => FindFirst(KnsNames, "KNS");
        public Transform FindBKNS() => FindFirst(BknsNames, "BKNS");
        public Transform FindFieldSeparator() => FindFirst(FieldSeparatorNames, "Field separator");
        public Transform FindFieldFlareStack() => FindFirst(FieldFlareNames, "Field flare stack");
        public Transform FindFieldFlameAnchor() => FindFirst(FieldFlameNames, "Field flame anchor");

        public List<Transform> FindGasPipes() => FindMany(GasPipeNames, "Gas pipes");
        public List<Transform> FindSteamPipes() => FindMany(SteamPipeNames, "Steam pipes");
        public List<Transform> FindWaterPipes() => FindMany(WaterPipeNames, "Water pipes");
        public List<Transform> FindOilProductPipes() => FindMany(OilPipeNames, "Oil/product pipes");

        public Transform ResolveOrCreateAnchor(string name, Vector3 position, Transform parent = null)
        {
            Transform found = EnumerateTransforms().FirstOrDefault(t => t != null && t.name == name);
            if (found != null)
            {
                return found;
            }

            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(parent != null ? parent : transform, true);
            anchor.transform.position = position;
            return anchor.transform;
        }

        public Transform ResolveFacility(FieldFacility facility)
        {
            switch (facility)
            {
                case FieldFacility.DNS:
                    return FindDNS();
                case FieldFacility.UPSV:
                    return FindUPSV();
                case FieldFacility.UPN:
                    return FindUPN();
                case FieldFacility.KNS:
                    return FindKNS();
                case FieldFacility.BKNS:
                    return FindBKNS();
                case FieldFacility.Separator:
                    return FindFieldSeparator();
                case FieldFacility.FlareStack:
                    return FindFieldFlareStack();
                default:
                    return null;
            }
        }
    }

    public enum FieldFacility
    {
        DNS,
        UPSV,
        UPN,
        KNS,
        BKNS,
        Separator,
        FlareStack
    }
}
