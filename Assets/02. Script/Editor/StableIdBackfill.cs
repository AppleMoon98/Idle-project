using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// EquipmentSO/SoldierSO/SkillSO/BehaviorProfileSO에 새로 추가된 StableId 필드를 프로젝트의
    /// 기존 자산 전체에 일괄 발급하는 1회성 도구(GitHub 이슈 #19). StableId가 비어있는 자산만
    /// 새 GUID를 발급하고, 이미 값이 있는 자산은 절대 덮어쓰지 않는다 - 몇 번을 실행해도 안전하다
    /// (idempotent). 콘텐츠 생성 스크립트(section V/CU 등)와 같은 성격의 1회성 Editor 도구다.
    /// </summary>
    internal static class StableIdBackfill
    {
        [MenuItem("Idle Project/Backfill Stable IDs (Equipment-Soldier-Skill-BehaviorProfile)")]
        private static void RunAll()
        {
            int total = 0;
            total += Backfill<Equipment.EquipmentSO>();
            total += Backfill<Soldier.SoldierSO>();
            total += Backfill<Skill.SkillSO>();
            total += Backfill<Behavior.BehaviorProfileSO>();

            AssetDatabase.SaveAssets();
            Debug.Log($"[StableIdBackfill] {total}개 자산에 새 StableId를 발급했습니다.");
        }

        private static int Backfill<T>() where T : ScriptableObject
        {
            int count = 0;
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);

                if (asset == null)
                {
                    continue;
                }

                var serializedObject = new SerializedObject(asset);
                SerializedProperty stableIdProperty = serializedObject.FindProperty("stableId");

                if (stableIdProperty == null || !string.IsNullOrEmpty(stableIdProperty.stringValue))
                {
                    continue;
                }

                stableIdProperty.stringValue = System.Guid.NewGuid().ToString("N");
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
                count++;
            }

            return count;
        }
    }
}
