# v3.3 Windows/macOS 파일 선택 기능

- Unity Editor에서 PC/Mac 파일 선택 및 저장 창 지원
- Windows standalone에서 파일 선택 및 저장 위치 선택 지원
- macOS standalone에서 파일 선택 및 저장 위치 선택 지원
- Android/iOS에서는 기존 NativeFilePicker 유지
- GLB, 이미지, 오디오, TXT, SDGMOD 선택을 하나의 CrossPlatformFileDialog로 통합

Windows/macOS standalone 빌드에는 `UnitySimpleFileBrowser` 또는 `UnityStandaloneFileBrowser`가 필요합니다.
자세한 내용은 `DESKTOP_FILE_DIALOG_KR.md`를 참고하세요.

---

# v2.9 추가 사항

- `MobileModController.Instance` 싱글톤
- `DontDestroyOnLoad`로 씬 전환 후 유지
- 중복 매니저 자동 삭제
- 각 씬의 `MobileModSceneReferences` 자동 탐색
- `importedModsParent`가 비어 있으면 참조 컴포넌트의 Transform 사용
- 참조 컴포넌트가 없으면 선택적으로 `ImportedMods` 자동 생성

자세한 내용은 `SINGLETON_SCENE_REFERENCES_KR.md`를 참고하세요.

---

# Unity 모바일 런타임 모드 시스템 v2

## 지원 범위
- 3D 모델: `.glb`
- 텍스처: `.png`, `.jpg`, `.jpeg`
- 사운드: `.wav`, `.ogg`, `.mp3`, `.aif/.aiff`
- 사용자 설정: UTF-8 `.txt`, `key=value` 형식
- 모드 패키지: `.sdgmod` (실제 내부 형식은 ZIP)
- 저장 대상: `manifest.json`, 계층구조/Transform, GLB 원본, 텍스처 바인딩, AudioSource 설정, 사용자 설정 텍스트

## v2에서 추가된 설정 텍스트 기능

`.sdgmod` 안에 다음 파일이 자동으로 포함됩니다.

```text
MyMod.sdgmod
├── manifest.json
└── assets
    ├── ModelGlb
    ├── Texture
    ├── Audio
    └── ConfigText
        └── <id>.txt
```

`manifest.json`의 `settingsAssetId`가 설정 텍스트 에셋을 가리킵니다.

### 기본 파일 형식

`Samples/DefaultModSettings.txt`를 Unity 프로젝트로 복사한 뒤 `MobileModController.defaultSettingsFile`에 연결할 수 있습니다.

```text
# SDGMOD 사용자 설정 파일
# 형식: key=value

mod.name={{MOD_NAME}}
mod.author={{AUTHOR}}
mod.category=custom

object.displayName={{MOD_NAME}}
object.enabled=true
object.uniformScale=1.0
spawn.position=0,0,0
spawn.rotation=0,0,0

audio.volume=1.0
custom.note=
```

규칙:
- `#` 또는 `;`로 시작하는 줄은 주석입니다.
- 키에는 영문자, 숫자, `.`, `_`, `-`만 사용할 수 있습니다.
- 값에는 `=`가 들어갈 수 있으며 첫 번째 `=`만 구분자로 사용합니다.
- 소수점은 현재 기기 언어와 무관하게 `.`을 사용합니다.
- Vector3 값은 `x,y,z`로 작성합니다.
- 같은 키가 여러 번 있으면 가장 마지막 값을 사용합니다.
- 기본 템플릿의 `{{MOD_NAME}}`, `{{AUTHOR}}`는 초기 생성 시 Controller 값으로 치환됩니다.
- 최대 크기는 UTF-8 기준 256KB입니다.
- 텍스트는 데이터로만 처리하며 코드로 실행하지 않습니다.

## 필요한 패키지
1. Package Manager > Add package by name
   - `com.unity.cloud.gltfast`
2. Package Manager > Add package from git URL
   - `https://github.com/yasirkula/UnityNativeFilePicker.git`
3. Windows/macOS standalone 파일 창 — 권장
   - Package Manager Git URL: `https://github.com/yasirkula/UnitySimpleFileBrowser.git`
4. 네이티브 Windows/macOS 파일 창 — 선택 사항
   - `UnityStandaloneFileBrowser` unitypackage 임포트
   - 저장소: `https://github.com/gkngkc/UnityStandaloneFileBrowser`
5. `ModTextConfigInputFieldBridge.cs`를 사용할 경우 Unity UI 패키지 필요
   - 일반적으로 Unity 프로젝트에 기본 설치되어 있습니다.

