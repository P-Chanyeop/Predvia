# 🛒 구매대행 시스템 - Predvia

> **전문적인 구매대행 서비스를 위한 통합 관리 시스템**

![Avalonia](https://img.shields.io/badge/Avalonia-11.0-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![C#](https://img.shields.io/badge/C%23-12.0-green)
![License](https://img.shields.io/badge/License-MIT-yellow)

## 📋 프로젝트 개요

구매대행 업무의 효율성을 극대화하기 위해 개발된 크로스플랫폼 데스크톱 애플리케이션입니다. 
Avalonia UI를 기반으로 하여 Windows, macOS, Linux에서 동일한 사용자 경험을 제공합니다.

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
- **10.png 구현**: 심플한 마켓등록 진입점
  - 화면 중앙의 주황색 "마켓등록하기" 버튼 (240×70px)
  - 적당한 둥근 모서리 (CornerRadius="12")
  - 20px 폰트 크기로 가독성 최적화
  - 호버/클릭 효과로 사용자 피드백 제공

- **11.png 구현**: 완전한 상품 등록 관리 시스템
  - 2×2 그리드 상품 카드 레이아웃
  - 실시간 피드백 시스템 ("중국어가 있습니다", "브랜드명이 포함되어있습니다")
  - 네이버쇼핑 바로가기 링크 (검정색 밑줄 스타일)
  - 옅은 주황색 배경의 차트 섹션 (#FFF5E6)
  - 막대 그래프: 회색 배경 + 주황색 전경 겹침 효과
  - 범례: 주문처리형, 밸런스형, 크무비형
  - 주황색 피드백 텍스트 (#E67E22)
  - **완전한 페이지 네비게이션**: 마켓점검 → 마켓등록 세부과정 이동

#### 🎯 메인상품찾기 (Main Product Finder)
- 베타 버전으로 개발 중
- AI 기반 상품 추천 시스템 (예정)

#### ⚙️ 설정 (Settings)
- 사용자 환경 설정
- 시스템 구성 관리

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
Gumaedaehang/
├── 📁 Assets/                 # 리소스 파일
├── 📁 images/                 # UI 참조 이미지
│   ├── 10.png                # 마켓점검 메인 화면
│   └── 11.png                # 마켓등록 상세 화면
├── 📁 Services/              # 비즈니스 로직
│   ├── ApiClient.cs          # API 통신 클라이언트
│   ├── ApiKeyAuthClient.cs   # 인증 서비스
│   ├── AuthManager.cs        # 인증 관리자
│   └── AdviceService.cs      # 조언 서비스
├── 📁 Styles/                # UI 스타일 정의
├── MainWindow.axaml          # 메인 윈도우 UI
├── MainWindow.axaml.cs       # 메인 윈도우 로직
├── SourcingPage.axaml        # 소싱 페이지 UI
├── SourcingPage.axaml.cs     # 소싱 페이지 로직
├── MarketCheckPage.axaml     # 마켓점검 페이지 UI
├── MarketCheckPage.axaml.cs  # 마켓점검 페이지 로직
├── MarketRegistrationPage.axaml    # 마켓등록 페이지 UI
├── MarketRegistrationPage.axaml.cs # 마켓등록 페이지 로직
├── ApiKeyAuthWindow.axaml    # 인증 윈도우 UI
├── ApiKeyAuthWindow.axaml.cs # 인증 윈도우 로직
├── LoginWindow.axaml         # 로그인 윈도우 UI
├── LoginWindow.axaml.cs      # 로그인 윈도우 로직
├── SignupWindow.axaml        # 회원가입 윈도우 UI
├── SignupWindow.axaml.cs     # 회원가입 윈도우 로직
├── ThemeManager.cs           # 테마 관리자
├── App.axaml                 # 애플리케이션 리소스
├── App.axaml.cs              # 애플리케이션 진입점
└── Program.cs                # 메인 프로그램
```

## 🚀 시작하기

### 필수 요구사항
- .NET 8.0 SDK 이상
- Visual Studio 2022 또는 JetBrains Rider
- Windows 10/11, macOS 10.15+, 또는 Linux

### 설치 및 실행
```bash
# 저장소 클론
git clone https://github.com/your-repo/Gumaedaehang.git

# 프로젝트 디렉토리로 이동
cd Gumaedaehang

# 의존성 복원
dotnet restore

# 애플리케이션 빌드
dotnet build

# 애플리케이션 실행
dotnet run
```

### 테스트 API 키
개발 및 테스트 목적으로 다음 API 키를 사용할 수 있습니다:
- `PREDVIA-API-KEY-12345` (프리미엄 라이선스)
- `TEST-API-KEY-67890` (스탠다드 라이선스)

## 🎨 UI/UX 디자인 가이드

### 색상 팔레트
- **주 색상**: #E67E22 (주황색) - 브랜드 아이덴티티
- **보조 색상**: #F47B20 (밝은 주황색) - 강조 요소
- **배경 색상**: #FFF5E6 (옅은 주황색) - 차트 배경
- **텍스트 색상**: #333333 (진한 회색) - 기본 텍스트
- **경계선 색상**: #E0E0E0 (연한 회색) - 구분선

### 타이포그래피
- **제목**: 20px, SemiBold
- **본문**: 14px, Regular
- **캡션**: 12px, Regular
- **링크**: 11px, Underline

### 컴포넌트 스타일
- **버튼**: 둥근 모서리 (12px), 적절한 패딩
- **카드**: 연한 테두리, 8px 둥근 모서리
- **입력 필드**: 최소한의 테두리, 포커스 시 주황색 강조

## 📈 개발 진행 상황

### ✅ 완료된 기능
- [x] 기본 프로젝트 구조 설정
- [x] API 키 인증 시스템
- [x] 다크/라이트 테마 전환
- [x] 소싱 페이지 기본 구조
- [x] 마켓점검 페이지 (10.png 구현)
- [x] 마켓등록 페이지 (11.png 구현)
- [x] 탭 네비게이션 시스템
- [x] 반응형 UI 레이아웃
- [x] 마켓점검 → 마켓등록 페이지 네비게이션 구현
- [x] 완전한 상품 등록 관리 시스템 UI 구현
- [x] **네임스코프 충돌 문제 해결**: MarketRegistrationPage.axaml의 중복된 MainGrid 이름 충돌 수정
- [x] **다크모드 텍스트 가시성 개선**: 마켓등록 페이지 다크모드에서 모든 텍스트 요소 가시성 확보
  - X축 라벨 텍스트 (1,000, 10,000, 50,000, 100,000) 주황색 적용
  - 체크박스 텍스트 (주문처리형, 밸런스형, 크무비형) 주황색 적용
  - 피드백 텍스트 주황색 유지 및 스타일 충돌 해결
  - 검색창 다크모드 배경색 적용 (#4A4A4A)
- [x] **XAML 구문 오류 수정**: 체크박스 커스텀 템플릿의 XML 구조 오류 해결
- [x] **체크박스 템플릿 최적화**: ContentPresenter를 TextBlock으로 변경하여 텍스트 색상 제어 개선
- [x] **명언 텍스트 정렬 개선**: "오늘의 명언:"과 명언 텍스트의 수직 정렬 문제 해결 (StackPanel → Grid 레이아웃 변경)

### 🚧 진행 중인 작업
- [ ] 메인상품찾기 기능 구현
- [ ] 설정 페이지 상세 기능
- [ ] API 서버 연동 완성
- [ ] 데이터 영속성 구현
- [ ] 마켓등록 페이지 상호작용 기능 추가

### 📋 향후 계획
- [ ] 다국어 지원 (한국어, 영어, 중국어)
- [ ] 자동 업데이트 시스템
- [ ] 성능 최적화
- [ ] 단위 테스트 작성
- [ ] 사용자 매뉴얼 작성

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
