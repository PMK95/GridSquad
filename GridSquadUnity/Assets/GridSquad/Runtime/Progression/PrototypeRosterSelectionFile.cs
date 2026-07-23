using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace GridSquad
{
    [Serializable]
    public sealed class PrototypeRosterSelectionSaveData
    {
        public int Version = 1;
        public List<string> SelectedUnitIds = new();
    }

    public static class PrototypeRosterSelectionFile
    {
        private const string FileName = "prototype-roster-selection.json";
        private static readonly UTF8Encoding Utf8WithoutBom = new(false);

        public static string FilePath =>
            Path.Combine(Application.persistentDataPath, FileName);

        public static IReadOnlyList<string> Load()
        {
            string path = FilePath;
            if (!File.Exists(path))
                return Array.Empty<string>();

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                PrototypeRosterSelectionSaveData data =
                    JsonUtility.FromJson<PrototypeRosterSelectionSaveData>(json);
                return data?.SelectedUnitIds != null
                    ? data.SelectedUnitIds
                    : Array.Empty<string>();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[로스터 저장] 마지막 선발 정보를 읽지 못했습니다. 새 선택으로 시작합니다.\n{exception.Message}");
                return Array.Empty<string>();
            }
        }

        public static void Save(IReadOnlyList<string> selectedUnitIds)
        {
            PrototypeRosterSelectionSaveData data = new();
            HashSet<string> uniqueIds = new(StringComparer.Ordinal);
            if (selectedUnitIds != null)
            {
                foreach (string unitId in selectedUnitIds)
                {
                    if (!string.IsNullOrWhiteSpace(unitId) && uniqueIds.Add(unitId))
                        data.SelectedUnitIds.Add(unitId);
                }
            }

            string path = FilePath;
            string temporaryPath = path + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(
                    temporaryPath,
                    JsonUtility.ToJson(data, true),
                    Utf8WithoutBom);
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[로스터 저장] 마지막 선발 정보를 저장하지 못했습니다.\n{exception.Message}");
                TryDeleteTemporaryFile(temporaryPath);
            }
        }

        private static void TryDeleteTemporaryFile(string temporaryPath)
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (Exception)
            {
                // 임시 파일 정리 실패는 다음 저장 시 덮어쓸 수 있으므로 별도 오류로 처리하지 않는다.
            }
        }
    }
}
