#!/usr/bin/env python
# -*- coding: utf-8 -*-

import sys
import io

# ⭐ UTF-8 출력 강제 설정 (Windows cp949 오류 방지)
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

import sqlite3
import os
import random
from lib import alibaba, yiwugo
from lib.ali1688 import ali1688

# ⭐ 프록시 관련 함수
def load_proxy_list():
    """프록시 목록 파일에서 로드"""
    proxy_file = "프록시유동_모모아이피.txt"

    if not os.path.exists(proxy_file):
        print(f"⚠️ 프록시 파일 없음: {proxy_file}")
        return []

    try:
        with open(proxy_file, 'r', encoding='utf-8') as f:
            proxies = [line.strip() for line in f if line.strip()]
        print(f"✅ 프록시 {len(proxies)}개 로드 완료 (파일: {proxy_file})")
        return proxies
    except Exception as e:
        print(f"❌ 프록시 로드 실패: {e}")
        return []

def get_random_proxy(proxy_list):
    """랜덤으로 프록시 선택"""
    if not proxy_list:
        print("⚠️ 프록시 없음 - 직접 연결")
        return None

    proxy = random.choice(proxy_list)
    print(f"🔄 프록시 사용: {proxy}")
    return {
        'http': f'http://{proxy}',
        'https': f'http://{proxy}'
    }

# 전역 프록시 목록
_proxy_list = load_proxy_list()

def get_chrome_cookies_all():
    """크롬에서 모든 타오바오 쿠키 가져오기"""
    cookie_path = os.path.expanduser(r"~\AppData\Local\Google\Chrome\User Data\Default\Network\Cookies")
    
    if not os.path.exists(cookie_path):
        print(f"❌ 쿠키 파일을 찾을 수 없습니다: {cookie_path}")
        return {}
    
    try:
        # Chrome이 실행 중이면 쿠키 DB에 접근할 수 없으므로 복사본 사용
        import shutil
        temp_cookie_path = cookie_path + "_temp"
        shutil.copy2(cookie_path, temp_cookie_path)
        
        conn = sqlite3.connect(temp_cookie_path)
        cursor = conn.cursor()
        cursor.execute("""
            SELECT name, value FROM cookies 
            WHERE host_key LIKE '%taobao%' AND value != ''
        """)
        cookies = {}
        for name, value in cursor.fetchall():
            cookies[name] = value
            print(f"🍪 쿠키 발견: {name}")
        conn.close()
        
        # 임시 파일 삭제
        os.remove(temp_cookie_path)
        
        return cookies
    except Exception as e:
        print(f"❌ 쿠키 로드 오류: {e}")
        return {}

def get_chrome_cookie():
    """크롬에서 _m_h5_tk 쿠키 자동 가져오기 (Windows)"""
    cookies = get_chrome_cookies_all()
    token = cookies.get('_m_h5_tk')
    if token:
        print(f"🔑 _m_h5_tk 토큰 발견: {token[:20]}...")
    else:
        print("❌ _m_h5_tk 토큰이 없습니다")
    return token

def load_taobao_upload():
    """타오바오 업로드 객체 생성 (쿠키 로드 + 프록시 설정)"""
    taobao_upload = None

    # ⭐ 프록시 선택
    proxy_dict = get_random_proxy(_proxy_list)

    # 1순위: 환경변수에서 토큰 확인
    env_token = os.environ.get('TAOBAO_TOKEN')
    print(f"🔍 환경변수 TAOBAO_TOKEN: {env_token[:20] + '...' if env_token else 'None'}")

    if env_token:
        print(f"🔑 환경변수에서 _m_h5_tk 토큰 발견: {env_token[:20]}...")
        try:
            taobao_upload = ali1688.WorldTaobao(manual_cookie=env_token, proxies=proxy_dict)
            print("✅ 환경변수 토큰으로 타오바오 연결 성공")
        except Exception as e:
            print(f"❌ 환경변수 토큰 연결 실패: {e}")
            taobao_upload = None

    # 환경변수 토큰이 없거나 실패한 경우 다른 방법 시도
    if taobao_upload is None:
        try:
            print("🔍 저장된 쿠키 파일 확인 중...")

            # C# 서버에서 저장한 쿠키 파일 경로
            import json
            cookie_file_path = os.path.expanduser(r"~\AppData\Roaming\Predvia\taobao_cookies.json")
            print(f"📁 쿠키 파일 경로: {cookie_file_path}")
            print(f"📁 파일 존재 여부: {os.path.exists(cookie_file_path)}")

            if os.path.exists(cookie_file_path):
                print("✅ 저장된 쿠키 파일 발견")
                with open(cookie_file_path, 'r', encoding='utf-8') as f:
                    saved_cookies = json.load(f)

                print(f"📊 쿠키 파일 내용: {len(saved_cookies)}개 쿠키")
                print(f"🔍 쿠키 키 목록: {list(saved_cookies.keys())}")

                # _m_h5_tk 토큰 확인
                if '_m_h5_tk' in saved_cookies:
                    token = saved_cookies['_m_h5_tk']
                    print(f"🔑 _m_h5_tk 토큰 발견: {token[:20]}...")
                    taobao_upload = ali1688.WorldTaobao(manual_cookie=token, proxies=proxy_dict)
                    print("✅ 저장된 쿠키로 타오바오 연결 성공")
                else:
                    print("❌ _m_h5_tk 토큰이 저장된 쿠키에 없습니다")
                    print(f"🔍 실제 쿠키 내용 (처음 5개): {dict(list(saved_cookies.items())[:5])}")
                    raise Exception("No _m_h5_tk token in saved cookies")
            else:
                print("❌ 저장된 쿠키 파일이 없습니다")
                print("세션 모드로 타오바오 연결 시도...")
                taobao_upload = ali1688.WorldTaobao(use_session=True, proxies=proxy_dict)
                print("✅ 세션 모드 성공")
        except Exception as e:
            print(f"저장된 쿠키/세션 모드 실패: {e}")
            print("Chrome 쿠키 직접 읽기 모드로 전환...")
            manual_cookie = get_chrome_cookie()
            if manual_cookie:
                taobao_upload = ali1688.WorldTaobao(manual_cookie=manual_cookie, proxies=proxy_dict)
                print("✅ Chrome 쿠키 직접 읽기 성공")
            else:
                print("❌ 모든 쿠키 획득 방법 실패")
                raise Exception("All cookie methods failed")

    return taobao_upload

