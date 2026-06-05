using System.Collections.Generic;
using UnityEngine;

namespace FlareSystem
{
    public enum FlareMode
    {
        Archive,
        EarlyWarning
    }

    public class FlareInstallationController : MonoBehaviour
    {
        [Header("Controllers")]
        public ArchiveModeController archiveModeController;
        public EarlyWarningController earlyWarningController;
        public LogisticRegressionRiskModel riskModel;
        public FlameController flameController;
        public PipeFlowController pipeFlowController;
        public ValveAnimator valveAnimator;
        public AlarmVisualController alarmVisualController;
        public FlareUIController uiController;
        public RiskPanelController riskPanelController;

        [Header("Scene Indicators")]
        public List<SensorIndicator> sensorIndicators = new List<SensorIndicator>();

        public FlareMode CurrentMode { get; private set; } = FlareMode.Archive;
        public FlareDataRecord CurrentRecord { get; private set; }
        public FlareSystemState CurrentState { get; private set; }
        public float CurrentProbability { get; private set; }

        private void Awake()
        {
            EnsureReferences();
        }

        private void Start()
        {
            SetArchiveMode();
        }

        public void EnsureReferences()
        {
            if (archiveModeController == null)
            {
                archiveModeController = GetComponent<ArchiveModeController>();
            }

            if (earlyWarningController == null)
            {
                earlyWarningController = GetComponent<EarlyWarningController>();
            }

            if (riskModel == null)
            {
                riskModel = GetComponent<LogisticRegressionRiskModel>();
            }

            if (flameController == null)
            {
                flameController = GetComponentInChildren<FlameController>(true);
            }

            if (pipeFlowController == null)
            {
                pipeFlowController = GetComponentInChildren<PipeFlowController>(true);
            }

            if (valveAnimator == null)
            {
                valveAnimator = GetComponentInChildren<ValveAnimator>(true);
            }

            if (alarmVisualController == null)
            {
                alarmVisualController = GetComponentInChildren<AlarmVisualController>(true);
            }

            if (uiController == null)
            {
#if UNITY_2023_1_OR_NEWER
                uiController = Object.FindAnyObjectByType<FlareUIController>(FindObjectsInactive.Include);
#else
                uiController = Object.FindObjectOfType<FlareUIController>(true);
#endif
            }

            if (riskPanelController == null)
            {
#if UNITY_2023_1_OR_NEWER
                riskPanelController = Object.FindAnyObjectByType<RiskPanelController>(FindObjectsInactive.Include);
#else
                riskPanelController = Object.FindObjectOfType<RiskPanelController>(true);
#endif
            }

            if (sensorIndicators.Count == 0)
            {
#if UNITY_2023_1_OR_NEWER
                sensorIndicators.AddRange(Object.FindObjectsByType<SensorIndicator>(FindObjectsInactive.Include));
#else
                sensorIndicators.AddRange(Object.FindObjectsOfType<SensorIndicator>(true));
#endif
            }

            if (archiveModeController != null)
            {
                archiveModeController.controller = this;
            }

            if (earlyWarningController != null)
            {
                earlyWarningController.controller = this;
                earlyWarningController.riskModel = riskModel;
            }
        }

        public void SetArchiveMode()
        {
            EnsureReferences();
            CurrentMode = FlareMode.Archive;

            if (uiController != null)
            {
                uiController.ShowMode(FlareMode.Archive);
            }

            if (archiveModeController != null)
            {
                if (archiveModeController.Records.Count == 0)
                {
                    archiveModeController.LoadRecords();
                }
                else
                {
                    archiveModeController.SetRecordIndex(archiveModeController.CurrentIndex);
                }
            }
        }

        public void SetEarlyWarningMode()
        {
            EnsureReferences();
            CurrentMode = FlareMode.EarlyWarning;

            if (archiveModeController != null)
            {
                archiveModeController.Pause();
            }

            if (uiController != null)
            {
                uiController.ShowMode(FlareMode.EarlyWarning);
            }

            if (earlyWarningController != null)
            {
                earlyWarningController.ConfigureSliders();
                earlyWarningController.Recalculate();
            }
        }

