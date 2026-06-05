using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace FlareSystem
{
    public class FlareCsvLoader
    {
        public const string DefaultFileName = FieldModelConstants.DefaultCsvFileName;

        private static readonly string[] ExpectedHeader =
        {
            "N",
            "P_flare",
            "Q_flare",
            "P_purge",
            "Q_purge",
            "T_flame",
            "Steam_Q",
            "otriv",
            "hlopok"
        };

        public List<FlareDataRecord> LoadDefaultRecords()
        {
            return LoadRecords(DefaultFileName);
        }

        public List<FlareDataRecord> LoadRecords(string fileName = DefaultFileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = DefaultFileName;
            }

            string path = Path.Combine(Application.streamingAssetsPath, fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning("CSV file not found: " + path + ". Demo flare records will be generated.");
                return CreateDemoRecords();
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (IOException ex)
            {
                Debug.LogError("Failed to read CSV file " + path + ": " + ex.Message);
                return CreateDemoRecords();
            }

            if (lines.Length == 0)
            {
                Debug.LogError("CSV file is empty: " + path);
                return CreateDemoRecords();
            }

            if (!ValidateHeader(lines[0]))
            {
                Debug.LogError("Invalid CSV header in " + path + ". Expected: " + string.Join(";", ExpectedHeader));
            }

            var records = new List<FlareDataRecord>();
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] cells = line.Split(';');
                if (cells.Length < ExpectedHeader.Length)
                {
                    Debug.LogError("CSV row " + (i + 1) + ": expected " + ExpectedHeader.Length + " columns, got " + cells.Length + ". Row skipped.");
                    continue;
                }

                FlareDataRecord record;
                if (TryParseRecord(cells, i + 1, out record))
                {
                    records.Add(record);
                }
            }

            if (records.Count == 0)
            {
                Debug.LogWarning("CSV " + path + " has no valid records. Demo flare records will be generated.");
                return CreateDemoRecords();
            }

            return records;
        }

        private static bool ValidateHeader(string headerLine)
        {
            string[] cells = headerLine.Trim().TrimStart('\uFEFF').Split(';');
            if (cells.Length < ExpectedHeader.Length)
            {
                return false;
            }

            for (int i = 0; i < ExpectedHeader.Length; i++)
            {
                if (!string.Equals(cells[i].Trim(), ExpectedHeader[i], System.StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseRecord(string[] cells, int lineNumber, out FlareDataRecord record)
        {
            record = new FlareDataRecord();

            int n;
            float pFlare;
            float qFlare;
            float pPurge;
            float qPurge;
            float tFlame;
            float steamQ;
            int otriv;
            int hlopok;

            bool ok =
                TryParseInt(cells[0], lineNumber, "N", out n) &&
                TryParseFloat(cells[1], lineNumber, "P_flare", out pFlare) &&
                TryParseFloat(cells[2], lineNumber, "Q_flare", out qFlare) &&
                TryParseFloat(cells[3], lineNumber, "P_purge", out pPurge) &&
                TryParseFloat(cells[4], lineNumber, "Q_purge", out qPurge) &&
                TryParseFloat(cells[5], lineNumber, "T_flame", out tFlame) &&
                TryParseFloat(cells[6], lineNumber, "Steam_Q", out steamQ) &&
                TryParseInt(cells[7], lineNumber, "otriv", out otriv) &&
                TryParseInt(cells[8], lineNumber, "hlopok", out hlopok);

            if (!ok)
            {
                return false;
            }

            record.N = n;
            record.P_flare = pFlare;
            record.Q_flare = qFlare;
            record.P_purge = pPurge;
            record.Q_purge = qPurge;
            record.T_flame = tFlame;
            record.Steam_Q = steamQ;
            record.otriv = otriv;
            record.hlopok = hlopok;
            return true;
        }

        private static bool TryParseFloat(string value, int row, string column, out float parsed)
        {
            if (float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return true;
            }

            Debug.LogError("CSV row " + row + ": value '" + value + "' in column " + column + " is not an invariant-culture float.");
            return false;
        }

        private static bool TryParseInt(string value, int row, string column, out int parsed)
        {
            if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return true;
            }

            Debug.LogError("CSV row " + row + ": value '" + value + "' in column " + column + " is not an int.");
            return false;
        }

        private static List<FlareDataRecord> CreateDemoRecords()
        {
            var records = new List<FlareDataRecord>();
            for (int i = 0; i < 100; i++)
            {
                float t = i / 99f;
                bool alarm = i == 72 || i == 88;

                records.Add(new FlareDataRecord
                {
                    N = i + 1,
                    P_flare = alarm ? 0.026f : Mathf.Lerp(0.008f, 0.018f, Mathf.PingPong(t * 3f, 1f)),
                    Q_flare = Mathf.Lerp(2f, 9f, Mathf.PingPong(t * 2f, 1f)),
                    P_purge = alarm ? 0.2f : Mathf.Lerp(0.32f, 0.52f, Mathf.PingPong(t * 4f, 1f)),
                    Q_purge = alarm ? 10f : Mathf.Lerp(23f, 50f, Mathf.PingPong(t * 5f, 1f)),
                    T_flame = alarm ? 580f : Mathf.Lerp(876f, 1200f, Mathf.PingPong(t * 2.4f, 1f)),
                    Steam_Q = Mathf.Lerp(67f, 110f, Mathf.PingPong(t * 2.2f, 1f)),
                    otriv = alarm ? 1 : 0,
                    hlopok = i == 88 ? 1 : 0
                });
            }

            return records;
        }
    }
}
