using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FlareSystem
{
    public class FlareObjectResolver : MonoBehaviour
    {
        public Transform modelRoot;

        private static readonly string[] SeparatorNames = { "separator", "knockout", "drum" };
        private static readonly string[] FlareStackNames = { "flare", "stack", "chimney", "tower" };
        private static readonly string[] DrainTankNames = { "drain", "condensate", "tank" };
        private static readonly string[] ControlCabinetNames = { "cabinet", "panel", "control" };
        private static readonly string[] FlameNames = { "flame", "tip", "flare_head", "FLAME_PLACEHOLDER", "VIDEO_FLAME_PLACEHOLDER" };
        private static readonly string[] PipeNames = { "pipe", "gas", "purge", "steam", "condensate" };
        private static readonly string[] ValveNames = { "valve", "handwheel", "wheel", "вентиль", "задвижка" };
        private static readonly string[] SensorNames = { "sensor", "pressure", "temperature", "level", "flow" };

        public Transform FindSeparator() => FindFirst(SeparatorNames, "Separator");
        public Transform FindFlareStack() => FindFirst(FlareStackNames, "Flare stack");
        public Transform FindDrainTank() => FindFirst(DrainTankNames, "Drain tank");
        public Transform FindControlCabinet() => FindFirst(ControlCabinetNames, "Control cabinet");
        public Transform FindFlameAnchor() => FindFirst(FlameNames, "Flame");

        public List<Transform> FindPipes() => FindMany(PipeNames, "Pipes");
        public List<Transform> FindValves() => FindMany(ValveNames, "Valves");
        public List<Transform> FindSensors() => FindMany(SensorNames, "Sensors");

        public Transform FindFirst(IEnumerable<string> partialNames, string categoryName)
        {
            Transform found = EnumerateTransforms()
                .FirstOrDefault(t => Matches(t.name, partialNames));

            if (found == null)
            {
                Debug.LogWarning($"FlareObjectResolver: объект категории '{categoryName}' не найден. Назначьте его вручную при необходимости.");
            }

            return found;
        }

        public List<Transform> FindMany(IEnumerable<string> partialNames, string categoryName)
        {
            var found = EnumerateTransforms()
                .Where(t => Matches(t.name, partialNames))
                .Distinct()
                .ToList();

            if (found.Count == 0)
            {
                Debug.LogWarning($"FlareObjectResolver: объекты категории '{categoryName}' не найдены. Будут созданы учебные маркеры.");
            }

            return found;
        }

        public IEnumerable<Transform> EnumerateTransforms()
        {
            if (modelRoot != null)
            {
                return modelRoot.GetComponentsInChildren<Transform>(true);
            }

#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
#else
            return Object.FindObjectsOfType<Transform>(true);
#endif
        }

        public static bool Matches(string objectName, IEnumerable<string> partialNames)
        {
            string lower = objectName.ToLowerInvariant();
            foreach (string partial in partialNames)
            {
                if (lower.Contains(partial.ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
