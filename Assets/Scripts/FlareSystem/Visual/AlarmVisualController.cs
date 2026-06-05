using UnityEngine;
using UnityEngine.UI;

namespace FlareSystem
{
    public class AlarmVisualController : MonoBehaviour
    {
        public Light alarmLight;
        public AudioSource audioSource;
        public Image uiAlarmImage;
        public Component alarmText;
        public string defaultAlarmMessage = "АВАРИЯ: высокий риск отрыва пламени";

        private bool alarmActive;
        private string currentMessage;

        private void Awake()
        {
            EnsureAudioSource();
            ResetAlarm();
        }

        private void Update()
        {
            if (!alarmActive)
            {
                return;
            }

            float pulse = 0.45f + Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 0.55f;
            if (alarmLight != null)
            {
                alarmLight.enabled = true;
                alarmLight.color = FlareConstants.DangerColor;
                alarmLight.intensity = Mathf.Lerp(0.5f, 5f, pulse);
            }

            if (uiAlarmImage != null)
            {
                Color color = FlareConstants.DangerColor;
                color.a = Mathf.Lerp(0.25f, 0.95f, pulse);
                uiAlarmImage.color = color;
            }
        }

        public void SetAlarm(bool active, string message = null)
        {
            alarmActive = active;
            currentMessage = string.IsNullOrWhiteSpace(message) ? defaultAlarmMessage : message;

            FlareUIController.SetText(alarmText, active ? currentMessage : string.Empty);

            if (alarmLight != null)
            {
                alarmLight.enabled = active;
            }

            if (uiAlarmImage != null)
            {
                uiAlarmImage.enabled = active;
            }

            EnsureAudioSource();
            if (audioSource != null && audioSource.clip != null)
            {
                if (active && !audioSource.isPlaying)
                {
                    audioSource.Play();
                }
                else if (!active && audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
            }
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.35f;

            if (audioSource.clip == null)
            {
                audioSource.clip = CreateGeneratedAlarmClip();
            }
        }

        private static AudioClip CreateGeneratedAlarmClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.5f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float gate = Mathf.Repeat(t, 0.25f) < 0.14f ? 1f : 0.2f;
                samples[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.28f * gate;
            }

            AudioClip clip = AudioClip.Create("GeneratedAlarmTone", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void ResetAlarm()
        {
            alarmActive = false;
            if (alarmLight != null)
            {
                alarmLight.enabled = false;
            }

            if (uiAlarmImage != null)
            {
                uiAlarmImage.enabled = false;
            }

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            FlareUIController.SetText(alarmText, string.Empty);
        }
    }
}