## 씬 설정
1. 빈 오브젝트 `MobileModManager` 생성
2. 다음 컴포넌트 추가
   - `MobileModController`
   - `RuntimeModAssetImporter`
   - `ModPackageExporter`
   - `ModPackageImporter`
3. 빈 오브젝트 `BuildRoot` 생성 후 `ModNode` 추가
4. `MobileModController.buildRoot`에 `BuildRoot` 연결
5. 불러온 모드의 부모가 될 `ImportedMods` 생성 후 `importedModsParent`에 연결
6. `Samples/DefaultModSettings.txt`를 프로젝트로 복사하고 `defaultSettingsFile`에 연결

## UI 버튼 연결

| 기능 | MobileModController 함수 |
|---|---|
| 모델 선택 | `PickAndImportModel()` |
| 텍스처 선택 | `PickAndApplyTexture()` |
| 사운드 선택 | `PickAndApplyAudio()` |
| 설정 TXT 선택 | `PickAndImportSettingsText()` |
| 설정 TXT 별도 저장 | `ExportCurrentSettingsText()` |
| 설정 기본값 복원 | `ResetCurrentSettingsText()` |
| 모드팩 저장 | `ExportCurrentMod()` |
| 모드팩 불러오기 | `PickAndImportModPackage()` |

## 앱 내부에서 사용자가 텍스트 수정하기

### 방법 1: 제공된 InputField 브리지

1. Canvas에 여러 줄 `InputField` 생성
2. 빈 오브젝트에 `ModTextConfigInputFieldBridge` 추가
3. `controller`와 `inputField` 연결
4. InputField의 `Line Type`을 `Multi Line Newline`으로 설정

브리지가 다음 처리를 자동으로 합니다.
- 실행 시 기본 설정 텍스트를 InputField에 표시
- 사용자가 입력할 때 `RuntimeModTextConfig` 갱신
- 외부 `.txt`를 불러오거나 초기화할 때 InputField 내용 갱신
- UI 갱신 시 `SetTextWithoutNotify`를 사용하여 무한 이벤트 호출 방지

### 방법 2: TMP_InputField 직접 연결

TMP_InputField를 사용하는 경우 `OnValueChanged(string)`을 다음 함수에 연결합니다.

```csharp
MobileModController.SetCurrentSettingsText(string)
```

현재 텍스트를 TMP UI로 다시 표시하려면 `onSettingsTextChanged` 이벤트를 받고 `SetTextWithoutNotify`를 호출하는 간단한 브리지를 작성합니다.

## 코드에서 현재 제작 중인 설정 변경

```csharp
controller.SetCurrentSettingValue("object.uniformScale", "1.5");
controller.SetCurrentSettingValue("audio.volume", "0.7");
```

또는 전체 텍스트를 교체할 수 있습니다.

```csharp
controller.SetCurrentSettingsText(myInputField.text);
```

모드팩을 내보낼 때 `BuildRoot`의 `RuntimeModTextConfig.TextContent`가 자동으로 포함됩니다.

## 모드 임포트 후 설정 값 읽기

불러온 루트 오브젝트에는 `RuntimeModTextConfig`가 자동으로 추가됩니다.

```csharp
public void OnModImported(GameObject importedRoot)
{
    RuntimeModTextConfig config =
        importedRoot.GetComponent<RuntimeModTextConfig>();

    if (config == null)
        return;

    string category = config.GetString("mod.category", "custom");

    if (config.TryGetFloat("object.uniformScale", out float scale))
        importedRoot.transform.localScale *= scale;

    if (config.TryGetBool("object.enabled", out bool enabled))
        importedRoot.SetActive(enabled);

    if (config.TryGetVector3("spawn.position", out Vector3 position))
        importedRoot.transform.localPosition = position;
}
```

지원되는 읽기 함수:

```csharp
config.GetString("key", "fallback");
config.TryGetString("key", out string value);
config.TryGetInt("key", out int value);
config.TryGetFloat("key", out float value);
config.TryGetBool("key", out bool value);
config.TryGetVector3("key", out Vector3 value);
config.GetAllValues();
config.ContainsKey("key");
```

## 임포트 직후 프로젝트 전용 값 적용

### 방법 1: onObjectCreated 사용

`MobileModController.onObjectCreated`에 프로젝트 전용 적용 함수를 연결합니다.

