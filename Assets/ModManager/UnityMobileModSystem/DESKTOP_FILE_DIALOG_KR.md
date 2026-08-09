# Windows / macOS 파일 선택 기능

모드 시스템은 플랫폼에 따라 다음 파일 선택 방식을 사용합니다.

```text
Android / iOS       NativeFilePicker
Unity Editor         UnityEditor.EditorUtility
Windows standalone  UnitySimpleFileBrowser 또는 UnityStandaloneFileBrowser
macOS standalone    UnitySimpleFileBrowser 또는 UnityStandaloneFileBrowser
```

## 권장: UnitySimpleFileBrowser

최신 Unity와 Apple Silicon Mac에서는 네이티브 바이너리가 필요 없는 `UnitySimpleFileBrowser` 사용을 권장합니다. Finder/Explorer 자체 창이 아니라 게임 내부 uGUI 파일 브라우저가 표시됩니다.

Package Manager에서 **Add package from git URL**:

```text
https://github.com/yasirkula/UnitySimpleFileBrowser.git
```

## 선택 사항: UnityStandaloneFileBrowser

운영체제의 네이티브 Finder/File Explorer 창을 사용하려면 다음 프로젝트의 `StandaloneFileBrowser.unitypackage`를 임포트할 수 있습니다.

```text
https://github.com/gkngkc/UnityStandaloneFileBrowser
```

이 플러그인의 공식 1.2 릴리스는 오래되었기 때문에 Apple Silicon 또는 최신 Unity에서 네이티브 번들 호환성을 별도로 확인해야 합니다.

## 교체/추가할 파일

```text
Runtime/MobileModController.cs
Runtime/CrossPlatformFileDialog.cs
Runtime/link.xml
```

두 데스크톱 플러그인은 reflection으로 탐색하므로, 플러그인을 설치하기 전에도 모드 시스템 스크립트는 컴파일됩니다. Standalone 빌드에서 둘 다 없으면 파일 선택을 취소하고 오류를 출력합니다.

## 지원되는 기능

```text
PickAndImportModel()          GLB 선택
PickAndApplyTexture()         PNG/JPG 선택
PickAndApplyAudio()           WAV/MP3/OGG/AIFF 선택
PickAndImportSettingsText()   TXT 선택
PickAndImportModPackage()     SDGMOD 선택
PickAndOpenModForEditing()    SDGMOD 편집용 선택
ExportCurrentSettingsText()   TXT 저장 위치 선택
ExportCurrentMod()            SDGMOD 저장 위치 선택
```

`UnitySimpleFileBrowser`가 설치되어 있으면 우선 사용하고, 없으면 `UnityStandaloneFileBrowser`를 사용합니다.
