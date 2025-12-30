#!/usr/bin/env python
# -*- coding: utf-8 -*-

import requests
import sqlite3
import os

def get_all_taobao_cookies():
    """크롬에서 타오바오 관련 모든 쿠키 가져오기"""
    cookie_path = os.path.expanduser(r"~\AppData\Local\Google\Chrome\User Data\Default\Network\Cookies")
    
    if not os.path.exists(cookie_path):
        return {}
    
    try:
        conn = sqlite3.connect(cookie_path)
        cursor = conn.cursor()
        cursor.execute("""
            SELECT name, value, host_key FROM cookies 
            WHERE host_key LIKE '%taobao%' AND value != ''
            ORDER BY creation_utc DESC
        """)
        
        cookies = {}
        for name, value, host in cursor.fetchall():
            cookies[name] = value
        
        conn.close()
        return cookies
    except:
        return {}

def create_taobao_session():
    """타오바오 세션 생성"""
    session = requests.Session()
    
    # 모든 타오바오 쿠키 가져오기
    cookies = get_all_taobao_cookies()
    
    # 세션에 쿠키 설정
    for name, value in cookies.items():
        session.cookies.set(name, value, domain='.taobao.com')
    
    # 헤더 설정
    session.headers.update({
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
        'Accept': 'application/json, text/plain, */*',
        'Accept-Language': 'zh-CN,zh;q=0.9,en;q=0.8',
        'Referer': 'https://www.taobao.com/',
    })
    
    return session

if __name__ == "__main__":
    session = create_taobao_session()
    print(f"세션 쿠키 개수: {len(session.cookies)}")
    
    # 테스트 요청
    test_url = "https://h5api.m.taobao.com/h5/mtop.tmall.hk.yx.worldhomepagepcapi.gethotwords/1.0/"
    response = session.get(test_url)
    print(f"응답 상태: {response.status_code}")
    print(f"응답 내용: {response.text[:200]}...")
