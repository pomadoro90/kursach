using System.Collections.Generic;
using UnityEngine;

namespace FlareSystem
{
    public static class FlareConstants
    {
        public const float PFlareMin = 0.004f;
        public const float PFlareMax = 0.016f;
        public const float QFlareMin = 1.5f;
        public const float PPurgeMin = 0.3f;
        public const float PPurgeMax = 0.6f;
        public const float QPurgeMin = 20f;
        public const float QPurgeMax = 50f;
        public const float TFlameMin = 800f;
        public const float TFlameMax = 1200f;

        public const float FlameLiftPPurgeAlarm = 0.28f;
        public const float FlameLiftQPurgeAlarm = 22f;
        public const float HlopokPFlareAlarm = 0.022f;

        public static readonly Color NormalColor = new Color(0.16f, 0.82f, 0.38f);
        public static readonly Color WarningColor = new Color(1f, 0.78f, 0.16f);
        public static readonly Color DangerColor = new Color(1f, 0.24f, 0.12f);
        public static readonly Color FlowReliefGasColor = new Color(0.86f, 0.9f, 0.94f, 0.85f);
        public static readonly Color FlowPurgeGasColor = new Color(0.2f, 0.48f, 1f, 0.9f);
        public static readonly Color FlowSteamColor = new Color(0.74f, 0.96f, 1f, 0.85f);

        public static FlareSystemState EvaluateRecord(FlareDataRecord record, FlareMode mode, float probability)
        {
            var state = new FlareSystemState
            {
                Record = record,
                Mode = mode,
                RiskProbability = probability,
                RiskLevel = ProbabilityToRiskLevel(probability),
                Recommendation = GetRecommendation(record)
            };

            state.Violations.AddRange(GetViolations(record));
            bool explicitAlarm = record != null && (record.otriv == 1 || record.hlopok == 1 || IsFlameLiftCondition(record) || IsHlopokCondition(record));

            if (explicitAlarm || probability > 0.8f)
            {
                state.RiskLevel = RiskLevel.Alarm;
                state.IsAlarm = true;
                state.StatusText = "Авария";
            }
            else if (state.Violations.Count > 0 || probability >= 0.3f)
            {
                state.RiskLevel = probability > 0.7f ? RiskLevel.Danger : RiskLevel.Warning;
                state.StatusText = state.RiskLevel == RiskLevel.Danger ? "Опасность" : "Предупреждение";
            }
            else
            {
                state.RiskLevel = RiskLevel.Normal;
                state.StatusText = "Норма";
            }

            return state;
        }

        public static RiskLevel ProbabilityToRiskLevel(float probability)
        {
            if (probability > 0.8f)
            {
                return RiskLevel.Alarm;
            }

            if (probability > 0.7f)
            {
                return RiskLevel.Danger;
            }

            if (probability >= 0.3f)
            {
                return RiskLevel.Warning;
            }

            return RiskLevel.Normal;
        }

        public static List<string> GetViolations(FlareDataRecord record)
        {
            var violations = new List<string>();
            if (record == null)
            {
                return violations;
            }

            if (record.P_flare < PFlareMin)
            {
                violations.Add("P_flare ниже нормы");
            }
            else if (record.P_flare > PFlareMax)
            {
                violations.Add("P_flare выше нормы");
            }

            if (record.Q_flare < QFlareMin)
            {
                violations.Add("Q_flare ниже нормы");
            }

            if (record.P_purge < PPurgeMin)
            {
                violations.Add("P_purge ниже нормы");
            }
            else if (record.P_purge > PPurgeMax)
            {
                violations.Add("P_purge выше нормы");
            }

            if (record.Q_purge < QPurgeMin)
            {
                violations.Add("Q_purge ниже нормы");
            }
            else if (record.Q_purge > QPurgeMax)
            {
                violations.Add("Q_purge выше нормы");
            }

            if (record.T_flame < TFlameMin)
            {
                violations.Add("T_flame ниже нормы");
            }
            else if (record.T_flame > TFlameMax)
            {
                violations.Add("T_flame выше нормы");
            }

            if (IsFlameLiftCondition(record))
            {
                violations.Add("Высокий риск отрыва пламени: P_purge < 0.28 и Q_purge < 22");
            }

            if (IsHlopokCondition(record))
            {
                violations.Add("Риск хлопка: P_flare > 0.022 или hlopok = 1");
            }

            if (record.otriv == 1)
            {
                violations.Add("Авария: отрыв пламени");
            }

            if (record.hlopok == 1)
            {
                violations.Add("Авария: хлопок");
            }

            return violations;
        }

