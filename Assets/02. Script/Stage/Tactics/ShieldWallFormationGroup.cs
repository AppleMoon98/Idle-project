using System.Collections.Generic;
using Character.Events;
using Combat;
using Core;
using UnityEngine;

namespace Stage.Tactics
{
    /// <summary>
    /// 방패벽 전술의 실제 대형 관리자. 생성 시점에 방패병/창병을 1:1로 짝짓고, 이후 누가 죽든
    /// CharacterDiedEvent를 받아 즉시 생존자만으로 다시 재배정한다("최대한 원래 대형을 유지").
    /// 재배정 규칙: 방패병 수와 창병 수 중 작은 쪽만큼 1:1 페어를 만들고 -
    ///   - 방패병이 남으면: 남는 방패병은 순환 배정으로 기존 페어의 창병 중 하나를 추가로
    ///     보호한다(MonsterTargetSelector를 끄고 GuardPositioner를 켜서 그 창병과 가장 가까운
    ///     적을 잇는 선 위에 자리 잡는다) - 이건 순전히 위치로 막는 것이지, 데미지를 대신
    ///     받아주는 게 아니다(방패병과 창병은 서로 다른 개체이므로 데미지를 공유하지 않는다 -
    ///     Character.ShieldGuard는 오직 자기 자신만 지킨다).
    ///   - 창병/궁병이 남으면: 남는 추종자는 FormationFollower를 끄고 RangedKiter를 켜서
    ///     혼자 카이팅한다(창병도 근접보다 긴 사거리를 가진 만큼, 적이 다가오면 물러나며
    ///     공격하는 게 기본 동작 - 방패 없이 무작정 붙어 싸우지 않는다).
    /// </summary>
    public sealed class ShieldWallFormationGroup : ITacticFormationGroup
    {
        private readonly List<GameObject> _shieldBearers;
        private readonly List<GameObject> _spearmen;
        private bool _disposed;

        public bool IsCleared => _shieldBearers.Count == 0 && _spearmen.Count == 0;

        public ShieldWallFormationGroup(IReadOnlyList<GameObject> shieldBearers, IReadOnlyList<GameObject> spearmen)
        {
            _shieldBearers = new List<GameObject>(shieldBearers);
            _spearmen = new List<GameObject>(spearmen);

            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            Rebalance();
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            bool removedShieldBearer = _shieldBearers.Remove(evt.Character);
            bool removedSpearman = !removedShieldBearer && _spearmen.Remove(evt.Character);

            if (!removedShieldBearer && !removedSpearman)
            {
                return;
            }

            if (_shieldBearers.Count == 0 && _spearmen.Count == 0)
            {
                Dispose();
                return;
            }

            Rebalance();
        }

        private void Rebalance()
        {
            int shieldCount = _shieldBearers.Count;
            int spearCount = _spearmen.Count;
            int pairCount = Mathf.Min(shieldCount, spearCount);

            for (int i = 0; i < pairCount; i++)
            {
                AssignPrimaryPair(_shieldBearers[i], _spearmen[i]);
            }

            if (shieldCount > spearCount)
            {
                for (int i = pairCount; i < shieldCount; i++)
                {
                    if (spearCount > 0)
                    {
                        GameObject protectedSpearman = _spearmen[(i - pairCount) % spearCount];
                        AssignExtraGuard(_shieldBearers[i], protectedSpearman);
                    }
                    else
                    {
                        ReleaseShieldBearerAlone(_shieldBearers[i]);
                    }
                }
            }

            if (spearCount > shieldCount)
            {
                for (int i = pairCount; i < spearCount; i++)
                {
                    ReleaseFollowerAlone(_spearmen[i]);
                }
            }
        }

        private static void AssignPrimaryPair(GameObject shieldBearer, GameObject spearman)
        {
            if (shieldBearer.TryGetComponent(out MonsterTargetSelector selector))
            {
                selector.enabled = true;
            }

            if (shieldBearer.TryGetComponent(out GuardPositioner guard))
            {
                guard.enabled = false;
            }

            if (spearman.TryGetComponent(out RangedKiter kiter))
            {
                kiter.enabled = false;
            }

            if (spearman.TryGetComponent(out FormationFollower follower))
            {
                follower.enabled = true;
                follower.SetLeader(shieldBearer.transform);
            }
        }

        private static void AssignExtraGuard(GameObject shieldBearer, GameObject protectedSpearman)
        {
            if (shieldBearer.TryGetComponent(out MonsterTargetSelector selector))
            {
                selector.enabled = false;
            }

            if (shieldBearer.TryGetComponent(out GuardPositioner guard))
            {
                guard.SetProtectedUnit(protectedSpearman.transform);
                guard.enabled = true;
            }
        }

        private static void ReleaseShieldBearerAlone(GameObject shieldBearer)
        {
            if (shieldBearer.TryGetComponent(out MonsterTargetSelector selector))
            {
                selector.enabled = true;
            }

            if (shieldBearer.TryGetComponent(out GuardPositioner guard))
            {
                guard.SetProtectedUnit(null);
                guard.enabled = false;
            }
        }

        /// <summary>
        /// 보호해줄 방패병이 없는 추종자(창병 또는 대체로 들어간 궁병)를 혼자 카이팅하는
        /// 상태로 전환한다 - FormationFollower를 끄고 RangedKiter를 켠다. 궁병은 원래
        /// RangedKiter가 기본이라 사실상 원상복구고, 창병도 같은 컴포넌트로 "사거리 유지하며
        /// 물러나기" 동작을 그대로 재사용한다.
        /// </summary>
        private static void ReleaseFollowerAlone(GameObject follower)
        {
            if (follower.TryGetComponent(out FormationFollower formationFollower))
            {
                formationFollower.SetLeader(null);
                formationFollower.enabled = false;
            }

            if (follower.TryGetComponent(out RangedKiter kiter))
            {
                kiter.enabled = true;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            GameBootstrapper.Events?.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
        }
    }
}
