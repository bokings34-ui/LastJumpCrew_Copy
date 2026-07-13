using System;
using System.Collections.Generic;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 세이브 파일에 직렬화되는 순수 데이터(DTO). JsonUtility 직렬화 대상이라 public 필드로 둔다.
    /// 런타임 로직은 PlayerProfile 이 담당(SRP) — 여기는 데이터만.
    /// </summary>
    [Serializable]
    public class PlayerProfileData
    {
        public int tokens;
        public List<int> ownedCosmetics = new();
    }
}
