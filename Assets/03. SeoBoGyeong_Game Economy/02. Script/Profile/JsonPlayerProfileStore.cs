using System;
using System.IO;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// IPlayerProfileStore 의 1차 구현 — Application.persistentDataPath 에 JSON 저장/로드.
    /// 세이브가 없거나 손상됐으면 새 기본값으로 시작한다(예외로 게임을 막지 않음).
    /// </summary>
    public sealed class JsonPlayerProfileStore : IPlayerProfileStore
    {
        private const string FILE_NAME = "player_profile.json";

        private static string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

        public PlayerProfileData Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new PlayerProfileData();

                string json = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<PlayerProfileData>(json);
                return data ?? new PlayerProfileData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ProfileStore] 세이브 로드 실패 — 새 프로필로 시작합니다: {e.Message}");
                return new PlayerProfileData();
            }
        }

        public void Save(PlayerProfileData data)
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfileStore] 세이브 저장 실패: {e.Message}");
            }
        }
    }
}