if __name__ == "__main__":
    import sys
    
    print("=== PYTHON 디버깅 시작 ===")
    sys.stdout.flush()

    # 명령행 인수에서 이미지 경로 받기
    if len(sys.argv) > 1:
        path = sys.argv[1]
        print(f"📷 [디버그] 명령행에서 받은 이미지 경로: {path}")
        sys.stdout.flush()
    else:
        path = "다운로드.jpg"
        print("📷 [디버그] 기본 이미지 사용: 다운로드.jpg")
        sys.stdout.flush()

    # 🔍 실제 파일 존재 여부 및 크기 확인
    import os
    if os.path.exists(path):
        file_size = os.path.getsize(path)
        print(f"✅ [디버그] 이미지 파일 존재 확인 - 크기: {file_size} bytes")
        print(f"📁 [디버그] 절대 경로: {os.path.abspath(path)}")
        sys.stdout.flush()
    else:
        print(f"❌ [디버그] 이미지 파일이 존재하지 않음: {path}")
        print("🔄 [디버그] 기본 이미지로 대체...")
        sys.stdout.flush()
        path = "다운로드.jpg"
        if os.path.exists(path):
            file_size = os.path.getsize(path)
            print(f"✅ [디버그] 기본 이미지 사용 - 크기: {file_size} bytes")
            sys.stdout.flush()
        else:
            print("❌ [디버그] 기본 이미지도 없음!")
            sys.stdout.flush()
            sys.exit(1)

    print(f"🎯 [디버그] 최종 사용할 이미지: {path}")
    print("=== 이미지 디버깅 완료, 타오바오 연결 시작 ===")
    sys.stdout.flush()

    # --retry 플래그 확인
    is_retry = '--retry' in sys.argv
    if is_retry:
        print("🔄 [재시도 모드] 쿠키 파일을 다시 로드합니다...")

    # ⭐ 먼저 타오바오 연결 설정
    taobao_upload = load_taobao_upload()

    # 1688 example
    # get cookie and token
    # upload image and get image id
    upload = ali1688.Ali1688Upload()
    res = upload.upload(filename=path)
    image_id = res.json().get("data", {}).get("imageId", "")
    if not image_id:
        raise Exception("not image id")
    print(image_id)

    # search goods by i®mage id
    image_search = ali1688.Ali1688ImageSearch()
    req = image_search.request(image_id=image_id)
    print(req.url)
        
    res = taobao_upload.upload(filename=path)
    response_json = res.json()

    # ⭐ 응답 분석
    print(f"📊 타오바오 API 응답 코드: {res.status_code}")
    sys.stdout.flush()

    # ret 필드 확인 (오류 체크)
    if "ret" in response_json:
        ret_value = response_json["ret"]
        print(f"📋 API ret 값: {ret_value}")
        sys.stdout.flush()

    # ⭐ 항상 "Full response:" 형식으로 출력 (C# 파싱용)
    import json
    json_str = json.dumps(response_json, ensure_ascii=False, separators=(',', ':'))
    print(f"Full response: {json_str}")
    sys.stdout.flush()

    # data 필드 확인
    data = response_json.get("data")
    if data and isinstance(data, dict) and len(data) > 0:
        print("✅ taobao_upload success")
        sys.stdout.flush()

        image_id = data.get("imageId", "")
        print(f"🆔 Image ID: {image_id}")
        sys.stdout.flush()

        if image_id:
            # 검색 결과 확인
            search_result = taobao_upload.search(image_id)
            print(f"🔗 Search URL: {search_result.url}")
            sys.stdout.flush()
        else:
            print("⚠️ No image ID found in response")
            sys.stdout.flush()
    else:
        print(f"❌ 타오바오 업로드 실패!")
        sys.stdout.flush()
        raise Exception("taobao upload fail")
    # alibaba example
    upload = alibaba.Upload()
    image_key = upload.upload(filename=path)
    print(f"{image_key}")

    image_searh = alibaba.ImageSearch()
    req = image_searh.search(image_key=image_key)
    print(req.url)

    # yiwugo
    # yiwugo = yiwugo.YiWuGo()
    # res = yiwugo.upload(path)
    # print(res.status_code)
    # assert "起购" in res.text, "yiwugo search error"
