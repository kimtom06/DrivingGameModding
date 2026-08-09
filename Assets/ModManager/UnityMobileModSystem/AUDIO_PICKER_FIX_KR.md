# v2.3 오디오 선택 수정

- `selectedAudioObject`가 비어 있어도 파일 선택창이 열립니다.
- 대상이 비어 있으면 `buildRoot` 아래에 새 `ModNode`를 자동 생성합니다.
- NativeFilePicker에 MIME/UTI 필터를 전달하지 않고 모든 파일을 표시합니다.
- 선택 후 `.wav`, `.mp3`, `.ogg`, `.aif`, `.aiff` 확장자를 직접 검사합니다.
- 선택 취소, 파일 없음, 미지원 확장자 상태 메시지를 추가했습니다.
