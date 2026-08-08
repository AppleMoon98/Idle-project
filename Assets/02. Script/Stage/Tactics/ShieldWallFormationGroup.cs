using System.Collections.Generic;
using Character;
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
    ///     적을 잇는 선 위에 자리 잡는다).
    ///   - 창병이 남으면: 남는 창병은 리더 없이(FormationFollower.SetLeader(null)) 혼자 싸운다 -
    ///     이미 FormationFollower 자체에 있는 대체 동작이라 별도 코드가 필요 없다.
    /// </summary>
    public sealed class ShieldWallFormationGroup : ITacticFormationGroup
    {
        private readonly List<GameObject> _shieldBearers;
        private readonly List<GameObject> _spearmen;
        private bool _disposed;

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
                    ReleaseSpearmanAlone(_spearmen[i]);
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

            if (shieldBearer.TryGetComponent(out ShieldGuard shieldGuard) && spearman.TryGetComponent(out Health spearHealth))
            {
                shieldGuard.SetProtectedUnit(spearHealth);
            }

            if (spearman.TryGetComponent(out FormationFollower follower))
            {
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

            if (shieldBearer.TryGetComponent(out ShieldGuard shieldGuard) && protectedSpearman.TryGetComponent(out Health spearHealth))
            {
                shieldGuard.SetProtectedUnit(spearHealth);
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

            if (shieldBearer.TryGetComponent(out ShieldGuard shieldGuard))
            {
                shieldGuard.SetProtectedUnit(null);
            }
        }

        private static void ReleaseSpearmanAlone(GameObject spearman)
        {
            if (spearman.TryGetComponent(out FormationFollower follower))
            {
                follower.SetLeader(null);
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
