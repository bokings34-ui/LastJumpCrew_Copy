/*

# Fire Presentation Bundle

## 개요

함선 화재 사고(EventId: Fire, 7101)의 시각/청각 표현을 담당하는 Presentation-only Bundle입니다.
서버 상태 판정(점화, Heat/Intensity, 확산, 피해, 소화)은 포함하지 않으며,
외부에서 상태값을 넘겨주면 그에 맞는 연출만 재생합니다.

## 구성
- FirePresentation_Root (Prefab)
  - FirePresentationController (스크립트, 외부 호출 진입점)
  - FireMesh (Mesh Renderer + TuriShader Material)
  - Point Light
  - Audio_FireLoop (AudioSource)

셰이더: TuriShader(ParticleSystem 미사용, Mesh + Material + Light 기반)

## 사용 방법 (외부 호출 API)

| 함수 | 호출 시점 | 설명 |
| ---|      ---|      ---|
| Telegraph() | 화재 발생 직전 경고 | 은은한 조명/노출 연출 시작
| Activate(FireIntensity intensity) | 화재 실제 발생 확정 시 | 항상 FireIntensity.Small로 시작. 불꽃·오디오 재생 시작
| SetIntensity(FireIntensity intensity) | Heat/Intensity 변화 시 호출 | Small/Medium/Large 전환
| Extinguish() | 소화 완료 시 호출 | 서서히 잦아들며 완전히 꺼짐
| ResetPresentation() | 오브젝트 재사용(풀링) 전 | 모든 시각/청각 요소 즉시 초기화

## 재사용(풀링) 관련
이 프리팹은 재사용 가능하도록 설계되었습니다.
동일 인스턴스를 다시 사용하기 전, 반드시 `ResetPresentation()`을 호출해주세요.
호출 시 진행 중이던 DoTween 트윈이 즉시 정리되고, 오디오/조명이 초기 상태로 돌아갑니다.
※ 별도의 Pool 관리 스크립트는 포함하지 않았습니다.
  Patch/Snapshot 시스템에서 재사용 여부를 결정해주시면 됩니다.

## 주의사항
- 반드시 씬에 배치된 인스턴스를 조작해야 합니다.
  Project 창의 프리팹 원본(에셋)을 직접 참조하면 Material 접근 에러가 발생합니다.
- Small/Medium/Large 각 단계별 수치(조명, 스케일, 색상, 볼륨)는
  FirePresentationController 인스펙터에서 직접 조정 가능합니다 (코드 수정 불필요).

## 확인 필요 사항
1. Heat/Intensity → Small/Medium/Large 매핑 기준이 이미 있으신지, 저희 쪽에서 3단계 구간을 나눠야 하는지
2. 재사용(풀링) 주체가 Patch/Snapshot 시스템인지, 별도 Pool이 필요한지

## 테스트 완료 사항
- Telegraph → Activate(Small) → SetIntensity(Medium) → SetIntensity(Large) → Extinguish → ResetPresentation
  전체 사이클 반복 실행 후 Console 에러 0, 오디오/조명 잔존 없음 확인
- GIF 첨부: (전체 사이클 연출 영상)

## Manifest
manifest.json 참고 (owner, bundleType, contentId, inputs 등) */
