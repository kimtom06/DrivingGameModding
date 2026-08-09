# MobileModSceneReferences 대기 후 임포트

`PickAndImportModPackage()`와 `LoadRecentModPackage()`는 더 이상 임포트 시작 시점의 fallback 부모에 모드를 생성하지 않습니다.

다음 두 조건이 만족될 때까지 매 프레임 기다립니다.

1. 현재 활성 씬에 `MobileModSceneReferences`가 존재
2. 그 컴포넌트의 `importedModsParent` 필드가 명시적으로 지정되어 있음

준비되면 해당 Transform 아래에서만 모드 패키지를 생성하고, 완전히 성공한 뒤 완료 콜백을 호출합니다.

## Inspector 설정

`MobileModController`:

- `Wait For Scene References Before Import`: true
- `Scene References Wait Timeout Seconds`: 0이면 무제한 대기

각 씬 또는 런타임 생성 오브젝트:

- `MobileModSceneReferences` 추가
- `Imported Mods Parent`에 실제 생성 부모 연결

`Imported Mods Parent`를 비워두면 계속 기다립니다. 이전처럼 컴포넌트 자신의 Transform을 자동 사용하지 않습니다.
