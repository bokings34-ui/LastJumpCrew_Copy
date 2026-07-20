using System.Collections.Generic;
using UnityEngine;
using LastJumpCrew.Common;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// Id(int)로 정적 데이터를 저장소.
    /// Item/Event/Zone 모두 "Id로 찾기"가 동일하므로 하나의 제네릭으로 통일한다.
    /// 순수 C# 클래스(MonoBehaviour 아님) — 생성 시 데이터 배열을 받아 Dictionary로 색인한다.
    /// </summary>
    /// <typeparam name="T">ScriptableObject 이면서 IGameData 를 구현한 정적 데이터 타입</typeparam>
    public class DataRepository<T> where T : ScriptableObject, IGameData
    {
        private readonly Dictionary<int, T> table = new();

        public DataRepository(IReadOnlyList<T> source)
        {
            foreach (var d in source)
            {
                if (d == null) continue;
                if (!table.TryAdd(d.Id, d))    // 중복 Id 방어
                    Debug.LogError($"[DataRepository] 중복 Id={d.Id} in {typeof(T).Name}");
            }
        }

        /// <summary>Id로 조회. 실패 시 false.</summary>
        public bool TryGet(int id, out T value) => table.TryGetValue(id, out value);

        /// <summary>Id로 조회. 없으면 null.</summary>
        public T Get(int id) => table.TryGetValue(id, out var v) ? v : null;

        /// <summary>전체 목록(읽기 전용).</summary>
        public IReadOnlyDictionary<int, T> All => table;

        public int Count => table.Count;
    }
}