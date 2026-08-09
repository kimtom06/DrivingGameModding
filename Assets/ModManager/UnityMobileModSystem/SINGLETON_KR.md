# MobileModController 싱글톤 v2.9

`MobileModController`가 붙은 프리팹을 최초 씬에 한 번 배치하세요.
컨트롤러 오브젝트는 실행 중 씬 최상위로 이동한 뒤 `DontDestroyOnLoad`로 유지됩니다.

```csharp
MobileModController.Instance.PickAndImportModPackage();
MobileModController.Instance.LoadRecentModPackage();
MobileModController.Instance.PickAndApplyAudio();
```

프리팹 내부의 다음 데이터는 씬 전환 후에도 그대로 유지됩니다.

```text
buildRoot
mainModelRoot
AudioStorage
설정 텍스트
최근 모드 캐시 설정
```

`ImportedMods`는 각 씬에 존재하는 생성 대상이므로 씬 전환 후 자동으로 다시 검색합니다.
각 씬에 이름이 `ImportedMods`인 오브젝트를 배치하면 별도 등록 스크립트가 필요 없습니다.

찾지 못했을 때 자동 생성하지 않으려면 다음 옵션을 끄세요.

```text
Create Imported Mods If Missing: false
```
