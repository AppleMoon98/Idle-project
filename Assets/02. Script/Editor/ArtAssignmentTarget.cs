using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// 스프라이트 하나를 필요로 하는 대상 하나를 추상화한다. 프리팹의 SpriteRenderer인지,
    /// 씬 오브젝트의 SpriteRenderer인지, SO의 icon 필드인지에 따라 로드/저장 방식이 전혀
    /// 다르므로 이 클래스 계층이 그 차이를 감춘다 — ContentEditorWindow는 Apply만 호출하면 된다.
    /// </summary>
    public abstract class ArtAssignmentTarget
    {
        public abstract string Label { get; }

        public abstract Sprite CurrentSprite { get; }

        /// <summary>
        /// sprite를 실제 대상에 반영하고 저장한다. 대상을 찾지 못하면 아무 변화 없이 false.
        /// </summary>
        public abstract bool Apply(Sprite sprite);
    }

    /// <summary>
    /// 프리팹 에셋의 SpriteRenderer(루트 또는 자식)를 교체한다. 씬/Prefab Mode를 열지 않고
    /// PrefabUtility.LoadPrefabContents로 프리팹 내용만 메모리에 올려 수정 후 저장한다.
    /// </summary>
    public sealed class PrefabSpriteTarget : ArtAssignmentTarget
    {
        private readonly string _label;
        private readonly string _prefabPath;

        public PrefabSpriteTarget(string label, string prefabPath)
        {
            _label = label;
            _prefabPath = prefabPath;
        }

        public override string Label => _label;

        public override Sprite CurrentSprite
        {
            get
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
                SpriteRenderer renderer = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>(true) : null;
                return renderer != null ? renderer.sprite : null;
            }
        }

        public override bool Apply(Sprite sprite)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(_prefabPath);

            try
            {
                SpriteRenderer renderer = root.GetComponentInChildren<SpriteRenderer>(true);

                if (renderer == null)
                {
                    return false;
                }

                renderer.sprite = sprite;
                PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    /// <summary>
    /// 프리팹이 아니라 현재 열린 씬에 직접 놓인 오브젝트(Player)의 SpriteRenderer를 교체한다.
    /// 씬이 열려있지 않거나 오브젝트를 못 찾으면 실패로 처리한다.
    /// </summary>
    public sealed class SceneSpriteTarget : ArtAssignmentTarget
    {
        private readonly string _label;
        private readonly string _gameObjectName;

        public SceneSpriteTarget(string label, string gameObjectName)
        {
            _label = label;
            _gameObjectName = gameObjectName;
        }

        public override string Label => _label;

        public override Sprite CurrentSprite
        {
            get
            {
                SpriteRenderer renderer = FindRenderer();
                return renderer != null ? renderer.sprite : null;
            }
        }

        public override bool Apply(Sprite sprite)
        {
            SpriteRenderer renderer = FindRenderer();

            if (renderer == null)
            {
                return false;
            }

            Undo.RecordObject(renderer, "Assign Sprite");
            renderer.sprite = sprite;
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
            return true;
        }

        private SpriteRenderer FindRenderer()
        {
            GameObject go = GameObject.Find(_gameObjectName);
            return go != null ? go.GetComponentInChildren<SpriteRenderer>(true) : null;
        }
    }

    /// <summary>
    /// ScriptableObject 에셋의 Sprite 필드(기본 이름 "icon")를 SerializedObject로 교체한다.
    /// </summary>
    public sealed class ScriptableObjectIconTarget : ArtAssignmentTarget
    {
        private readonly string _label;
        private readonly string _assetPath;
        private readonly string _fieldName;

        public ScriptableObjectIconTarget(string label, string assetPath, string fieldName = "icon")
        {
            _label = label;
            _assetPath = assetPath;
            _fieldName = fieldName;
        }

        public override string Label => _label;

        public override Sprite CurrentSprite
        {
            get
            {
                ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(_assetPath);

                if (so == null)
                {
                    return null;
                }

                SerializedProperty prop = new SerializedObject(so).FindProperty(_fieldName);
                return prop != null ? prop.objectReferenceValue as Sprite : null;
            }
        }

        public override bool Apply(Sprite sprite)
        {
            ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(_assetPath);

            if (so == null)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(so);
            SerializedProperty prop = serialized.FindProperty(_fieldName);

            if (prop == null)
            {
                return false;
            }

            prop.objectReferenceValue = sprite;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