```csharp
public void ApplyModSettings(GameObject importedRoot)
{
    RuntimeModTextConfig config =
        importedRoot.GetComponent<RuntimeModTextConfig>();

    if (config == null)
        return;

    if (config.TryGetFloat("vehicle.maxSpeed", out float maxSpeed))
    {
        // 프로젝트의 자동차 컨트롤러에 적용
        // carController.maxspeed = Mathf.Clamp(maxSpeed, 10f, 500f);
    }
}
```

`Runtime/ModSettingsExampleApplier.cs`에는 이름, 크기, 위치, 회전, 오디오 볼륨, 활성 상태를 적용하는 예제가 들어 있습니다.

### 방법 2: onImportedSettings 사용

텍스트 설정만 바로 받고 싶은 경우 다음 이벤트를 사용합니다.

```csharp
MobileModController.onImportedSettings
```

연결 함수 예시:

```csharp
public void ReadImportedSettings(RuntimeModTextConfig config)
{
    Debug.Log(config.GetString("mod.category", "custom"));
}
```

`onImportedSettings`와 `onImportedSettingsText`가 호출된 뒤 `onObjectCreated`가 호출됩니다.
`onSettingsTextChanged`는 현재 제작 중인 `BuildRoot` 편집 UI에만 사용됩니다.

## 외부 텍스트 편집 흐름

```text
앱에서 기본 설정 생성
→ ExportCurrentSettingsText()
→ 모바일 파일 앱/텍스트 편집기로 수정
→ PickAndImportSettingsText()
→ 앱 내부에서 값 확인 또는 추가 수정
→ ExportCurrentMod()
→ 설정 TXT가 포함된 .sdgmod 생성
```

## 텍스처 적용
- 런타임 선택 시스템에서 `selectedTextureRenderer`를 현재 선택한 Renderer로 갱신합니다.
- URP Lit 기본 텍스처는 `_BaseMap`, Built-in Standard는 `_MainTex`입니다.
- 프로퍼티가 없으면 코드가 `_BaseMap`과 `_MainTex`를 순서대로 탐색합니다.

## 버전 호환성
- 새로 내보낸 모드팩 버전: `2`
- 임포터 지원 버전: `1`, `2`
- 기존 v1 모드를 불러오면 설정 텍스트가 없으므로 기본 설정 컴포넌트를 자동 생성합니다.
- v2 모드는 사용자 설정 텍스트를 그대로 복원합니다.

## 중요한 설계 제한
- 빌드된 모바일 앱에서 Unity Prefab 에셋을 새로 생성할 수 없으므로 데이터 패키지 방식입니다.
- 임의의 MonoBehaviour, DLL, C# 코드는 저장하거나 실행하지 않습니다.
- GLB 내부 자동 생성 오브젝트에는 `ModNode`가 없으므로 계층 중복 저장에서 제외됩니다.
- 사용자가 추가한 빈 오브젝트/자식 오브젝트에는 반드시 `ModNode`가 있어야 내보내집니다.
- GLTF 대신 GLB만 허용하여 외부 `.bin`/텍스처 파일 누락 문제를 방지합니다.
- 설정 텍스트의 키가 실제 게임 기능에 적용되는 방법은 게임 프로젝트가 직접 허용 목록을 정해야 합니다.

## 설정값 적용 시 보안 권장사항

사용자 설정값을 그대로 믿지 말고 범위를 제한합니다.

```csharp
if (config.TryGetFloat("vehicle.maxSpeed", out float maxSpeed))
    maxSpeed = Mathf.Clamp(maxSpeed, 10f, 500f);
```

권장 사항:
- 허용할 키를 코드에 명시
- 속도, 크기, 볼륨, 개수 등의 최소/최대값 제한
- 파일 경로나 URL을 설정값에서 직접 실행하지 않기
- 클래스 이름을 읽어 `AddComponent`하지 않기
- Reflection으로 임의 메서드를 호출하지 않기

## iOS
- NativeFilePicker의 Custom Types에 `sdgmod` 확장자를 등록합니다.
- iOS에서 선택한 임시 파일은 앱 종료 후 사라질 수 있으므로 모델/텍스처/사운드/설정 TXT는 선택 직후 `persistentDataPath/ModWorkspace`로 복사합니다.

## Android
- 사용자 정의 확장자 MIME 필터는 문서 공급자마다 달라 `.sdgmod` 선택 시 전체 파일을 표시합니다.
- `.txt`는 `text/plain`으로 선택합니다.
- 실제 모드 파일은 manifest의 magic/version 및 ZIP 경로 검증을 통과해야 로드됩니다.

