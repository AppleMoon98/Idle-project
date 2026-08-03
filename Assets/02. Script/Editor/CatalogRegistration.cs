using System;

namespace Editor
{
    /// <summary>
    /// CatalogEditorPanel이 다룰 카탈로그 하나의 등록 정보. 이 프로젝트의 카탈로그 SO는
    /// 전부 "배열 하나만 든 컨테이너" 형태(EquipmentCatalogSO/SoldierCatalogSO/...)라 이
    /// 4개 값만으로 어떤 카탈로그든 표현할 수 있다. 새 카탈로그를 이 도구에 추가하려면
    /// ContentEditorWindow의 등록 목록에 이 구조체 하나만 더 넣으면 된다.
    /// </summary>
    public sealed class CatalogRegistration
    {
        public readonly string DisplayName;
        public readonly string CatalogAssetPath;
        public readonly string ArrayFieldName;
        public readonly Type ItemType;

        public CatalogRegistration(string displayName, string catalogAssetPath, string arrayFieldName, Type itemType)
        {
            DisplayName = displayName;
            CatalogAssetPath = catalogAssetPath;
            ArrayFieldName = arrayFieldName;
            ItemType = itemType;
        }
    }
}
