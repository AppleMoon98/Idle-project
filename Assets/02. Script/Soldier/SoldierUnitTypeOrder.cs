using System;

namespace Soldier
{
    /// <summary>
    /// SoldierSO에는 별도의 "병과" 필드가 없다(Prefab 참조로만 구분되는 기존 설계, section DL) -
    /// 대신 DisplayName의 마지막 단어(등급 접두어를 뺀 나머지, 예: "레전더리 곰" → "곰")를 병과
    /// 식별자로 재사용해, 로스터/부대 편성 목록이 공유하는 고정된 병과 정렬 순서를 여기 한
    /// 곳에서만 관리한다. 로드맵 순서를 그대로 따르되, 삭제된 기마궁수(section FM)는 빼고
    /// 기마병은 현재 이름인 곰으로 갱신했다.
    /// </summary>
    public static class SoldierUnitTypeOrder
    {
        private static readonly string[] Order =
        {
            "보병", "궁병", "곰", "기사", "창병", "방패보병", "공성병"
        };

        /// <summary>
        /// definition의 병과가 Order에서 몇 번째인지 반환한다. 목록에 없는 병과(콘텐츠 추가 지연,
        /// 오타 등)는 항상 맨 뒤로 밀리도록 Order.Length를 반환한다.
        /// </summary>
        public static int IndexOf(SoldierSO definition)
        {
            string name = definition.DisplayName;
            int spaceIndex = name.LastIndexOf(' ');
            string unitType = spaceIndex >= 0 ? name.Substring(spaceIndex + 1) : name;

            int index = Array.IndexOf(Order, unitType);
            return index >= 0 ? index : Order.Length;
        }
    }
}
