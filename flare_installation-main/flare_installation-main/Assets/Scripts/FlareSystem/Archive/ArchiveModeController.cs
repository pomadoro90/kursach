using System.Collections.Generic;
using UnityEngine;

namespace FlareSystem
{
    public class ArchiveModeController : MonoBehaviour
    {
        public FlareInstallationController controller;
        public string csvFileName = "variant_3_15.csv";
        public float playbackInterval = 1.25f;
        public bool loadOnStart = true;

        public List<FlareDataRecord> Records { get; private set; } = new List<FlareDataRecord>();
        public int CurrentIndex { get; private set; }
        public bool IsPlaying { get; private set; }

        private float timer;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<FlareInstallationController>();
            }
        }

        private void Start()
        {
            if (loadOnStart)
            {
                LoadRecords();
            }
        }

        private void Update()
        {
            if (!IsPlaying || Records.Count == 0)
            {
                return;
            }

            timer += Time.deltaTime;
            if (timer >= playbackInterval)
            {
                timer = 0f;
                NextRecord();
            }
        }

        public void LoadRecords()
        {
            Records = new FlareCsvLoader().LoadRecords(csvFileName);
            CurrentIndex = Mathf.Clamp(CurrentIndex, 0, Mathf.Max(0, Records.Count - 1));

            if (controller != null && Records.Count > 0)
            {
                controller.ApplyRecord(Records[CurrentIndex]);
            }
        }

        public void NextRecord()
        {
            if (Records.Count == 0)
            {
                LoadRecords();
            }

            if (Records.Count == 0)
            {
                return;
            }

            SetRecordIndex((CurrentIndex + 1) % Records.Count);
        }

        public void PreviousRecord()
        {
            if (Records.Count == 0)
            {
                LoadRecords();
            }

            if (Records.Count == 0)
            {
                return;
            }

            SetRecordIndex((CurrentIndex - 1 + Records.Count) % Records.Count);
        }

        public void SetRecordIndex(float index)
        {
            SetRecordIndex(Mathf.RoundToInt(index));
        }

        public void SetRecordIndex(int index)
        {
            if (Records.Count == 0)
            {
                LoadRecords();
            }

            if (Records.Count == 0)
            {
                return;
            }

            CurrentIndex = Mathf.Clamp(index, 0, Records.Count - 1);
            timer = 0f;

            if (controller != null)
            {
                controller.ApplyRecord(Records[CurrentIndex]);
            }
        }

        public void Play()
        {
            if (Records.Count == 0)
            {
                LoadRecords();
            }

            IsPlaying = true;
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void TogglePlayPause()
        {
            if (IsPlaying)
            {
                Pause();
            }
            else
            {
                Play();
            }

            if (controller != null && controller.uiController != null)
            {
                controller.uiController.UpdateArchive(controller.CurrentRecord, controller.CurrentState, CurrentIndex, Records.Count, IsPlaying);
            }
        }

        public void Stop()
        {
            Pause();
            SetRecordIndex(0);
        }
    }
}