## 보안
- 기본 제한: 패키지 100MB, 압축 해제 250MB, ZIP 엔트리 512개
- 설정 텍스트 최대 256KB
- Zip Slip 경로 차단
- 에셋 종류 및 manifest 버전 검사
- 코드 모드 미지원

## v2.1 수정 사항

- `ModNode`, `RuntimeModelBinding`, `RuntimeTextureBinding`, `RuntimeAudioBinding`을 각각 클래스명과 동일한 `.cs` 파일로 분리했습니다.
- Unity Add Component 메뉴에서 `ModNode`가 정상적으로 검색됩니다.
- Unity 6에서 `CompressionLevel` 이름 충돌이 발생하지 않도록 `System.IO.Compression.CompressionLevel`을 명시했습니다.

---

## v2.5: AudioStorage 다중 오디오

GLB 모델 루트 아래에 `AudioStorage`가 자동 생성됩니다. `PickAndApplyAudio()`를 호출할 때마다 같은 `AudioStorage`에 새로운 `AudioSource`와 `RuntimeAudioBinding`이 추가되며, 내보내기/불러오기 시 모든 오디오가 유지됩니다. 자세한 내용은 `MULTI_AUDIO_STORAGE_KR.md`를 확인하세요.

---

## v2.7 최근 모드

성공적으로 불러온 마지막 `.sdgmod`를 `Application.persistentDataPath` 아래에 보관합니다.
최근 모드 버튼의 OnClick에는 `MobileModController.LoadRecentModPackage()`를 연결하세요.
자세한 내용은 `RECENT_MOD_KR.md`를 확인하세요.


# 기존 모드를 다시 편집하기

## 버튼 연결

기존 모드 파일을 선택해 편집하려면 버튼의 `OnClick()`에 다음 함수를 연결합니다.

```text
MobileModController.Instance.PickAndOpenModForEditing()
```

최근 저장된 모드를 바로 편집하려면:

```text
MobileModController.Instance.OpenRecentModForEditing()
```

편집을 닫으려면:

```text
MobileModController.Instance.CloseCurrentModEditing()
```

## 동작 구조

```text
.sdgmod 선택
→ 안전하게 압축 해제
→ GLB/오디오/설정 텍스트를 ModWorkspace에 영구 복사
→ 원래 ModNode 계층과 PersistentId 복원
→ 불러온 모드 루트를 currentEditableModRoot로 지정
→ 불러온 모드 루트를 buildRoot로 전환
→ 사용자 편집
→ ExportCurrentMod()로 다시 내보내기
```

일반 `PickAndImportModPackage()`는 게임 플레이용으로 `ImportedMods` 아래에 생성합니다.
`PickAndOpenModForEditing()`는 편집용으로 열며, 불러온 루트 자체가 내보내기 대상이 됩니다.

## 씬 참조

`MobileModSceneReferences`에 선택적으로 `Edit Workspace Parent`를 연결할 수 있습니다.
비어 있으면 `Build Root`, 그것도 없으면 참조 컴포넌트가 붙은 오브젝트를 사용합니다.

권장 구조:

```text
MobileModSceneReferences
├── BuildRoot
├── EditWorkspace
└── ImportedMods
```

## 보존되는 항목

- 계층구조와 Transform
- ModNode PersistentId
- GLB 원본 파일 바인딩
- 다중 AudioSource와 오디오 원본 파일 바인딩
- 별도 텍스처 바인딩
- 사용자 설정 텍스트
- 원래 modId
- 모드 이름과 작성자

GLB 내부 자식에 직접 추가한 임의 컴포넌트나 GLB 메시 자체의 런타임 변경은 모드 포맷에 기록되지 않습니다. 저장하려는 새 그룹은 `ModNode`로 생성해야 합니다.

후속 씬의 UI Button에는 `MobileModSceneUiActions`를 사용하면 싱글톤을 Inspector에서 직접 참조하지 않아도 됩니다.

## v3.0: 모드 임포트 완료 콜백

`PickAndImportModPackage`와 `LoadRecentModPackage`가 성공적으로 완료됐을 때만 호출되는 전용 UnityEvent와 C# 콜백이 추가되었습니다. 자세한 내용은 `IMPORT_CALLBACKS_KR.md`를 참고하세요.

## v3.4 Standard material conversion

GLB 생성 직후 glTFast 머티리얼을 Built-in Render Pipeline의 `Standard` 머티리얼로 변환할 수 있습니다.
`RuntimeModAssetImporter`에서 `Convert Imported Materials To Standard`를 활성화하고 `Standard Shader`를 지정하세요.
URP/HDRP 프로젝트에서는 이 옵션을 사용하지 마세요.
