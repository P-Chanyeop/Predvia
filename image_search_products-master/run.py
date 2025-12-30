#!/usr/bin/env python
# -*- coding: utf-8 -*-

import sqlite3
import os
from lib import alibaba, yiwugo
from lib.ali1688 import ali1688

def get_chrome_cookie():
    """크롬에서 _m_h5_tk 쿠키 자동 가져오기 (Windows)"""
    cookie_path = os.path.expanduser(r"~\AppData\Local\Google\Chrome\User Data\Default\Network\Cookies")
    
    if not os.path.exists(cookie_path):
        return None
    
    try:
        conn = sqlite3.connect(cookie_path)
        cursor = conn.cursor()
        cursor.execute("""
            SELECT value FROM cookies 
            WHERE name = '_m_h5_tk' AND value != ''
            ORDER BY creation_utc DESC LIMIT 1
        """)
        result = cursor.fetchone()
        conn.close()
        return result[0] if result else None
    except:
        return None

if __name__ == "__main__":
    path = "다운로드.jpg"

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

    # 세션 모드로 타오바오 사용 (모든 쿠키 자동 로드)
    try:
        print("세션 모드로 타오바오 연결 시도...")
        taobao_upload = ali1688.WorldTaobao(use_session=True)
        print("✅ 세션 모드 성공")
    except Exception as e:
        print(f"세션 모드 실패: {e}")
        print("수동 쿠키 모드로 전환...")
        manual_cookie = get_chrome_cookie()
        taobao_upload = ali1688.WorldTaobao(manual_cookie=manual_cookie)
        
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
