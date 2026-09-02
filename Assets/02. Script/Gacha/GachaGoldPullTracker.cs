using System;

namespace Gacha
{
    /// <summary>
    /// 티어별 골드 뽑기 누적 횟수를 추적한다. 무엇을 뽑는지와 무관한 순수 카운팅 메커니즘이라,
    /// GachaService(병사)/SkillGachaService(스킬)가 각자 동일한 형태로 갖고 있던 int[] 필드 +
    /// 조회/증가/스냅샷 로직을 하나로 뽑았다 — 두 서비스의 Pull/TryPullOne 본체(무엇을 어떻게
    /// 뽑는지)는 여전히 도메인별로 독립돼 있고, 이건 그 아래에서 공유하는 부품일 뿐이다.
    /// </summary>
    public sealed class GachaGoldPullTracker
    {
        private readonly int[] _counts;

        public GachaGoldPullTracker(int tierCount)
        {
            _counts = new int[tierCount];
        }

        /// <summary>
        /// tierIndex 테이블에서 지금까지 성공한 골드 뽑기 횟수.
        /// </summary>
        public int GetCount(int tierIndex)
        {
            return tierIndex >= 0 && tierIndex < _counts.Length ? _counts[tierIndex] : 0;
        }

        /// <summary>
        /// tierIndex 테이블의 골드 뽑기 성공 횟수를 1 늘린다.
        ///
        /// GitHub 이슈 #48 - 이미 int.MaxValue에 도달한 카운트를 그대로 unchecked ++하면
        /// int.MinValue로 반전돼(오버플로), 이후 GachaTableSO.GetGoldCostForPull이 "매우 많이
        /// 뽑았다"가 아니라 "전혀 안 뽑았다"로 오판해 비용이 초기값으로 돌아가는 결과를 낳는다.
        /// int.MaxValue에 도달하면 더 이상 증가시키지 않고 그 값에서 캡한다 - 그 시점이면 이미
        /// GetGoldCostForPull이 반환할 수 있는 최대 비용(int.MaxValue)에 도달해 있으므로
        /// 게임플레이상으로도 더 늘어날 이유가 없다.
        /// </summary>
        public void Increment(int tierIndex)
        {
            if (tierIndex >= 0 && tierIndex < _counts.Length && _counts[tierIndex] < int.MaxValue)
            {
                _counts[tierIndex]++;
            }
        }

        /// <summary>
        /// 테이블 배열 순서 그대로의 누적 횟수 스냅샷(SaveService가 세이브 직렬화에 쓴다).
        /// </summary>
        public int[] ExportSnapshot()
        {
            return (int[])_counts.Clone();
        }

        /// <summary>
        /// 세이브에서 복원한 스냅샷을 그대로 되돌린다. counts가 null이면 아무것도 안 건드린다
        /// (RestoreSnapshot 자체가 한 번도 호출된 적 없는 것과 동일하게 취급 - 이 클래스가
        /// 매번 새로 생성돼 항상 0으로 시작하는 것과 일관됨). counts가 있으면 매번 먼저 전체를
        /// 0으로 리셋한 뒤 겹치는 앞부분만 덮어쓴다.
        ///
        /// GitHub 이슈 #48 - 예전엔 리셋 없이 겹치는 앞부분만 덮어썼다: 콘텐츠 삭제 등으로
        /// counts가 이전보다 짧아지면(예: {int.MaxValue, -5} 복원 후 {7}로 재복원) 뒤쪽 인덱스가
        /// 손도 안 닿아 옛 값(-5)이 그대로 남고, 재직렬화하면 [7, -5]처럼 절대 저장된 적 없는
        /// 상태가 나왔다 - Soldier.SoldierRosterService.RestoreSnapshot 등이 이미 확립한
        /// "clear-first" 관례(GitHub 이슈 #26/#46)를 여기도 동일하게 적용했다. 음수 값도 이제
        /// 0으로 클램프한다 - 뽑기 횟수는 절대 음수일 수 없는 값이라, 손상된 세이브(레지스트리/
        /// plist 직접 편집 등)가 음수를 넣어도 "0회"로 안전하게 취급한다.
        /// </summary>
        public void RestoreSnapshot(int[] counts)
        {
            if (counts == null)
            {
                return;
            }

            Array.Clear(_counts, 0, _counts.Length);

            int length = Math.Min(counts.Length, _counts.Length);

            for (int i = 0; i < length; i++)
            {
                _counts[i] = Math.Max(0, counts[i]);
            }
        }
    }
}
