using System;
using Skill;
using UnityEngine;

namespace Gacha
{
    /// <summary>
    /// 가챠 확률 테이블 내 스킬 하나의 가중치를 정의한다. SkillGachaTableSO에서 사용한다.
    /// GachaPoolEntry(병사)와 동일한 형태 — 가중치 기반 선택이므로 확률(0~1)이 아니라
    /// 상대적 가중치(정수)로 표현한다.
    /// </summary>
    [Serializable]
    public sealed class SkillGachaPoolEntry
    {
        [SerializeField]
        private SkillSO skill;

        [SerializeField]
        private int weight;

        /// <summary>
        /// SkillGachaTableSO가 SkillCatalogSO로부터 항목을 자동 생성할 때 쓰는 생성자
        /// (인스펙터 수동 배열 대신 런타임에 조립하는 용도).
        /// </summary>
        public SkillGachaPoolEntry(SkillSO skill, int weight)
        {
            this.skill = skill;
            this.weight = weight;
        }

        /// <summary>
        /// 뽑힐 수 있는 스킬.
        /// </summary>
        public SkillSO Skill => skill;

        /// <summary>
        /// 다른 항목 대비 상대적 가중치. 전체 가중치 합 대비 이 값의 비율이 뽑힐 확률이 된다.
        /// </summary>
        public int Weight => weight;
    }
}
