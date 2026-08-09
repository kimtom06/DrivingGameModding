# v2.5 AudioStorage / 여러 AudioSource

## 생성 계층

GLB를 불러오면 다음 계층이 자동 생성됩니다.

```text
BuildRoot
└── ModelName                 (ModNode + RuntimeModelBinding)
    ├── __Model               (glTFast가 생성한 모델)
    └── AudioStorage          (ModNode)
        ├── AudioSource 1
        ├── AudioSource 2
        └── AudioSource 3 ...
```

`PickAndApplyAudio()`를 호출할 때마다 `AudioStorage`에 새로운 `AudioSource`가 추가됩니다. 기존 오디오는 덮어쓰지 않습니다.

## Inspector

`MobileModController.mainModelRoot`는 GLB를 가져올 때 자동으로 최신 모델 루트로 설정됩니다. 직접 만든 모델 루트를 사용하려면 Inspector에 연결하거나 다음을 호출합니다.

```csharp
controller.SetMainModelRoot(modelRoot);
```

## 여러 오디오 접근

```csharp
GameObject storage = controller.GetOrCreateAudioStorage(controller.mainModelRoot);
AudioSource[] sources = storage.GetComponents<AudioSource>();

foreach (AudioSource source in sources)
{
    Debug.Log(source.clip != null ? source.clip.name : "No Clip");
}
```

## 모드팩 저장

모드 패키지 버전은 3으로 증가했습니다. `AudioStorage`에 붙은 모든 `RuntimeAudioBinding`과 `AudioSource`가 `manifest.json`의 `audios` 리스트에 기록됩니다.

v3 Importer는 기존 v1/v2 모드팩의 단일 `audio` 필드도 계속 불러옵니다. 반대로 구형 v2 Importer는 v3 모드팩을 불러올 수 없으므로 Exporter와 Importer를 함께 교체해야 합니다.

## 반드시 함께 교체할 파일

```text
MobileModController.cs
RuntimeModAssetImporter.cs
RuntimeAudioBinding.cs
ModPackageData.cs
ModPackageExporter.cs
ModPackageImporter.cs
```
