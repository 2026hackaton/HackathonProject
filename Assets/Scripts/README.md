# 상하차 - Unity 2D 버전

3D에서 겪은 Character/Box 레이어 마스크 설정 누락, Layer Collision Matrix 위치
헷갈림 같은 문제를 구조적으로 없앤 버전입니다. **레이어도, 마스크도, 충돌
매트릭스도 하나도 안 건드립니다.** 대신:

- 살아있는 캐릭터 전부 → `CharacterState.All` (static 리스트)
- 필드에 있는 상자 전부 → `BoxItem.All` (static 리스트)
- 배송구역 판정 → 트리거 이벤트 대신 `DeliveryZone.Contains(좌표)` 직접 호출

밀치기/줍기/납품 전부 이 세 가지로만 판정합니다. Physics2D 설정 창을 열 일이
거의 없습니다.

## 조작

| 입력 | 동작 |
|---|---|
| WASD | 월드 기준 이동 |
| 마우스 | 그 방향을 바라봄 (조준) |
| 우클릭 | 근처에 상자 있으면 줍기 / 없으면 제자리 밀치기 (대시 없음) |
| 좌클릭 누르기 | 상자를 들고 있을 때만 유효, 차징 시작 |
| 좌클릭 떼기 | 조준 방향으로 던지기 |

## 상자 4종

- **Normal**: 기본
- **Bomb**: 충격(던져서 착지 / 밀쳐서 놓침) 받으면 폭발. 배송구역 안으로 공중에서
  바로 들어가면 터지기 전에 안전하게 납품됩니다.
- **Return**: 배송구역에 넣어도 `returnCyclesRequired`(기본 1회)만큼 다시
  걸어나갑니다.
- **Creature**: 바닥(Ground)에 있을 때 스스로 배회합니다.

## 씬 설정 체크리스트

### 카메라
Main Camera의 `Projection`을 **Orthographic**으로 변경. `TopDownCameraFollow`
붙이고 `target`에 Player 연결.

### 캐릭터 프리팹 (플레이어 1 + CPU 3)
- **Rigidbody2D** — Body Type: Dynamic, Gravity Scale: 0 (스크립트가 Awake에서
  자동으로 0으로 맞추긴 하지만 인스펙터에서도 0으로 해두면 헷갈리지 않음)
- **CircleCollider2D** (또는 원하는 모양) — 서로 부딪혀서 밀려나는 느낌을 위해
  필요. Is Trigger 체크 안 함
- `CharacterState` 컴포넌트
- 플레이어: `PlayerController` 추가
- CPU 3명: `CPUAgent` 추가
- 자식으로 빈 오브젝트 `HandSocket` 추가 → `CharacterState.handSocket`에 연결
- SpriteRenderer로 캐릭터 스프라이트 표시 (자유롭게)

레이어는 전부 Default로 둬도 됩니다. 마스크 필드가 스크립트 어디에도 없습니다.

### 상자 프리팹 4종 (Normal / Bomb / Return / Creature)
- **Collider2D** 아무거나 하나 (물리 충돌용은 아니고, 있어도 없어도 게임플레이엔
  지장 없음 — 넣어두면 씬뷰에서 크기 파악하기 편함)
- `BoxItem` 컴포넌트, `boxType` 설정
- SpriteRenderer로 상자 스프라이트 표시. 공중에 떠 보이게 하고 싶으면 자식
  오브젝트를 하나 만들어서 그 안에 스프라이트를 넣고, `BoxItem.visualPivot`에
  연결 (던지는 동안 위아래로 움직이는 연출은 이 자식 오브젝트의 로컬 Y가 담당)
- 타입별 색 다르게 (빨강=폭탄, 파랑=반품, 초록=생물 추천)

### 배송구역
- 빈 오브젝트에 **Collider2D 아무거나 하나**(트리거 체크 여부 상관없음) +
  `DeliveryZone` 컴포넌트. Rigidbody2D도 필요 없음

### 트럭 스폰 지점
- 빈 오브젝트에 `TruckSpawnPoint`, 상자 프리팹 4종 연결

### GameManager
- 빈 오브젝트에 `GameManager`, `characters` 리스트에 플레이어+CPU 3명 등록
  (점수 집계용. 게임플레이 판정 자체는 `CharacterState.All`을 쓰므로 이 리스트를
  비워놔도 밀치기/줍기/납품은 그대로 동작하지만, 점수는 안 쌓입니다)

### UI (선택)
`MatchUI`는 최소 예시입니다. Text(또는 TMP_Text)를 인스펙터에 연결해서 쓰세요.

## Transform 좌표 예시 (2D는 Z=0 고정)

| 오브젝트 | Position |
|---|---|
| Player | (0, 0, 0) |
| CPU-1 | (-3, 3, 0) |
| CPU-2 | (-3, -3, 0) |
| CPU-3 | (1, 5, 0) |
| TruckSpawnPoint | (-8, 0, 0) |
| DeliveryZone | (8, 0, 0), Collider2D 크기 6×10 |
| Main Camera | (0, 0, -10) |

## Input 설정
레거시 Input Manager 기준(`Input.GetAxisRaw`, `Input.GetMouseButtonDown` 등).
Project Settings → Player → Active Input Handling이 "Input Manager (Old)" 또는
"Both"여야 합니다.

## 밸런스 메모
스턴 2초 / 밀치기 쿨타임 2초 / 기절 회복 직후 무적 0.6초 — 지난번에 얘기한
콤보락 방지 값 그대로 가져왔습니다. `CharacterState`의 `staggerDuration` /
`postRagdollInvuln`에서 조절하세요.
