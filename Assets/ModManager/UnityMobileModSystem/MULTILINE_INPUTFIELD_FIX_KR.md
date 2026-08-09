# ModTextConfigInputFieldBridge 여러 줄 표시 수정

## 원인

Legacy Unity UI InputField의 Line Type이 Single Line이면 설정 텍스트에 실제 줄바꿈이 있어도 한 줄 입력창처럼 표시됩니다.

## 자동 적용되는 설정

- Content Type: Standard
- Line Type: Multi Line Newline
- Character Limit: 0
- Horizontal Overflow: Wrap
- Vertical Overflow: Overflow
- Alignment: Upper Left

## UI 크기

스크립트가 여러 줄 동작을 활성화해도 InputField의 RectTransform 높이가 작으면 한 줄만 보일 수 있습니다.
InputField 높이를 충분히 늘리고, 기본 InputField 하위의 Text Area와 Text 오브젝트를 삭제하지 마세요.
