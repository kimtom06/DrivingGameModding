# 기존 모드 다시 편집 기능

## UI 버튼

```csharp
MobileModController.Instance.PickAndOpenModForEditing();
MobileModController.Instance.OpenRecentModForEditing();
MobileModController.Instance.ExportCurrentMod();
MobileModController.Instance.CloseCurrentModEditing();
```

## 일반 불러오기와 차이

- `PickAndImportModPackage()`는 플레이용으로 `ImportedMods` 아래에 생성합니다.
- `PickAndOpenModForEditing()`는 불러온 루트 자체를 현재 `buildRoot`로 바꿉니다.
- 이후 모델/오디오/설정 텍스트를 추가하거나 Transform을 수정한 뒤 `ExportCurrentMod()`를 호출합니다.

## 편집 가능한 계층

`ModNode` 컴포넌트가 있는 오브젝트가 모드 데이터로 저장됩니다. GLB가 생성한 `__Model` 내부 계층은 원본 GLB로 다시 생성되므로 개별 노드로 저장하지 않습니다.

## 씬 UI 버튼 연결

후속 씬의 버튼은 Persistent 싱글톤을 Inspector에서 직접 연결하기 어려울 수 있습니다.
씬 오브젝트에 `MobileModSceneUiActions`를 추가한 뒤 버튼을 다음 함수에 연결하세요.

```text
기존 모드 선택 편집 → MobileModSceneUiActions.PickAndOpenModForEditing
최근 모드 편집      → MobileModSceneUiActions.OpenRecentModForEditing
다시 내보내기       → MobileModSceneUiActions.ExportCurrentMod
편집 닫기           → MobileModSceneUiActions.CloseCurrentModEditing
```
