using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Editor
{
    /// <summary>
    /// GitHub 이슈 #42 - 팝업(popupRoot 필드를 가진 모든 MonoBehaviour, 현재 31개) 바깥 영역이
    /// 레이캐스트를 막지 않아 배경 UI(하단 탭 등)로 터치가 그대로 관통되던 문제를 씬 구조 차원에서
    /// 일괄 해결하는 1회성 마이그레이션 도구. 새 런타임 C# 로직은 필요 없다 - popupRoot로 지정된
    /// 패널은 대부분 화면 정중앙에 고정 크기로 배치돼 있고, 그 바로 위 부모(팝업 스크립트가 붙은,
    /// 항상 활성 상태인 컨테이너)는 이미 전체화면으로 앵커돼 있는 이 프로젝트의 공통 패턴이다.
    /// 그래서 패널을 새로 만든 전체화면 Wrapper 밑으로 한 단계 더 내리고, 그 Wrapper의 첫 자식으로
    /// 전체화면 차단용 Image를 추가한 뒤, popupRoot 필드 자체를 그 Wrapper로 다시 연결하기만 하면
    /// 된다(Wrapper가 SetActive될 때 차단 Image와 기존 패널이 함께 켜지고 꺼진다) - 팝업 스크립트
    /// 31개를 코드 레벨에서 손댈 필요가 전혀 없다. 패널의 부모가 이 공통 패턴(전체화면 앵커)을
    /// 벗어난 예외라면 아무것도 건드리지 않고 경고만 남긴다 - 그런 팝업은 손으로 확인해야 한다.
    /// 몇 번을 실행해도 안전하다(idempotent) - 이미 처리된 팝업은 기존 Wrapper를 그대로 반환한다.
    /// </summary>
    internal static class PopupInputBlockerMigration
    {
        private const string BlockerChildName = "InputBlocker";
        private const string WrapperNameSuffix = "_ModalRoot";
        private const string PopupRootFieldName = "popupRoot";
        private static readonly Color BlockerColor = new(0f, 0f, 0f, 0.5f);

        [MenuItem("Idle Project/Add Full-Screen Input Blockers To All Popups")]
        private static void Run()
        {
            var candidates = new Dictionary<GameObject, List<(MonoBehaviour Component, FieldInfo Field)>>();

            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null)
                {
                    continue;
                }

                FieldInfo field = mb.GetType().GetField(PopupRootFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (field == null || field.FieldType != typeof(GameObject))
                {
                    continue;
                }

                if (field.GetValue(mb) is not GameObject panel || panel == null)
                {
                    continue;
                }

                if (!candidates.TryGetValue(panel, out List<(MonoBehaviour, FieldInfo)> list))
                {
                    list = new List<(MonoBehaviour, FieldInfo)>();
                    candidates[panel] = list;
                }

                list.Add((mb, field));
            }

            int migrated = 0;
            int alreadyDone = 0;
            var warnings = new List<string>();

            foreach (KeyValuePair<GameObject, List<(MonoBehaviour Component, FieldInfo Field)>> pair in candidates)
            {
                GameObject panel = pair.Key;
                GameObject wrapper = TryMigrate(panel, warnings, out bool wasAlreadyMigrated);

                if (wrapper == null)
                {
                    continue;
                }

                if (wasAlreadyMigrated)
                {
                    alreadyDone++;
                }
                else
                {
                    migrated++;
                }

                foreach ((MonoBehaviour component, FieldInfo field) in pair.Value)
                {
                    if (!ReferenceEquals(field.GetValue(component), wrapper))
                    {
                        field.SetValue(component, wrapper);
                        EditorUtility.SetDirty(component);
                    }
                }
            }

            if (migrated > 0 || alreadyDone > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            Debug.Log($"[PopupInputBlockerMigration] 완료 - 팝업 대상={candidates.Count}, 새로 처리={migrated}, 이미 처리됨(재실행)={alreadyDone}, 경고={warnings.Count}");

            foreach (string warning in warnings)
            {
                Debug.LogWarning($"[PopupInputBlockerMigration] {warning}");
            }
        }

        /// <summary>
        /// panel을 감싸는 전체화면 Wrapper를 반환한다. 이미 마이그레이션된 상태면 기존 Wrapper를
        /// 그대로 반환(wasAlreadyMigrated=true), 처음이면 새로 만들어 반환한다. panel의 부모가
        /// 전체화면 앵커가 아니면(공통 패턴을 벗어난 예외 케이스) 아무것도 바꾸지 않고 null을
        /// 반환하며 warnings에 사유를 남긴다.
        /// </summary>
        private static GameObject TryMigrate(GameObject panel, List<string> warnings, out bool wasAlreadyMigrated)
        {
            wasAlreadyMigrated = false;
            Transform parent = panel.transform.parent;

            if (parent == null)
            {
                warnings.Add($"{GetPath(panel)} - 부모가 없음(Canvas 직계가 아님), 건너뜀");
                return null;
            }

            Transform existingBlocker = parent.Find(BlockerChildName);

            if (existingBlocker != null)
            {
                wasAlreadyMigrated = true;
                return parent.gameObject;
            }

            if (!parent.TryGetComponent(out RectTransform parentRect) || !IsFullScreenStretch(parentRect))
            {
                warnings.Add($"{GetPath(panel)} - 부모({parent.name})가 전체화면 앵커가 아님, 수동 확인 필요");
                return null;
            }

            if (!panel.TryGetComponent(out RectTransform panelRect))
            {
                warnings.Add($"{GetPath(panel)} - RectTransform이 없음, 건너뜀");
                return null;
            }

            int siblingIndex = panelRect.GetSiblingIndex();
            bool wasActive = panel.activeSelf;

            var wrapperGo = new GameObject(panel.name + WrapperNameSuffix, typeof(RectTransform));
            Transform wrapperTransform = wrapperGo.transform;
            wrapperTransform.SetParent(parent, false);
            wrapperTransform.SetSiblingIndex(siblingIndex);

            var wrapperRect = (RectTransform)wrapperTransform;
            wrapperRect.anchorMin = Vector2.zero;
            wrapperRect.anchorMax = Vector2.one;
            wrapperRect.offsetMin = Vector2.zero;
            wrapperRect.offsetMax = Vector2.zero;
            wrapperRect.pivot = new Vector2(0.5f, 0.5f);

            panelRect.SetParent(wrapperTransform, false);

            var blockerGo = new GameObject(BlockerChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Transform blockerTransform = blockerGo.transform;
            blockerTransform.SetParent(wrapperTransform, false);
            blockerTransform.SetAsFirstSibling();

            var blockerRect = (RectTransform)blockerTransform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;

            Image blockerImage = blockerGo.GetComponent<Image>();
            blockerImage.color = BlockerColor;
            blockerImage.raycastTarget = true;

            // Wrapper가 켜고 끄는 역할을 대신하므로, 패널 자신의 활성 플래그는 항상 켜둔다 -
            // 그래야 Wrapper.SetActive(true) 한 번에 차단 Image와 패널이 함께 캐스케이드된다.
            panel.SetActive(true);
            wrapperGo.SetActive(wasActive);

            EditorUtility.SetDirty(wrapperGo);
            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(blockerGo);

            return wrapperGo;
        }

        private static bool IsFullScreenStretch(RectTransform rect)
        {
            return Vector2.Distance(rect.anchorMin, Vector2.zero) < 0.001f
                && Vector2.Distance(rect.anchorMax, Vector2.one) < 0.001f;
        }

        private static string GetPath(GameObject go)
        {
            var segments = new List<string>();
            Transform current = go.transform;

            while (current != null)
            {
                segments.Insert(0, current.name);
                current = current.parent;
            }

            return string.Join("/", segments);
        }
    }
}
