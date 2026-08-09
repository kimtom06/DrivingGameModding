# LoadRecentModPackage 완료 콜백

`LoadRecentModPackage` 성공 후 사용할 수 있는 콜백은 세 가지입니다.

## 1. 호출 시 일회성 콜백

```csharp
MobileModController.Instance.LoadRecentModPackage(importedRoot =>
{
    Debug.Log(importedRoot.name);
});
```

## 2. C# 이벤트 구독

매개변수 없는 `LoadRecentModPackage()`를 UI Button에서 호출해도 실행됩니다.

```csharp
private void OnEnable()
{
    MobileModController.Instance.RecentModPackageLoadCompleted += OnRecentLoaded;
}

private void OnDisable()
{
    if (MobileModController.HasInstance)
        MobileModController.Instance.RecentModPackageLoadCompleted -= OnRecentLoaded;
}

private void OnRecentLoaded(GameObject importedRoot)
{
    Debug.Log("최근 모드 완료: " + importedRoot.name);
}
```

## 3. Inspector UnityEvent

`MobileModController.onRecentModPackageLoadCompleted`에 Dynamic GameObject 함수를 연결합니다.

모드 파일이 없거나, 씬 참조 대기 실패, 압축 해제/모델/오디오 임포트 실패 시에는 완료 콜백이 실행되지 않습니다.
