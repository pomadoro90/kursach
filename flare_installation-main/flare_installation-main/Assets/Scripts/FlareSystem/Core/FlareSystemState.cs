using System;
using System.Collections.Generic;

namespace FlareSystem
{
    [Serializable]
    public class FlareSystemState
    {
        public FlareDataRecord Record;
        public FlareMode Mode;
        public float RiskProbability;
        public RiskLevel RiskLevel;
        public bool IsAlarm;
        public string StatusText = "Норма";
        public string Recommendation = "Параметры в допустимых пределах";
        public readonly List<string> Violations = new List<string>();

        public string ViolationsText
        {
            get
            {
                return Violations.Count == 0 ? "Нарушений нет" : string.Join("\n", Violations);
            }
        }
    }
}
