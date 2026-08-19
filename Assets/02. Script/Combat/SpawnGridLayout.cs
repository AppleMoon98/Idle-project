using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 몬스터(Stage 도메인)와 병사(Soldier 도메인)가 공유하는 "화면(최대 축소 기준 고정 범위) 밖
    /// 경계선에서 시작하는 가로 13열, 세로 무제한 그리드" 좌표 계산기. 두 도메인 모두 항상 이
    /// 화면 경계 바깥에서만 스폰된다 — 몬스터는 위쪽(북쪽) 경계 밖, 병사는 아래쪽(남쪽) 경계 밖.
    /// index가 커질수록(그리드가 행 우선으로 채워질수록) 화면 밖으로 더 멀어지는 방향
    /// (rowSign)으로 물러난다 — 몬스터는 +Y(더 위로), 병사는 -Y(더 아래로). 열은 항상 월드 X축을
    /// 따라 중앙 정렬로 배치된다.
    /// </summary>
    public static class SpawnGridLayout
    {
        public const int Columns = 13;
        public const float ColumnSpacing = 1.5f;
        public const float RowSpacing = 1.5f;
        public const float ScreenEdgeMargin = 1f;

        /// <summary>
        /// 화면 아래쪽에는 하단 UI(메인 탭 바 등)가 실제 플레이 화면 일부를 가리고 있어, 병사
        /// 스폰 기준점(ComputeBottomOrigin)만 그만큼 화면 중앙 쪽으로 끌어올린다 — 어차피 그 구간은
        /// 하단 UI에 가려 보이지 않는 영역이라, 이만큼 덜 내려가도 "화면 밖"이라는 인상은 그대로
        /// 유지하면서 병사가 덜 멀리서 등장한다. 몬스터 쪽(ComputeTopOrigin)은 대응하는 UI가 없어
        /// 적용하지 않는다.
        /// </summary>
        public const float BottomUiClearance = 4f;

        /// <summary>
        /// 화면 위쪽(북쪽) 경계 바로 밖의 그리드 기준점(0번 행의 중심선).
        /// </summary>
        public static Vector3 ComputeTopOrigin(Vector3 boundsCenter, Vector2 boundsHalfExtent)
        {
            return new Vector3(boundsCenter.x, boundsCenter.y + boundsHalfExtent.y + ScreenEdgeMargin, 0f);
        }

        /// <summary>
        /// 화면 아래쪽(남쪽) 경계 바로 밖의 그리드 기준점(0번 행의 중심선) — 하단 UI가 가리는
        /// 만큼(BottomUiClearance) 화면 중앙 쪽으로 더 끌어올려져 있다.
        /// </summary>
        public static Vector3 ComputeBottomOrigin(Vector3 boundsCenter, Vector2 boundsHalfExtent)
        {
            return new Vector3(boundsCenter.x, boundsCenter.y - boundsHalfExtent.y - ScreenEdgeMargin + BottomUiClearance, 0f);
        }

        /// <summary>
        /// index(0부터, 행 우선: column = index % Columns, row = index / Columns)의 그리드 좌표.
        /// rowSign은 몬스터=+1(더 위로), 병사=-1(더 아래로).
        /// </summary>
        public static Vector3 ComputePosition(int index, Vector3 origin, float rowSign)
        {
            int column = index % Columns;
            int row = index / Columns;

            float columnOffset = (column - (Columns - 1) / 2f) * ColumnSpacing;
            float rowOffset = row * RowSpacing * rowSign;

            return new Vector3(origin.x + columnOffset, origin.y + rowOffset, origin.z);
        }

        /// <summary>
        /// 화면 왼쪽(서쪽) 경계 바로 밖의 지점 — index만큼 세로(Y)로 순환 스프레드한다(습격 전술
        /// 등, 그리드 본래 용도인 몬스터/병사 기본 스폰과는 별개로 화면 좌/우 가장자리 좌표가
        /// 필요한 소수의 특수 호출부를 위한 보조 계산).
        /// </summary>
        public static Vector3 ComputeLeftEdgePosition(int index, Vector3 boundsCenter, Vector2 boundsHalfExtent)
        {
            float x = boundsCenter.x - boundsHalfExtent.x - ScreenEdgeMargin;
            float y = boundsCenter.y + ((index % Columns) - (Columns - 1) / 2f) * ColumnSpacing;
            return new Vector3(x, y, 0f);
        }

        /// <summary>
        /// ComputeLeftEdgePosition과 동일하되 화면 오른쪽(동쪽) 경계 바로 밖.
        /// </summary>
        public static Vector3 ComputeRightEdgePosition(int index, Vector3 boundsCenter, Vector2 boundsHalfExtent)
        {
            float x = boundsCenter.x + boundsHalfExtent.x + ScreenEdgeMargin;
            float y = boundsCenter.y + ((index % Columns) - (Columns - 1) / 2f) * ColumnSpacing;
            return new Vector3(x, y, 0f);
        }
    }
}
