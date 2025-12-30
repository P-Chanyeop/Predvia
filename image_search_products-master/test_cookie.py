#!/usr/bin/env python
# -*- coding: utf-8 -*-

import sqlite3
import os

def test_chrome_cookie():
    """크롬 쿠키 테스트"""
    print("=== 크롬 쿠키 테스트 ===")
    
    # 쿠키 파일 경로들 확인 (Windows)
    possible_paths = [
        os.path.expanduser(r"~\AppData\Local\Google\Chrome\User Data\Default\Network\Cookies"),
        os.path.expanduser(r"~\AppData\Local\Google\Chrome\User Data\Default\Cookies"),
        os.path.expanduser(r"~\AppData\Local\Google\Chrome\User Data\Profile 1\Network\Cookies"),
        os.path.expanduser(r"~\AppData\Local\Google\Chrome\User Data\Profile 1\Cookies"),
        os.path.expanduser(r"~\AppData\Local\Google\Chrome\User Data\Profile 2\Network\Cookies"),
        os.path.expanduser(r"~\AppData\Local\Google\Chrome\User Data\Profile 2\Cookies"),
    ]
    
    print("쿠키 파일 경로 확인:")
    for path in possible_paths:
        exists = os.path.exists(path)
        print(f"  {path} - {'존재' if exists else '없음'}")
        if exists:
            cookie_path = path
            break
    else:
        print("쿠키 파일을 찾을 수 없습니다")
        return None
    
    print(f"\n사용할 쿠키 파일: {cookie_path}")
    
    try:
        # 쿠키 DB 연결
        conn = sqlite3.connect(cookie_path)
        cursor = conn.cursor()
        
        # 모든 타오바오 관련 쿠키 확인
        print("\n타오바오 관련 모든 쿠키:")
        cursor.execute("""
            SELECT name, value, host_key, creation_utc, expires_utc
            FROM cookies 
            WHERE host_key LIKE '%taobao%'
            ORDER BY creation_utc DESC
        """)
        
        all_cookies = cursor.fetchall()
        if all_cookies:
            for name, value, host, created, expires in all_cookies:
                print(f"  Host: {host}")
                print(f"  Name: {name}")
                print(f"  Value: {value[:50]}..." if len(value) > 50 else f"  Value: {value}")
                print(f"  Created: {created}, Expires: {expires}")
                print("  ---")
        else:
            print("  타오바오 쿠키가 없습니다")
        
        # _m_h5_tk 쿠키만 찾기
        print("\n_m_h5_tk 쿠키 검색:")
        cursor.execute("""
            SELECT name, value, host_key
            FROM cookies 
            WHERE name = '_m_h5_tk'
            ORDER BY creation_utc DESC
        """)
        
        tk_cookies = cursor.fetchall()
        if tk_cookies:
            for name, value, host in tk_cookies:
                print(f"  Host: {host}, Value: {value}")
                return value
        else:
            print("  _m_h5_tk 쿠키를 찾을 수 없습니다")
        
        conn.close()
        return None
        
    except Exception as e:
        print(f"오류 발생: {e}")
        return None

if __name__ == "__main__":
    cookie = test_chrome_cookie()
    if cookie:
        print(f"\n✅ 찾은 쿠키: {cookie}")
    else:
        print("\n❌ 쿠키를 찾을 수 없습니다")
        print("\n해결 방법:")
        print("1. 크롬에서 https://www.taobao.com 접속")
        print("2. 로그인")
        print("3. 크롬 완전 종료")
        print("4. 다시 테스트 실행")
