using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// 두 개의 탭으로 이루어진 컨텐츠 편집 도구. "World Sprites" 탭은 Player/Monster/Skill 등
    /// 아직 placeholder 스프라이트를 쓰는 고정 대상들을 드래그-적용으로 실제 프리팹/씬
    /// 오브젝트/SO에 바로 반영한다. "Catalog Editor" 탭은 이 프로젝트의 모든 카탈로그(SO 배열
    /// 하나 든 컨테이너) 형태 데이터를 범용으로 편집한다 — 항목 이름/아이콘/수치 등 어떤 필드든
    /// SerializedObject+PropertyField로 그대로 노출되므로 새 필드가 추가돼도 이 도구를 고칠
    /// 필요가 없다. Equipment(150개)는 항목 수가 많아 World Sprites 대신 Catalog Editor의
    /// 검색 가능한 리스트로 다룬다. Stage(320개)는 별도의 일회성 생성 스크립트로 만들어진
    /// 콘텐츠라 이 도구의 대상에서 제외했다. Animator Controller 연결은 프로젝트에 Animator를
    /// 쓰는 곳이 아직 없어 이번 버전에서는 다루지 않는다.
    /// </summary>
    public sealed class ContentEditorWindow : EditorWindow
    {
        private enum Tab
        {
            WorldSprites,
            CatalogEditor
        }

        private static readonly string[] TabLabels = { "World Sprites", "Catalog Editor" };

        private Tab _tab;
        private List<ArtAssignmentTarget> _targets;
        private readonly Dictionary<ArtAssignmentTarget, Sprite> _pending = new();
        private Vector2 _scroll;
        private CatalogEditorPanel _catalogPanel;

        [MenuItem("Tools/Idle Project/Content Editor")]
        private static void Open()
        {
            GetWindow<ContentEditorWindow>("Content Editor");
        }

        private void OnEnable()
        {
            BuildTargets();
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
                new CatalogRegistration("Soldier Equipment", "Assets/03. SO/SoldierEquipment/SoldierEquipmentCatalog.asset", "items", typeof(SoldierEquipment.SoldierEquipmentSO)),
                new CatalogRegistration("Behavior Profile", "Assets/03. SO/Behavior/BehaviorProfileCatalog.asset", "profiles", typeof(Behavior.BehaviorProfileSO)),
            });
        }

        private void BuildTargets()
        {
            _targets = new List<ArtAssignmentTarget>
            {
                new SceneSpriteTarget("Player", "Player"),

                new PrefabSpriteTarget("Monster (일반)", "Assets/04. Prefab/Monster.prefab"),
                new PrefabSpriteTarget("Monster (엘리트)", "Assets/04. Prefab/Monster_Elite.prefab"),
                new PrefabSpriteTarget("Monster (보스)", "Assets/04. Prefab/Monster_Boss.prefab"),
                new PrefabSpriteTarget("Monster (골드 던전)", "Assets/04. Prefab/Monster_GoldDungeon.prefab"),
                new PrefabSpriteTarget("War Boss", "Assets/04. Prefab/WarBoss.prefab"),
                new PrefabSpriteTarget("Soldier (근접)", "Assets/04. Prefab/Soldier.prefab"),
                new PrefabSpriteTarget("Soldier (원거리)", "Assets/04. Prefab/Soldier_Ranged.prefab"),
                new PrefabSpriteTarget("Cargo", "Assets/04. Prefab/Cargo.prefab"),
                new PrefabSpriteTarget("Projectile", "Assets/04. Prefab/Projectile.prefab"),

                new ScriptableObjectIconTarget("Skill: 회오리베기", "Assets/03. SO/Skills/Skill_WhirlwindSlash.asset"),
                new ScriptableObjectIconTarget("Skill: 전투의 함성", "Assets/03. SO/Skills/Skill_BattleCry.asset"),
                new ScriptableObjectIconTarget("Skill: 강타", "Assets/03. SO/Skills/Skill_PowerStrike.asset"),

                new ScriptableObjectIconTarget("Soldier SO: 근접", "Assets/03. SO/Soldiers/Soldier_Melee.asset"),
                new ScriptableObjectIconTarget("Soldier SO: 원거리", "Assets/03. SO/Soldiers/Soldier_Ranged.asset"),
            };
        }

        private void OnGUI()
        {
            _tab = (Tab)GUILayout.Toolbar((int)_tab, TabLabels);

            EditorGUILayout.Space(6);

            if (_tab == Tab.WorldSprites)
            {
                DrawWorldSpritesTab();
            }
            else
            {
                _catalogPanel.OnGUI();
            }
        }

        private void DrawWorldSpritesTab()
        {
            if (GUILayout.Button("새로고침"))
            {
                BuildTargets();
                _pending.Clear();
            }

            EditorGUILayout.Space(8);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (ArtAssignmentTarget target in _targets)
            {
                DrawRow(target);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawRow(ArtAssignmentTarget target)
        {
            EditorGUILayout.BeginHorizontal("box");

            EditorGUILayout.LabelField(target.Label, GUILayout.Width(160));

            Sprite current = target.CurrentSprite;
            DrawPreview(current);

            if (!_pending.TryGetValue(target, out Sprite pending))
            {
                pending = current;
            }

            Sprite newSprite = (Sprite)EditorGUILayout.ObjectField(pending, typeof(Sprite), false, GUILayout.Width(180));

            if (newSprite != pending)
            {
                _pending[target] = newSprite;
            }

            GUI.enabled = newSprite != current;

            if (GUILayout.Button("적용", GUILayout.Width(60)))
            {
                if (target.Apply(newSprite))
                {
                    _pending.Remove(target);
                }
                else
                {
                    Debug.LogWarning($"[ContentEditorWindow] '{target.Label}' 적용 실패 — 대상을 찾을 수 없습니다.");
                }
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreview(Sprite sprite)
        {
            Texture preview = sprite != null ? AssetPreview.GetAssetPreview(sprite) : null;

            if (sprite != null && preview == null)
            {
                Repaint();
            }

            GUILayout.Box(preview != null ? preview : Texture2D.grayTexture, GUILayout.Width(48), GUILayout.Height(48));
        }
    }
}
