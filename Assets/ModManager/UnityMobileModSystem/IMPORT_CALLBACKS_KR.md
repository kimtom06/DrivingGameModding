# 모드 임포트 완료 콜백

## Inspector 이벤트

`MobileModController`에 다음 이벤트가 추가되었습니다.

- `On Mod Package Import Completed`: `PickAndImportModPackage` 성공 시 호출
- `On Recent Mod Package Load Completed`: `LoadRecentModPackage` 성공 시 호출
- `On Any Mod Import Completed`: 위 두 방식 중 하나가 성공할 때마다 호출

각 이벤트는 생성된 모드 루트 `GameObject`를 전달합니다. 취소, 파일 없음, 압축 오류, 임포트 실패 시에는 호출되지 않습니다.

## C# 콜백

```csharp
MobileModController.Instance.PickAndImportModPackage(importedRoot =>
{
    Debug.Log("모드 불러오기 완료: " + importedRoot.name);
});

MobileModController.Instance.LoadRecentModPackage(importedRoot =>
{
    Debug.Log("최근 모드 불러오기 완료: " + importedRoot.name);
});
```

## 씬 로컬 UI 이벤트

`MobileModSceneUiActions`에도 다음 이벤트가 추가되었습니다.

- `On Pick Import Completed`
- `On Recent Import Completed`

씬 안의 UI나 게임 로직을 연결할 때 사용할 수 있습니다.
