# 최근 모드 저장 및 불러오기

## 저장 위치

성공적으로 임포트한 마지막 모드 파일은 다음 앱 내부 경로에 복사됩니다.

`Application.persistentDataPath/MobileModSystem/RecentMod/last_loaded.sdgmod`

원래 파일 이름은 `last_loaded_name.txt`에 저장됩니다.

새 모드는 먼저 `pending.sdgmod`로 복사됩니다. 임포트가 성공한 경우에만 기존 `last_loaded.sdgmod`를 교체하므로, 손상된 모드를 선택해도 이전 최근 모드는 유지됩니다.

## 최근 모드 버튼

1. Canvas에 Button을 생성합니다.
2. Button의 OnClick에 MobileModController가 붙은 GameObject를 연결합니다.
3. 함수에서 `MobileModController.LoadRecentModPackage()`를 선택합니다.

버튼을 최근 모드가 있을 때만 활성화하려면 Button에 `RecentModButtonState`를 추가하고 다음을 연결합니다.

- Controller: MobileModController
- Button: 현재 Button
- Label: 버튼 자식 Text (선택사항)

## 사용 가능한 함수

- `PickAndImportModPackage()` : 모드 선택, 임포트 성공 후 최근 모드 저장
- `LoadRecentModPackage()` : 저장된 최근 모드 즉시 불러오기
- `HasRecentMod()` : 최근 모드 존재 여부
- `GetRecentModOriginalFileName()` : 원래 선택한 파일 이름
- `ClearRecentModPackage()` : 최근 모드 데이터 삭제

## Inspector

MobileModController의 `Save Last Imported Mod`를 활성화합니다. 기본값은 활성화입니다.
