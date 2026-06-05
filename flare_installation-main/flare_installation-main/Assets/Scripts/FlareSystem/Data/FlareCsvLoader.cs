using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace FlareSystem
{
    public class FlareCsvLoader
    {
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

        public List<FlareDataRecord> LoadRecords(string fileName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"CSV файл не найден: {path}. Будут созданы демонстрационные записи.");
                return CreateDemoRecords();
            }

            var records = new List<FlareDataRecord>();
            string[] lines;

            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (IOException ex)
            {
                Debug.LogError($"Не удалось прочитать CSV файл {path}: {ex.Message}");
                return CreateDemoRecords();
            }

            if (lines.Length == 0)
            {
                Debug.LogError($"CSV файл пустой: {path}");
                return CreateDemoRecords();
            }

            if (!ValidateHeader(lines[0]))
            {
                Debug.LogError($"Некорректный заголовок CSV {path}. Ожидается: {string.Join(";", ExpectedHeader)}");
            }

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
                    Debug.LogError($"CSV строка {i + 1}: ожидалось {ExpectedHeader.Length} колонок, получено {cells.Length}. Строка пропущена.");
                    continue;
                }

                if (TryParseRecord(cells, i + 1, out FlareDataRecord record))
                {
                    records.Add(record);
                }
            }

            if (records.Count == 0)
            {
                Debug.LogWarning($"CSV {path} не содержит валидных записей. Будут созданы демонстрационные записи.");
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
            var culture = CultureInfo.InvariantCulture;

            bool ok =
                TryParseInt(cells[0], lineNumber, "N", out record.N) &&
                TryParseFloat(cells[1], lineNumber, "P_flare", out record.P_flare) &&
                TryParseFloat(cells[2], lineNumber, "Q_flare", out record.Q_flare) &&
                TryParseFloat(cells[3], lineNumber, "P_purge", out record.P_purge) &&
                TryParseFloat(cells[4], lineNumber, "Q_purge", out record.Q_purge) &&
                TryParseFloat(cells[5], lineNumber, "T_flame", out record.T_flame) &&
                TryParseFloat(cells[6], lineNumber, "Steam_Q", out record.Steam_Q) &&
                TryParseInt(cells[7], lineNumber, "otriv", out record.otriv) &&
                TryParseInt(cells[8], lineNumber, "hlopok", out record.hlopok);

            return ok;

            bool TryParseFloat(string value, int row, string column, out float parsed)
            {
                if (float.TryParse(value.Trim(), NumberStyles.Float, culture, out parsed))
                {
                    return true;
                }

                Debug.LogError($"CSV строка {row}: значение '{value}' в колонке {column} не является float с точкой.");
                return false;
            }

            bool TryParseInt(string value, int row, string column, out int parsed)
            {
                if (int.TryParse(value.Trim(), NumberStyles.Integer, culture, out parsed))
                {
                    return true;
                }

                Debug.LogError($"CSV строка {row}: значение '{value}' в колонке {column} не является int.");
                return false;
            }
        }

        private static List<FlareDataRecord> CreateDemoRecords()
        {
            var records = new List<FlareDataRecord>();

            for (int i = 0; i < 100; i++)
            {
                float t = i / 99f;
                bool warning = i % 17 == 0;
                bool alarm = i == 72 || i == 88;

                records.Add(new FlareDataRecord
                {
                    N = i + 1,
                    P_flare = alarm ? 0.024f : Mathf.Lerp(0.006f, 0.016f, Mathf.PingPong(t * 3f, 1f)),
                    Q_flare = warning ? 1.2f : Mathf.Lerp(1.8f, 4.4f, Mathf.PingPong(t * 2f, 1f)),
                    P_purge = alarm ? 0.22f : (warning ? 0.29f : Mathf.Lerp(0.34f, 0.52f, Mathf.PingPong(t * 4f, 1f))),
                    Q_purge = alarm ? 17f : (warning ? 21f : Mathf.Lerp(26f, 44f, Mathf.PingPong(t * 5f, 1f))),
                    T_flame = warning ? 760f : Mathf.Lerp(880f, 1120f, Mathf.PingPong(t * 2.4f, 1f)),
                    Steam_Q = Mathf.Lerp(80f, 132f, Mathf.PingPong(t * 2.2f, 1f)),
                    otriv = alarm ? 1 : 0,
                    hlopok = i == 88 ? 1 : 0
                });
            }

            return records;
        }
    }
}
