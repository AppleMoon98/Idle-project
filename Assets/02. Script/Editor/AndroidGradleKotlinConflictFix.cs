using System.IO;
using UnityEditor.Android;

namespace IdleProject.Editor
{
    /// <summary>
    /// Google Play Billing(com.unity.purchasing)이 추가하는 최신 kotlin-stdlib와
    /// androidx.games:games-activity가 끌어오는 구버전 kotlin-stdlib-jdk7/jdk8(1.6.21)가
    /// 충돌해 checkReleaseDuplicateClasses에서 빌드가 실패하는 문제를 해결한다.
    /// 빌드마다 자동 생성되는 unityLibrary/build.gradle에 exclude 규칙을 주입한다.
    /// </summary>
    public class AndroidGradleKotlinConflictFix : IPostGenerateGradleAndroidProject
    {
        private const string Marker = "// Kotlin stdlib duplicate-class fix (auto-inserted by AndroidGradleKotlinConflictFix)";

        public int callbackOrder => 999;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string gradleFilePath = Path.Combine(path, "build.gradle");
            if (!File.Exists(gradleFilePath))
            {
                return;
            }

            string content = File.ReadAllText(gradleFilePath);
            if (content.Contains(Marker))
            {
                return;
            }

            string fix =
                "\n" + Marker + "\n" +
                "configurations.all {\n" +
                "    resolutionStrategy {\n" +
                "        force 'org.jetbrains.kotlin:kotlin-stdlib:1.8.22'\n" +
                "        exclude group: 'org.jetbrains.kotlin', module: 'kotlin-stdlib-jdk7'\n" +
                "        exclude group: 'org.jetbrains.kotlin', module: 'kotlin-stdlib-jdk8'\n" +
                "    }\n" +
                "}\n";

            File.AppendAllText(gradleFilePath, fix);
        }
    }
}
