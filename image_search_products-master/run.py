#!/usr/bin/env python
# -*- coding: utf-8 -*-

import sqlite3
import os
from lib import alibaba, yiwugo
from lib.ali1688 import ali1688

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

if __name__ == "__main__":
    path = "다운로드.jpg"

    # ⭐ 먼저 타오바오 연결 설정
    taobao_upload = None
    
    # 1순위: 환경변수에서 토큰 확인
    env_token = os.environ.get('TAOBAO_TOKEN')
    print(f"🔍 환경변수 TAOBAO_TOKEN: {env_token[:20] + '...' if env_token else 'None'}")
    
    if env_token:
        print(f"🔑 환경변수에서 _m_h5_tk 토큰 발견: {env_token[:20]}...")
        try:
            taobao_upload = ali1688.WorldTaobao(manual_cookie=env_token)
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
                    taobao_upload = ali1688.WorldTaobao(manual_cookie=token)
                    print("✅ 저장된 쿠키로 타오바오 연결 성공")
                else:
                    print("❌ _m_h5_tk 토큰이 저장된 쿠키에 없습니다")
                    print(f"🔍 실제 쿠키 내용 (처음 5개): {dict(list(saved_cookies.items())[:5])}")
                    raise Exception("No _m_h5_tk token in saved cookies")
            else:
                print("❌ 저장된 쿠키 파일이 없습니다")
                print("세션 모드로 타오바오 연결 시도...")
                taobao_upload = ali1688.WorldTaobao(use_session=True)
                print("✅ 세션 모드 성공")
        except Exception as e:
            print(f"저장된 쿠키/세션 모드 실패: {e}")
            print("Chrome 쿠키 직접 읽기 모드로 전환...")
            manual_cookie = get_chrome_cookie()
            if manual_cookie:
                taobao_upload = ali1688.WorldTaobao(manual_cookie=manual_cookie)
                print("✅ Chrome 쿠키 직접 읽기 성공")
            else:
                print("❌ 모든 쿠키 획득 방법 실패")
                raise Exception("All cookie methods failed")

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
    if res.json().get("data"):
        print("taobao_upload success")
        print("Full response:", res.json())  # 전체 응답 확인
        image_id = res.json().get("data", {}).get("imageId", "")
        print(f"Image ID: {image_id}")
        
        if image_id:
            # 검색 결과 확인
            search_result = taobao_upload.search(image_id)
            print(f"Search URL: {search_result.url}")
        else:
            print("No image ID found in response")
    else:
        print("Upload response:", res.json())
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
