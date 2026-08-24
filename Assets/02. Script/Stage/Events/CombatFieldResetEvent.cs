namespace Stage.Events
{
    /// <summary>
    /// StageController.LoadStage/PauseForOverlay/ResumeAfterOverlay가 발행한다 - "지금 전장에
    /// 남아있는 것들은 전부 무효"라는 뜻으로, 실제 스테이지 전환뿐 아니라 던전/랭크 승급전 같은
    /// 오버레이 진입·복귀 시점까지 포괄한다(Stage.Events.StageChangedEvent는 오버레이 진입 시
    /// 발행되지 않으므로 - 실제 진행도를 건드리지 않기 위해 의도적으로 그렇다, 오버레이 경계까지
    /// 함께 커버해야 하는 구독자는 이 이벤트를 대신 쓴다). 발사된 화살(Combat.Projectile)이나
    /// 시전 중인 광역/지속 스킬(Skill.Effects.PoisonSkillEffect 등)처럼, 스테이지가 바뀌거나
    /// 던전에 들어가고 나온 뒤에도 판정만 계속 살아남아 엉뚱한(다음 스테이지/던전의) 대상을 계속
    /// 공격하는 것을 막기 위한 신호다. 페이로드가 필요 없는 순수 신호(readonly struct, 필드 없음).
    /// </summary>
    public readonly struct CombatFieldResetEvent
    {
    }
}
