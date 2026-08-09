# ImportedMods 자동 탐색 v2.9

`MobileModController`와 모드 제작용 오브젝트는 싱글톤 프리팹 내부에서 `DontDestroyOnLoad`로 유지됩니다.
씬 전환 시 다시 찾아야 하는 참조는 `ImportedMods` 하나뿐입니다.

## 기본 설정

각 모드 사용 씬에 다음 이름의 오브젝트를 배치하세요.

```text
ImportedMods
```

루트 오브젝트 또는 다른 오브젝트의 비활성 자식이어도 검색됩니다.

`MobileModController` 설정:

```text
Imported Mods Object Name: ImportedMods
Create Imported Mods If Missing: true
```

## 탐색 순서

1. 현재 활성 씬의 기존 참조가 유효한지 확인
2. 활성 씬의 루트에서 정확한 이름 검색
3. 활성 씬의 모든 자식과 비활성 자식 검색
4. Additive 방식으로 로드된 다른 씬 검색
5. 찾지 못하면 활성 씬에 자동 생성

## 직접 호출

```csharp
Transform importedMods =
    MobileModController.Instance.GetImportedModsParent();
```

강제로 다시 검색:

```csharp
MobileModController.Instance.FindImportedModsNow();
```

직접 지정:

```csharp
MobileModController.Instance.SetImportedModsParent(
    importedModsTransform
);
```

`MobileModSceneReferences`는 필수가 아닙니다. `ImportedMods` 이름이 다르거나 직접 참조를 지정하고 싶을 때만 사용합니다.
