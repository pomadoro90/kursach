using UnityEngine;

namespace FlareSystem
{
    public class FieldFlameController : FlameController
    {
        [Header("Field Flame")]
        public FieldCoordinateMode coordinateMode = FieldCoordinateMode.XyzAsUnity;
        public bool positionAtFieldTipOnAwake = true;
        public float minTemperature = 580f;
        public float maxTemperature = 1250f;

        private void Awake()
        {
            EnsureFieldPlacement();
            EnsureEffect();
            SetNormal();
        }

        public void EnsureFieldPlacement()
        {
            if (!positionAtFieldTipOnAwake)
            {
                return;
            }

            transform.position = ConvertPoint(FieldModelConstants.FlareTipPosition);
            if (flameRoot != null)
            {
                flameRoot.localPosition = Vector3.zero;
            }
        }

        public void ApplyRecord(FlareDataRecord record)
        {
            if (record == null)
            {
                SetNormal();
                return;
            }

            if (record.otriv == 1 || record.hlopok == 1 || FlareConstants.IsHlopokCondition(record))
            {
                SetAlarm();
            }
            else if (FlareConstants.IsFlameLiftCondition(record) || record.T_flame < FlareConstants.TFlameMin)
            {
                SetDanger();
            }
            else if (record.T_flame > FlareConstants.TFlameMax)
            {
                SetWarning();
            }
            else
            {
                SetNormal();
            }

            ApplyTemperature(record.T_flame);
        }

        private void ApplyTemperature(float temperature)
        {
            if (flameParticles == null)
            {
                return;
            }

            float t = Mathf.InverseLerp(minTemperature, maxTemperature, temperature);
            var main = flameParticles.main;
            main.startSpeed = Mathf.Lerp(1.1f, 2.7f, t);

            if (flameLight != null)
            {
                flameLight.range = Mathf.Lerp(10f, 20f, t);
            }
        }

        private Vector3 ConvertPoint(Vector3 point)
        {
            return coordinateMode == FieldCoordinateMode.XyzToUnityYUp
                ? FieldModelConstants.XyzToUnityYUp(point)
                : point;
        }
    }
}
