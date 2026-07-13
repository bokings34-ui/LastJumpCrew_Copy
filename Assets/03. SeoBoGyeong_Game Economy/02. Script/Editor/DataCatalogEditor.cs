using LastJumpCrew.SeoBoGyeong.Data;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong.EditorTools
{
    /// <summary>
    /// DataCatalog 인스펙터에 "폴더 스캔 → 자동 등록" 버튼을 추가하는 에디터 스크립트.
    /// 프로젝트 전체에서 해당 타입 SO를 찾아 리스트를 다시 채운다(수동 등록 부담 제거).
    /// Editor 폴더 전용 — 빌드에는 포함되지 않는다.
    /// </summary>
    [CustomEditor(typeof(DataCatalog))]
    public class DataCatalogEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("폴더 스캔 → 카탈로그 자동 등록"))
            {
                FillCatalog();
            }
        }

        private void FillCatalog()
        {
            serializedObject.Update();

            // t:ItemData 검색은 상속 타입(UtilityItemData)도 함께 찾는다.
            int itemCount = FillList<ItemData>(serializedObject.FindProperty("items"));
            int zoneCount = FillList<ZoneData>(serializedObject.FindProperty("zones"));

            serializedObject.ApplyModifiedProperties();
            Debug.Log($"[DataCatalog] 자동 등록 완료 — Items {itemCount}개 / Zones {zoneCount}개", target);
        }

        /// <summary>프로젝트에서 T 타입 SO를 모두 찾아 리스트 프로퍼티를 다시 채운다. 등록 수를 반환.</summary>
        private static int FillList<T>(SerializedProperty listProp) where T : ScriptableObject
        {
            listProp.ClearArray();

            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null) continue;

                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = asset;
            }
            return listProp.arraySize;
        }
    }
}
