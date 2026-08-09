# MobileModSystem v2.8 - Singleton / Scene References

## 1. 첫 씬

`MobileModController`가 붙은 오브젝트를 하나만 둡니다.

실행 시 다음 동작을 합니다.

- `MobileModController.Instance`에 등록
- 부모가 있다면 루트로 이동
- `DontDestroyOnLoad` 적용
- 다음 씬에 중복 매니저가 있으면 중복 오브젝트 삭제
- `SceneManager.sceneLoaded`에서 새 씬 참조 갱신

코드 호출:

```csharp
MobileModController.Instance.PickAndImportModPackage();
MobileModController.Instance.LoadRecentModPackage();
MobileModController.Instance.ExportCurrentMod();
```

안전 검사:

```csharp
if (MobileModController.HasInstance)
{
    MobileModController.Instance.LoadRecentModPackage();
}
```

## 2. 각 씬

빈 GameObject를 만들고 `MobileModSceneReferences`를 추가합니다.

권장 구조:

```text
MobileModSceneReferences
├── BuildRoot
└── ImportedMods
```

Inspector 연결:

```text
MobileModSceneReferences
├── Imported Mods Parent -> ImportedMods
└── Build Root           -> BuildRoot (제작 기능을 쓰는 씬만)
```

`Imported Mods Parent`를 비우면 `MobileModSceneReferences`가 붙은 오브젝트 자체가 부모로 사용됩니다.

씬에 `MobileModSceneReferences`가 없고 `Create Fallback Imported Mods Parent`가 켜져 있으면, 활성 씬에 `ImportedMods` 오브젝트를 자동 생성합니다.

## 3. 씬 전환 동작

```text
새 씬 로드
→ MobileModController 유지
→ 새 씬의 MobileModSceneReferences 탐색
→ importedModsParent / buildRoot 재연결
→ 모드 임포트 시 새 씬의 부모 아래에 생성
```

## 4. 주의

- `MobileModController`는 첫 씬에만 두는 것이 좋습니다.
- 다른 씬에 실수로 매니저를 다시 넣어도 중복 인스턴스는 삭제됩니다.
- 씬의 UI 버튼이 Persistent 매니저를 Inspector에서 직접 참조할 수 없다면, 버튼용 로컬 스크립트에서 `MobileModController.Instance`를 호출하세요.