        public static string GetRecommendation(FlareDataRecord record)
        {
            if (record == null)
            {
                return "Параметры в допустимых пределах";
            }

            var recommendations = new List<string>();

            if (record.P_purge < 0.28f)
            {
                recommendations.Add("Увеличить давление продувочного газа");
            }

            if (record.Q_purge < 22f)
            {
                recommendations.Add("Увеличить расход продувочного газа");
            }

            if (record.P_flare > 0.022f)
            {
                recommendations.Add("Снизить давление сбросного газа и проверить вход сепаратора");
            }

            if (record.Q_flare < QFlareMin)
            {
                recommendations.Add("Проверить подачу сбросного газа");
            }

            if (record.T_flame < TFlameMin)
            {
                recommendations.Add("Проверить устойчивость горения и дежурные горелки");
            }

            if (record.T_flame > TFlameMax)
            {
                recommendations.Add("Проверить подачу пара и режим горения");
            }

            if (record.hlopok == 1)
            {
                recommendations.Add("Авария: хлопок. Проверить систему сброса");
            }

            if (record.otriv == 1)
            {
                recommendations.Add("Авария: отрыв пламени. Увеличить продувочный газ");
            }

            return recommendations.Count == 0 ? "Параметры в допустимых пределах" : string.Join("\n", recommendations);
        }

        public static bool IsFlameLiftCondition(FlareDataRecord record)
        {
            return record != null && record.P_purge < FlameLiftPPurgeAlarm && record.Q_purge < FlameLiftQPurgeAlarm;
        }

        public static bool IsHlopokCondition(FlareDataRecord record)
        {
            return record != null && (record.P_flare > HlopokPFlareAlarm || record.hlopok == 1);
        }

        public static Color GetColor(RiskLevel level)
        {
            switch (level)
            {
                case RiskLevel.Warning:
                    return WarningColor;
                case RiskLevel.Danger:
                case RiskLevel.Alarm:
                    return DangerColor;
                default:
                    return NormalColor;
            }
        }

        public static string GetNormalRangeText(string parameterKey)
        {
            switch (parameterKey)
            {
                case "P_flare":
                    return "0.004-0.016 МПа";
                case "Q_flare":
                    return ">= 1.5 м3/ч";
                case "P_purge":
                    return "0.3-0.6 МПа";
                case "Q_purge":
                    return "20-50 м3/ч";
                case "T_flame":
                    return "800-1200 °C";
                case "Steam_Q":
                    return "Технологический расход пара";
                case "Level":
                    return "Нормальный уровень";
                default:
                    return "Не задано";
            }
        }

        public static float GetParameterValue(FlareDataRecord record, string parameterKey)
        {
            if (record == null)
            {
                return 0f;
            }

            switch (parameterKey)
            {
                case "P_flare":
                    return record.P_flare;
                case "Q_flare":
                    return record.Q_flare;
                case "P_purge":
                    return record.P_purge;
                case "Q_purge":
                    return record.Q_purge;
                case "T_flame":
                    return record.T_flame;
                case "Steam_Q":
                    return record.Steam_Q;
                case "Level":
                    return record.hlopok == 1 || record.otriv == 1 ? 1f : 0f;
                default:
                    return 0f;
            }
        }

        public static RiskLevel GetParameterRisk(FlareDataRecord record, string parameterKey)
        {
            if (record == null)
            {
                return RiskLevel.Normal;
            }

            switch (parameterKey)
            {
                case "P_flare":
                    return record.P_flare > HlopokPFlareAlarm ? RiskLevel.Alarm : Outside(record.P_flare, PFlareMin, PFlareMax);
                case "Q_flare":
                    return record.Q_flare < QFlareMin ? RiskLevel.Warning : RiskLevel.Normal;
                case "P_purge":
                    return record.P_purge < FlameLiftPPurgeAlarm ? RiskLevel.Alarm : Outside(record.P_purge, PPurgeMin, PPurgeMax);
                case "Q_purge":
                    return record.Q_purge < FlameLiftQPurgeAlarm ? RiskLevel.Alarm : Outside(record.Q_purge, QPurgeMin, QPurgeMax);
                case "T_flame":
                    return Outside(record.T_flame, TFlameMin, TFlameMax);
                case "Level":
                    return record.hlopok == 1 || record.otriv == 1 ? RiskLevel.Alarm : RiskLevel.Normal;
                default:
                    return RiskLevel.Normal;
            }
        }

        private static RiskLevel Outside(float value, float min, float max)
        {
            return value < min || value > max ? RiskLevel.Warning : RiskLevel.Normal;
        }
    }
}