        public void ApplyRecord(FlareDataRecord record)
        {
            EnsureReferences();
            CurrentRecord = record;
            CurrentProbability = riskModel != null ? riskModel.CalculateProbability(record.P_purge, record.Q_purge, record.P_flare) : 0f;
            CurrentState = FlareConstants.EvaluateRecord(record, CurrentMode, CurrentProbability);

            ApplyStateToVisuals(CurrentState);

            if (uiController != null && archiveModeController != null)
            {
                uiController.UpdateArchive(record, CurrentState, archiveModeController.CurrentIndex, archiveModeController.Records.Count, archiveModeController.IsPlaying);
            }

            UpdateRiskPanel(false);
        }

        public void ApplyRisk(float probability)
        {
            EnsureReferences();
            CurrentProbability = probability;

            FlareDataRecord synthetic = earlyWarningController != null
                ? earlyWarningController.BuildSyntheticRecord()
                : CurrentRecord;

            CurrentRecord = synthetic;
            CurrentState = FlareConstants.EvaluateRecord(synthetic, CurrentMode, probability);
            if (earlyWarningController != null)
            {
                CurrentState.Recommendation = earlyWarningController.CurrentRecommendation;
            }

            ApplyStateToVisuals(CurrentState);

            if (uiController != null && earlyWarningController != null)
            {
                uiController.UpdateEarlyWarning(
                    earlyWarningController.currentPPurge,
                    earlyWarningController.currentQPurge,
                    earlyWarningController.currentPFlare,
                    probability,
                    CurrentState.RiskLevel,
                    CurrentState.Recommendation);
            }

            UpdateRiskPanel(false);
        }

        public void ResetAlarm()
        {
            if (alarmVisualController != null)
            {
                alarmVisualController.ResetAlarm();
            }

            if (flameController != null)
            {
                flameController.SetNormal();
            }
        }

        public void OpenRiskPanel()
        {
            UpdateRiskPanel(true);
        }

        private void ApplyStateToVisuals(FlareSystemState state)
        {
            if (state == null || state.Record == null)
            {
                return;
            }

            if (flameController != null)
            {
                flameController.SetByRiskLevel(state.RiskLevel);
            }

            if (pipeFlowController != null)
            {
                pipeFlowController.StartFlow();
                float intensity = Mathf.Clamp01((state.Record.Q_flare + state.Record.Q_purge * 0.08f + state.Record.Steam_Q * 0.04f) / 10f);
                pipeFlowController.SetIntensity(Mathf.Lerp(0.4f, 1.8f, intensity));
                pipeFlowController.SetPathIntensity("ReliefGas", Mathf.Max(4f, state.Record.Q_flare * 6f));
                pipeFlowController.SetPathIntensity("PurgeGas", Mathf.Max(4f, state.Record.Q_purge * 0.8f));
                pipeFlowController.SetPathIntensity("Steam", Mathf.Max(2f, state.Record.Steam_Q * 0.7f));
            }

            foreach (SensorIndicator indicator in sensorIndicators)
            {
                if (indicator != null)
                {
                    indicator.UpdateFromRecord(state.Record);
                }
            }

            if (valveAnimator != null)
            {
                valveAnimator.SetSpeed(state.IsAlarm ? 240f : 120f);
                valveAnimator.RotateOnce(state.IsAlarm ? 120f : 45f);
            }

            if (alarmVisualController != null)
            {
                alarmVisualController.SetAlarm(state.IsAlarm, state.IsAlarm ? "АВАРИЯ: " + state.ViolationsText.Replace("\n", "; ") : string.Empty);
            }

            if (uiController != null)
            {
                uiController.UpdateStatus(state);
            }
        }

        private void UpdateRiskPanel(bool open)
        {
            if (riskPanelController == null || CurrentState == null)
            {
                return;
            }

            string flameState = flameController != null ? flameController.CurrentFlameState : "Не задано";
            riskPanelController.UpdatePanel(CurrentMode, CurrentRecord, CurrentProbability, CurrentState.RiskLevel, CurrentState.Recommendation, flameState);

            if (open)
            {
                riskPanelController.Open();
            }
        }
    }
}
