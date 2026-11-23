# 방치형 타워 디펜스 키우기(Tower Defence RPG 2D)
작성 <time datetime = "2026-11-23"> 2026-11-23 </time>

## 프로젝트 소개
2D 도트 그래픽, 탑 다운 뷰로 진행되는 방치형 RPG와 타워 디펜스를 섞은 혼합형 RPG 입니다. 

## 게임 다운로드 링크
[게임 빌드](https://drive.google.com/drive/folders/1muJJiHtBZgwKApU33YLaJKkf-An3fi7N?usp=sharing)

261123_build.zip 압축 풀기 후 Tower Defence RPG 2D.exe **(Windows)** 실행 

261123_2d_buid_v0.99.5.apk **(Android)** 다운로드 시 : 무시하고 설치, 검사 X 
	
## 게임 플레이				
### 메인 플레이 화면 상세 설명	
+ **좌측 상단** : 가이드 퀘스트, 닉네임 설정
+ **중앙 상단** : 현재 스테이지, 클리어 조건, 남은 시간
+ **우측 상단** : 재화 표시, 공지사항, 우편, 게임 설정
+ **좌측 하단** : 대화, 상점, 미션, 랭킹, 친구, 길드, 가방
+ **중앙 하단** : 오프라인 보상, 유저 레벨, 경험치
+ **우측 하단 1** : 화살표 버튼 -> 플레이어 스탯, 스탯 강화, 던전 입장, 동료 도감, 동료 배치
+ **우측 하단 2** : 게임 속도 배속

#### 부가 설명
+ **Windows 빌드 실행 시 입력 되는 키** : Esc (게임 일시 정지)  
+ **로그인 방식** : 게스트 로그인 or 이메일 로그인 **택 1** *(게스트 로그인 시 -> 추후 이메일 아이디로 연동 가능)*
+ 이메일 로그인 시 **이메일 형식** 맞춰야 **회원 가입** 가능 *(ex : asd@asd.com)*
+ 비밀번호 6자 이상 **필수**
+ 구글 로그인 **불가**
+ **닉네임 설정** : 스테이지 1-5 도달 시 닉네임 변경 1회 **무료**

## 프로젝트에 사용된 기능
### 서버
뒤끝(BackND) 콘솔을 이용하여 서버에 올린 데이터베이스로 아래 기능들을 클라이언트로 받아와 사용
 <details open>	<summary> 기능 </summary>	 
 
+ 로그인
+ 랭킹
+ 친구 추가
+ 길드
+ 우편
+ 공지사항
+ 랭킹 보상
+ 닉네임 설정
+ 동료 도감
+ 동료 뽑기
		 
</details>
	 
 ### 서버 외 기능
 <details> <summary> 메인 플레이 화면 </summary>

 + 인 게임 배속
 + 몬스터, 플레이어 공격 오브젝트 풀링
 + 알람 기능 *(플레이 시간이 n분 / n시간 되었습니다.)*
 + ScriptableObject를 사용한 스테이지 별 Enemy Prefab 다양화 
 + 타일 맵을 이용하여 동료를 배치할 수 있는 타일 표시
	 
 </details>
 
- - -
본 프로젝트에서는 Unity Store 에서 구매한 에셋과 ***생성형 Ai를 이용한 도트 그래픽*** 이 사용되었습니다.  

<details> <summary>사용된 에셋</summary>	

 + 2D Mobile Game UI Kit (https://assetstore.unity.com/packages/2d/gui/2d-mobile-game-ui-kit-355774)
 + 500 Free Skill Icons (https://assetstore.unity.com/packages/2d/gui/icons/500-free-skill-icons-344112)
 + Fantasy Free GUI (https://assetstore.unity.com/packages/2d/gui/icons/fantasy-free-gui-147682)
 + Free 2D Mega Pack (https://assetstore.unity.com/packages/p/free-2d-mega-pack-177430)
 + Free Pixel Font - Thaleah (https://assetstore.unity.com/packages/p/free-pixel-font-thaleah-140059)
 + Pixel Art Icon Pack - RPG (https://assetstore.unity.com/packages/2d/gui/icons/pixel-art-icon-pack-rpg-158343)
 + Pixel Art Top Down - Basic (https://assetstore.unity.com/packages/2d/environments/pixel-art-top-down-basic-187605)
 + Pixel Units - SPUM Bundle Pack Basic (https://assetstore.unity.com/packages/2d/characters/pixel-units-spum-bundle-pack-basic-192104)
 + Space Game GUI kit (https://assetstore.unity.com/packages/2d/gui/icons/space-game-gui-kit-298577)
 + Tiny Swords (https://assetstore.unity.com/packages/2d/environments/tiny-swords-352566)
 + 탑다운 2D RPG 에셋 팩 (https://assetstore.unity.com/packages/p/top-down-2d-rpg-assets-pack-188718)

</details>  
<details> <summary>사용된 생성형 Ai, 프로그램</summary>	

+ Aseprite : 2D 도트 구현
+ Claude : Aseprite와 연동하여 도트 스프라이트 생성
	
</details>
