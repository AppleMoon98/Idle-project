using UnityEditor;

namespace Editor
{
    /// <summary>
    /// 이 프로젝트의 모든 카탈로그(SO 배열 하나 든 컨테이너) 형태 데이터를 범용으로 편집하는
    /// 도구. 항목 이름/아이콘/수치 등 어떤 필드든 SerializedObject+PropertyField로 그대로
    /// 노출되므로 새 필드가 추가돼도 이 도구를 고칠 필요가 없다. Stage(320개)는 별도의
    /// 일회성 생성 스크립트로 만들어진 콘텐츠라 이 도구의 대상에서 제외했다.
    /// </summary>
    public sealed class ContentEditorWindow : EditorWindow
    {
        private CatalogEditorPanel _catalogPanel;

        [MenuItem("Tools/Idle Project/Content Editor")]
        private static void Open()
        {
            GetWindow<ContentEditorWindow>("Content Editor");
        }

        private void OnEnable()
        {
            BuildCatalogPanel();
        }

        private void BuildCatalogPanel()
        {
            _catalogPanel = new CatalogEditorPanel(new[]
            {
                new CatalogRegistration("Equipment", "Assets/03. SO/Items/EquipmentCatalog.asset", "items", typeof(Equipment.EquipmentSO)),
                new CatalogRegistration("Soldier", "Assets/03. SO/Soldiers/SoldierCatalog.asset", "soldiers", typeof(Soldier.SoldierSO)),
                new CatalogRegistration("Skill", "Assets/03. SO/Skills/SkillCatalog.asset", "skills", typeof(Skill.SkillSO)),
                new CatalogRegistration("Rank", "Assets/03. SO/Ranks/RankCatalog.asset", "ranks", typeof(Rank.RankSO)),
                new CatalogRegistration("Equipment Grade", "Assets/03. SO/Items/Grades/EquipmentGradeCatalog.asset", "grades", typeof(Equipment.EquipmentGradeSO)),
                new CatalogRegistration("Behavior Profile", "Assets/03. SO/Behavior/BehaviorProfileCatalog.asset", "profiles", typeof(Behavior.BehaviorProfileSO)),
                new CatalogRegistration("Monster Visual Set", "Assets/03. SO/Monsters/MonsterVisualSetCatalog.asset", "sets", typeof(Character.MonsterVisualSetSO)),
            });
        }

        private void OnGUI()
        {
            _catalogPanel.OnGUI();
        }
    }
}
