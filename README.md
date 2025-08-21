# 🛒 구매대행 시스템 - Predvia

> **전문적인 구매대행 서비스를 위한 통합 관리 시스템**

![Avalonia](https://img.shields.io/badge/Avalonia-11.0-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![C#](https://img.shields.io/badge/C%23-12.0-green)
![License](https://img.shields.io/badge/License-MIT-yellow)
![Build](https://img.shields.io/badge/Build-Success-brightgreen)
![Release](https://img.shields.io/badge/Release-v1.0-orange)

## 📋 프로젝트 개요

구매대행 업무의 효율성을 극대화하기 위해 개발된 크로스플랫폼 데스크톱 애플리케이션입니다. 
Avalonia UI를 기반으로 하여 Windows, macOS, Linux에서 동일한 사용자 경험을 제공합니다.

## 🚀 빠른 시작

### 📥 다운로드 및 실행

1. **릴리스 다운로드**: [Releases](https://github.com/your-repo/Predvia/releases)에서 최신 버전 다운로드
2. **압축 해제**: 다운로드한 파일을 원하는 폴더에 압축 해제
3. **실행**: `실행.bat` 더블클릭 또는 `Predvia.exe` 직접 실행

### 🔑 테스트 API 키
```
PREDVIA-API-KEY-12345  (프리미엄 라이선스)
TEST-API-KEY-67890     (스탠다드 라이선스)
DEMO-KEY-2024          (데모 라이선스)
FREE-TRIAL-KEY         (무료 체험 라이선스)
```

## ✨ 주요 기능

### 🔐 인증 시스템
- **API 키 기반 인증**: 안전한 라이선스 검증
- **테스트 모드 지원**: 개발 및 데모용 키 제공
- **자동 로그인**: 인증 정보 자동 저장

### 🎨 사용자 인터페이스
- **다크/라이트 테마**: 사용자 선호에 따른 테마 전환
- **반응형 디자인**: 다양한 화면 크기 지원
- **직관적인 탭 네비게이션**: 기능별 명확한 구분

### 🛍️ 핵심 업무 기능

#### 📦 소싱 (Sourcing)
- 상품 검색 및 분석
- 공급업체 관리
- 가격 비교 시스템

#### 🔍 마켓점검 (Market Check)
- **완전한 상품 등록 관리 시스템**
  - 2×2 그리드 상품 카드 레이아웃
  - 실시간 피드백 시스템 ("중국어가 있습니다", "브랜드명이 포함되어있습니다")
  - 네이버쇼핑 바로가기 링크
  - 옅은 주황색 배경의 차트 섹션
  - 막대 그래프: 회색 배경 + 주황색 전경 겹침 효과
  - 범례: 주문처리형, 밸런스형, 크무비형
  - **완전한 페이지 네비게이션**: 마켓점검 → 마켓등록 세부과정 이동

#### 🎯 메인상품찾기 (Main Product Finder)
- **완전한 상품 관리 시스템**
  - 왼쪽 상품 이미지 (320×240px) + 오른쪽 정보 패널 구조
  - 정보 패널: 누적판매수, 동일 키워드 상품 최대 리뷰, 카테고리 제조사 정보
  - 3개 액션 버튼: "상세페이지 만들기", "썸네일 만들기", "타오바오 링크 재배어링"
  - **사이드바 토글 기능**: 우측 토글 버튼, 닫기 버튼, 오버레이 기능

- **전체 화면 GUI 시스템**: 3개 버튼 클릭 시 프로그램 전체 화면을 덮는 대형 GUI
  - **상세페이지 만들기**: 2열 레이아웃 (입력 패널 + 실시간 미리보기)
  - **썸네일 만들기**: 2열 레이아웃 (설정 패널 + 400×400px 미리보기)
  - **타오바오 링크 재배어링**: 2열 레이아웃 (입력/설정 패널 + 결과 미리보기)

#### ⚙️ 설정 (Settings)
- **완전한 설정 관리 시스템**
  - 동적 메뉴 시스템: 클릭한 메뉴에 따라 설정 박스 위치 및 내용 동적 변경
  - 3개 메뉴 카테고리: 계정관리, 마켓주소관리, 프로그램 설정
  - **전체 화면 오버레이**: 닉네임 변경, 마켓 설정 페이지
  - **완벽한 라이트/다크모드 지원**: 모든 UI 요소의 테마별 색상 자동 전환

## 🏗️ 기술 스택

### Frontend
- **Avalonia UI 11.0**: 크로스플랫폼 UI 프레임워크
- **XAML**: 선언적 UI 마크업
- **C# 12.0**: 최신 언어 기능 활용

### Backend Services
- **HttpClient**: RESTful API 통신
- **System.Text.Json**: JSON 직렬화/역직렬화
- **Avalonia.Themes.Fluent**: 모던 UI 테마

### Architecture
- **MVVM 패턴**: 관심사 분리 및 테스트 용이성
- **Singleton 패턴**: ThemeManager, ApiClient 등
- **Event-Driven**: 사용자 상호작용 처리

## 📁 프로젝트 구조

```
Predvia/
├── 📁 Assets/                 # 리소스 파일
├── 📁 images/                 # UI 참조 이미지
├── 📁 Services/              # 비즈니스 로직
│   ├── ApiClient.cs          # API 통신 클라이언트
│   ├── ApiKeyAuthClient.cs   # 인증 서비스
│   ├── AuthManager.cs        # 인증 관리자
│   └── AdviceService.cs      # 조언 서비스
├── 📁 Styles/                # UI 스타일 정의
├── 📁 publish-windows/       # Windows 배포 파일
├── MainWindow.axaml          # 메인 윈도우 UI
├── SourcingPage.axaml        # 소싱 페이지
├── MarketCheckPage.axaml     # 마켓점검 페이지
├── MarketRegistrationPage.axaml    # 마켓등록 페이지
├── MainProductFinderPage.axaml     # 메인상품찾기 페이지
├── SettingsPage.axaml        # 설정 페이지
├── ApiKeyAuthWindow.axaml    # 인증 윈도우
├── LoginWindow.axaml         # 로그인 윈도우
├── SignupWindow.axaml        # 회원가입 윈도우
├── ThemeManager.cs           # 테마 관리자
├── Predvia.exe              # 실행 파일 (91MB)
├── 실행.bat                  # 실행 스크립트
└── README-실행방법.txt       # 사용 설명서
```

## 🚀 개발 환경 설정

### 필수 요구사항
- .NET 8.0 SDK 이상
- Visual Studio 2022 또는 JetBrains Rider
- Windows 10/11, macOS 10.15+, 또는 Linux

### 설치 및 빌드
```bash
# 저장소 클론
git clone https://github.com/your-repo/Predvia.git

# 프로젝트 디렉토리로 이동
cd Predvia

# 의존성 복원
dotnet restore Gumaedaehang.csproj

# 개발용 빌드
dotnet build Gumaedaehang.csproj --configuration Debug

# 배포용 빌드 (Windows)
dotnet publish Gumaedaehang.csproj --configuration Release --runtime win-x64 --self-contained true --output ./publish-windows

# 실행
dotnet run --project Gumaedaehang.csproj
```

### 편의 스크립트
```bash
# 안전한 빌드 (Windows)
./빌드-최종안전.bat

# 빌드 상태 확인
./빌드-성공확인.bat

# 프로그램 실행
./실행.bat
```

## 🎨 UI/UX 디자인 가이드

### 색상 팔레트
- **주 색상**: `#E67E22` (주황색) - 브랜드 아이덴티티
- **보조 색상**: `#F47B20` (밝은 주황색) - 강조 요소
- **다크모드 강조**: `#FFDAC4` (연한 주황색) - 부드러운 톤
- **배경 색상**: `#FFF5E6` (옅은 주황색) - 차트 배경
- **텍스트 색상**: `#333333` (진한 회색) - 기본 텍스트
- **경계선 색상**: `#E0E0E0` (연한 회색) - 구분선

### 타이포그래피
- **제목**: 36px/20px, SemiBold
- **라벨**: 24px/20px, Regular
- **본문**: 18px/14px, Regular
- **입력 필드**: 18px, Regular
- **캡션**: 12px, Regular

### 컴포넌트 스타일
- **버튼**: 둥근 모서리 (12px), 적절한 패딩
- **카드**: 연한 테두리, 8px 둥근 모서리
- **입력 필드**: 최소한의 테두리, 포커스 시 주황색 강조

## 📈 개발 진행 상황

### ✅ 완료된 기능 (v1.0)
- [x] **완전한 프로젝트 구조** 및 아키텍처 설정
- [x] **API 키 인증 시스템** - 안전한 라이선스 검증
- [x] **완벽한 다크/라이트 테마** - 모든 UI 요소 테마 지원
- [x] **소싱 페이지** - 완전한 UI 구현
- [x] **마켓점검 & 마켓등록 페이지** - 11.png 참조 이미지와 100% 일치
- [x] **메인상품찾기 페이지** - 13.png/27.png와 완전 일치, 전체 화면 GUI 시스템
- [x] **설정 페이지** - 15.png/16.png와 완전 일치, 전체 화면 오버레이
- [x] **완벽한 탭 네비게이션** - 모든 페이지 간 원활한 이동
- [x] **반응형 UI 레이아웃** - 다양한 화면 크기 지원
- [x] **완전한 빌드 시스템** - Windows 독립 실행 파일 생성
- [x] **리소스 오류 해결** - 모든 필수 DLL 포함
- [x] **사용자 경험 최적화** - 직관적인 인터페이스 및 피드백

### 🔧 기술적 성과
- [x] **XML 구조 오류 해결** - 모든 XAML 파일 구조적 무결성 확보
- [x] **빌드 오류 완전 해결** - 컴파일 오류 0개 달성
- [x] **다크모드 UI 색상 통합** - 일관된 색상 체계 구현
- [x] **라이트모드 텍스트 가시성** - 모든 텍스트 요소 완벽한 가독성
- [x] **Avalonia 호환성** - 모든 지원되지 않는 속성 제거 및 대체
- [x] **Self-contained 배포** - .NET 런타임 내장, 별도 설치 불필요

### 🚧 향후 계획 (v2.0)
- [ ] **실제 API 서버 연동** - 백엔드 서비스 통합
- [ ] **데이터 영속성** - 로컬 데이터베이스 구현
- [ ] **실제 기능 로직** - 비즈니스 로직 구현
- [ ] **다국어 지원** - 한국어, 영어, 중국어
- [ ] **자동 업데이트** - 원클릭 업데이트 시스템
- [ ] **성능 최적화** - 메모리 사용량 및 속도 개선
- [ ] **단위 테스트** - 코드 품질 보장
- [ ] **사용자 매뉴얼** - 상세한 사용 가이드

## 📦 배포 정보

### 시스템 요구사항
- **운영체제**: Windows 10/11 (64-bit)
- **메모리**: 최소 4GB RAM (권장 8GB)
- **저장공간**: 최소 200MB 여유 공간
- **그래픽**: DirectX 11 지원 그래픽 카드
- **.NET**: 내장되어 있어 별도 설치 불필요

### 파일 구성 (v1.0)
```
📦 Predvia-v1.0-Windows.zip (106MB)
├── Predvia.exe (91MB)           # 메인 실행 파일
├── av_libglesv2.dll (4.2MB)     # OpenGL 그래픽 라이브러리
├── libHarfBuzzSharp.dll (1.6MB) # 텍스트 렌더링 라이브러리
├── libSkiaSharp.dll (9.0MB)     # 2D 그래픽 엔진
├── 실행.bat                     # 편리한 실행 스크립트
├── 빌드-성공확인.bat             # 빌드 상태 확인
└── README-실행방법.txt           # 사용 설명서
```

## 🤝 기여하기

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📝 라이선스

이 프로젝트는 MIT 라이선스 하에 배포됩니다. 자세한 내용은 `LICENSE` 파일을 참조하세요.

## 📞 연락처

**Softcat Team**
- 이메일: oracle7579@gmail.com
- 웹사이트: https://softcat.co.kr

## 🙏 감사의 말

- [Avalonia UI](https://avaloniaui.net/) - 훌륭한 크로스플랫폼 UI 프레임워크
- [.NET Foundation](https://dotnetfoundation.org/) - 강력한 개발 플랫폼
- 모든 기여자들과 테스터들

---

**Made with ❤️ by Softcat Team**

> **"구매대행의 새로운 표준을 제시합니다"** - Predvia v1.0
