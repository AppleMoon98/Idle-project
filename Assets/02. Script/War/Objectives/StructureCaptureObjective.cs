using UnityEngine;

namespace War.Objectives
{
    /// <summary>
    /// 구조물 점령 목표. requiredStructures 전부가 점령되면 완료된다.
    /// </summary>
    public sealed class StructureCaptureObjective : MonoBehaviour, IWarObjective
    {
        [SerializeField]
        private WarStructure[] requiredStructures;

        public bool IsCompleted
        {
            get
            {
                if (requiredStructures == null || requiredStructures.Length == 0)
                {
                    return false;
                }

                foreach (WarStructure structure in requiredStructures)
                {
                    if (structure == null || !structure.IsCaptured)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public bool HasFailed => false;

        public float Progress01
        {
            get
            {
                if (requiredStructures == null || requiredStructures.Length == 0)
                {
                    return 0f;
                }

                float total = 0f;
                int count = 0;

                foreach (WarStructure structure in requiredStructures)
                {
                    if (structure == null)
                    {
                        continue;
                    }

                    total += structure.Control;
                    count++;
                }

                return count == 0 ? 0f : total / count;
            }
        }

        public void ResetForNewAttempt()
        {
            if (requiredStructures == null)
            {
                return;
            }

            foreach (WarStructure structure in requiredStructures)
            {
                structure?.ResetForNewAttempt();
            }
        }
    }
}
