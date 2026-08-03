using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 앱 버전(Application.version)을 표시한다.
    /// </summary>
    public sealed class VersionDisplayUI : MonoBehaviour
    {
        [SerializeField]
        private Text versionText;

        private void Awake()
        {
            versionText.text = $"버전 {Application.version}";
        }
    }
}
