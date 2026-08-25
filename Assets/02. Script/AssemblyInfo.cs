using System.Runtime.CompilerServices;

// Assets/02. Script/Editor 아래의 회귀 검증 스크립트(Assembly-CSharp-Editor로 컴파일됨, 이 프로젝트에
// 게임 코드 전용 asmdef가 없어 새 테스트 전용 어셈블리를 만들어 참조하는 방식은 프로젝트 전체 어셈블리
// 경계를 재구성해야 해서 포기했다)가 이 어셈블리(Assembly-CSharp)의 internal 타입
// (Character.RuntimeStatApplier/PossessionStatApplier 등)에 접근할 수 있도록 허용한다. 프로덕션 코드
// 쪽 접근 제한자는 그대로 두고(internal이 맞는 캡슐화 범위), 검증 스크립트만 예외적으로 통과시킨다.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
