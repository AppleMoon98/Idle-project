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
        /// </summary>
        public void Increment(int tierIndex)
        {
            if (tierIndex >= 0 && tierIndex < _counts.Length)
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
        /// 세이브에서 복원한 스냅샷을 그대로 되돌린다. counts가 null이거나 길이가 안 맞으면
        /// 겹치는 앞부분만 복원한다(콘텐츠 추가로 티어 수가 늘어난 세이브도 안전하게 처리).
        /// 시딩이지 게임플레이 변화가 아니므로 이벤트는 발행하지 않는다.
        /// </summary>
        public void RestoreSnapshot(int[] counts)
        {
            if (counts == null)
            {
                return;
            }

            int length = Math.Min(counts.Length, _counts.Length);

            for (int i = 0; i < length; i++)
            {
                _counts[i] = counts[i];
            }
        }
    }
}
