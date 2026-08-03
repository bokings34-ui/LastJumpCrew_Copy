using LastJumpCrew.ParkHanSol.Multiplayer.Customization;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    [CustomEditor(typeof(PHS_CosmeticItemTuningPreview))]
    public sealed class PHS_CosmeticItemTuningPreviewEditor : UnityEditor.Editor
    {
        private SerializedProperty item;
        private SerializedProperty catalog;

        private void OnEnable()
        {
            catalog = serializedObject.FindProperty("catalog");
            item = serializedObject.FindProperty("item");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var cosmeticCatalog = catalog.objectReferenceValue as CosmeticCatalog;
            if (cosmeticCatalog == null)
            {
                EditorGUILayout.HelpBox("Cosmetic Catalog 참조가 없습니다.", MessageType.Error);
                return;
            }

            var items = cosmeticCatalog.Items;
            var current = item.objectReferenceValue as CosmeticItemData;
            var currentIndex = -1;
            var labels = new string[items.Count];
            for (var index = 0; index < items.Count; index++)
            {
                labels[index] = $"{items[index].Slot} / {items[index].DisplayName}";
                if (items[index] == current) currentIndex = index;
            }

            EditorGUI.BeginChangeCheck();
            var selectedIndex = EditorGUILayout.Popup("교체 아이템", currentIndex, labels);
            if (EditorGUI.EndChangeCheck())
            {
                item.objectReferenceValue = selectedIndex >= 0 ? items[selectedIndex] : null;
            }
            serializedObject.ApplyModifiedProperties();

            current = item.objectReferenceValue as CosmeticItemData;
            if (current == null)
            {
                EditorGUILayout.HelpBox("조절할 아이템을 선택하세요.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("실제 장착값", EditorStyles.boldLabel);
            var itemData = new SerializedObject(current);
            itemData.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(itemData.FindProperty("localPosition"), new GUIContent("Local Position"));
            EditorGUILayout.PropertyField(itemData.FindProperty("localEulerAngles"), new GUIContent("Local Euler Angles"));
            EditorGUILayout.PropertyField(itemData.FindProperty("localScale"), new GUIContent("Local Scale"));
            if (EditorGUI.EndChangeCheck())
            {
                itemData.ApplyModifiedProperties();
                EditorUtility.SetDirty(current);
                SceneView.RepaintAll();
            }

            EditorGUILayout.HelpBox("여기 값을 바꾸면 실제 플레이어와 게임 장착값에 같이 적용됩니다.", MessageType.None);
        }

    }
}
