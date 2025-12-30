#!/usr/bin/env python
# -*- coding: utf-8 -*-

import sqlite3
import os
import json
from selenium import webdriver
from selenium.webdriver.chrome.options import Options

def get_chrome_cookies_sqlite():
    """크롬 쿠키 DB에서 직접 가져오기"""
    # Windows 크롬 쿠키 경로
    cookie_path = os.path.expanduser(r"~\AppData\Local\Google\Chrome\User Data\Default\Network\Cookies")
    
    if not os.path.exists(cookie_path):
        print(f"쿠키 파일을 찾을 수 없습니다: {cookie_path}")
        return None
    
    try:
        conn = sqlite3.connect(cookie_path)
        cursor = conn.cursor()
        
        # 타오바오 관련 쿠키 조회
        cursor.execute("""
            SELECT name, value, host_key 
            FROM cookies 
            WHERE host_key LIKE '%taobao%' AND name = '_m_h5_tk'
        """)
        
        cookies = cursor.fetchall()
        conn.close()
        
        if cookies:
            for name, value, host in cookies:
                print(f"Host: {host}, Cookie: {name}={value}")
                return value
        else:
            print("_m_h5_tk 쿠키를 찾을 수 없습니다")
            return None
            
    except Exception as e:
        print(f"쿠키 읽기 오류: {e}")
        return None

def get_chrome_cookies_selenium():
    """Selenium으로 크롬 프로필 사용해서 쿠키 가져오기"""
    chrome_options = Options()
    
    # 기본 크롬 프로필 사용
    user_data_dir = os.path.expanduser(r"~\AppData\Local\Google\Chrome\User Data")
    chrome_options.add_argument(f"--user-data-dir={user_data_dir}")
    chrome_options.add_argument("--profile-directory=Default")
    
    driver = webdriver.Chrome(options=chrome_options)
    
    try:
        # 타오바오 접속
        driver.get("https://www.taobao.com")
        
        # 쿠키 가져오기
        cookies = driver.get_cookies()
        
        for cookie in cookies:
            if cookie['name'] == '_m_h5_tk':
                print(f"_m_h5_tk: {cookie['value']}")
                return cookie['value']
        
        print("_m_h5_tk 쿠키를 찾을 수 없습니다")
        return None
        
    finally:
        driver.quit()

if __name__ == "__main__":
    print("=== 크롬 프로필에서 타오바오 쿠키 가져오기 ===")
    
    # 방법 1: SQLite DB에서 직접 가져오기 (크롬이 닫혀있어야 함)
    print("방법 1: SQLite DB에서 가져오기")
    cookie = get_chrome_cookies_sqlite()
    
    if not cookie:
        # 방법 2: Selenium으로 가져오기
        print("\n방법 2: Selenium으로 가져오기")
        cookie = get_chrome_cookies_selenium()
    
    if cookie:
        print(f"\n사용할 쿠키: {cookie}")
        print("\nrun.py에서 이렇게 사용하세요:")
        print(f'taobao_upload = ali1688.WorldTaobao(manual_cookie="{cookie}")')
