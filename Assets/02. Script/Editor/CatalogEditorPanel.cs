using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// 카탈로그(SO 배열 하나 든 컨테이너) 기반 범용 편집 패널. 카탈로그를 고르면 그 안의 모든
    /// 항목을 나열하고, 항목을 고르면 SerializedObject + PropertyField로 그 SO의 모든 필드를
    /// 그대로 그려서 편집한다 — 새 필드가 SO에 추가돼도 이 코드를 고칠 필요가 없다. "새 항목
    /// 추가"는 카탈로그 에셋과 같은 폴더에 새 에셋을 만들고 카탈로그 배열 끝에 등록한다.
    /// </summary>
    public sealed class CatalogEditorPanel
    {
        private readonly CatalogRegistration[] _catalogs;

        private int _selectedCatalogIndex;
        private ScriptableObject _catalogAsset;
        private SerializedObject _catalogSerialized;
        private SerializedProperty _arrayProperty;

        private string _searchFilter = "";
        private Vector2 _listScroll;
        private Vector2 _detailScroll;

        private int _selectedItemIndex = -1;
        private SerializedObject _selectedItemSerialized;

        public CatalogEditorPanel(CatalogRegistration[] catalogs)
        {
            _catalogs = catalogs;
            LoadCatalog(0);
        }

        public void OnGUI()
        {
            DrawCatalogTabs();

            if (_catalogSerialized == null || _arrayProperty == null)
            {
                EditorGUILayout.HelpBox("카탈로그 에셋을 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            _catalogSerialized.Update();

            EditorGUILayout.BeginHorizontal();
            DrawItemList();
            DrawItemDetail();
            EditorGUILayout.EndHorizontal();
        }

        private void LoadCatalog(int index)
        {
            _selectedCatalogIndex = index;
            _selectedItemIndex = -1;
            _selectedItemSerialized = null;

            CatalogRegistration reg = _catalogs[index];
            _catalogAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(reg.CatalogAssetPath);
            _catalogSerialized = _catalogAsset != null ? new SerializedObject(_catalogAsset) : null;
            _arrayProperty = _catalogSerialized?.FindProperty(reg.ArrayFieldName);
        }

        private void DrawCatalogTabs()
        {
            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < _catalogs.Length; i++)
            {
                bool selected = i == _selectedCatalogIndex;
                GUI.enabled = !selected;

                if (GUILayout.Button(_catalogs[i].DisplayName))
                {
                    LoadCatalog(i);
                }

                GUI.enabled = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawItemList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(260));

            _searchFilter = EditorGUILayout.TextField("검색", _searchFilter);

            if (GUILayout.Button("+ 새 항목 추가"))
            {
                AddNewItem();
            }

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            for (int i = 0; i < _arrayProperty.arraySize; i++)
            {
                UnityEngine.Object element = _arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue;

                if (element == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(_searchFilter) && element.name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                GUI.backgroundColor = i == _selectedItemIndex ? Color.cyan : Color.white;

                if (GUILayout.Button(element.name))
                {
                    SelectItem(i, element);
                }

                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void SelectItem(int index, UnityEngine.Object obj)
        {
            _selectedItemIndex = index;
            _selectedItemSerialized = new SerializedObject(obj);
        }

        private void DrawItemDetail()
        {
            EditorGUILayout.BeginVertical("box");

            if (_selectedItemSerialized == null || _selectedItemSerialized.targetObject == null)
            {
                EditorGUILayout.LabelField("왼쪽에서 항목을 선택하세요.");
                EditorGUILayout.EndVertical();
                return;
            }

            _selectedItemSerialized.Update();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_selectedItemSerialized.targetObject.name, EditorStyles.boldLabel);
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);

            if (GUILayout.Button("삭제", GUILayout.Width(60)))
            {
                DeleteSelectedItem();
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

            SerializedProperty iterator = _selectedItemSerialized.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.propertyPath == "m_Script")
                {
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            EditorGUILayout.EndScrollView();

            if (_selectedItemSerialized.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_selectedItemSerialized.targetObject);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.EndVertical();
        }

        private void AddNewItem()
        {
            CatalogRegistration reg = _catalogs[_selectedCatalogIndex];
            ScriptableObject newItem = ScriptableObject.CreateInstance(reg.ItemType);

            string folder = Path.GetDirectoryName(reg.CatalogAssetPath)?.Replace('\\', '/');
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/New{reg.ItemType.Name}.asset");

            AssetDatabase.CreateAsset(newItem, assetPath);

            _arrayProperty.arraySize++;
            _arrayProperty.GetArrayElementAtIndex(_arrayProperty.arraySize - 1).objectReferenceValue = newItem;
            _catalogSerialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            SelectItem(_arrayProperty.arraySize - 1, newItem);
        }

        /// <summary>
        /// 현재 선택된 항목을 카탈로그 배열에서 빼고 에셋 자체도 삭제한다. 되돌릴 수 없는
        /// 작업이라 먼저 확인 대화상자를 띄운다.
        /// </summary>
        private void DeleteSelectedItem()
        {
            UnityEngine.Object target = _selectedItemSerialized.targetObject;
            string name = target.name;

            bool confirmed = EditorUtility.DisplayDialog(
                "항목 삭제",
                $"'{name}' 항목을 삭제하시겠습니까? 되돌릴 수 없습니다.",
                "삭제",
                "취소");

            if (!confirmed)
            {
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(target);

            // SerializedProperty.DeleteArrayElementAtIndex는 오브젝트 참조 배열에서 대상이
            // null이 아니면 첫 호출에 참조만 null로 지우고 슬롯 자체는 안 지운다 — 실제로
            // 슬롯을 제거하려면 한 번 더 호출해야 하는 유니티의 잘 알려진 동작이다.
            int oldSize = _arrayProperty.arraySize;
            _arrayProperty.DeleteArrayElementAtIndex(_selectedItemIndex);

            if (_arrayProperty.arraySize == oldSize)
            {
                _arrayProperty.DeleteArrayElementAtIndex(_selectedItemIndex);
            }

            _catalogSerialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.SaveAssets();

            _selectedItemIndex = -1;
            _selectedItemSerialized = null;
        }
    }
}
