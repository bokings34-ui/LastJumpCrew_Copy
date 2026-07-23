GameCore : 데이터 관리자

Sample Prefabs : TradeStation & ItemSpawner 와 Credit UI 연결 예시

TradeStation:
- 현재 구매기능만 구현됨
- CheckoutDetector.cs와 CheckoutButton.cs 가 UtilityItemData를 인식하는 구조
- TradeTriggerZone에 붙어있는 UtilityConnect.cs가
UtilityItemData (int 기반)와 UtilityItemPrefabData (string 기반)를 연결해서 조회

ItemSpawner:
-CanBuy가 있는 아이템만 생성