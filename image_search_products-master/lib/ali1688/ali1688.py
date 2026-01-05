#!/usr/bin/env python
# -*- coding: utf-8 -*-


import base64
import json
import re
from typing import Dict

import requests
from requests.cookies import RequestsCookieJar

from lib.ali1688.sign import Sign
from lib.func_txy import now, request_get, request_post


class Ali1688(object):
    def __init__(self):
        self.t = now()
        self.app_key = "12574478"

    def headers(self):
        headres = {
            "User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:85.0) Gecko/20100101 Firefox/85.0",
        }
        return headres


class Token(Ali1688):
    def __init__(self, api: str, hostname: str):
        self.api = api
        self.hostname = hostname
        self.token_url = f"https://{self.hostname}/h5/{self.api.lower()}/1.0/"
        self.cookies: RequestsCookieJar
        super(Token, self).__init__()

    def get_token_params(self) -> Dict[str, str]:
        params = {
            "jsv": "2.7.0",
            "appKey": self.app_key,
            "t": str(self.t),
            "api": self.api,
            "v": "1.0",
            "type": "json",
            "dataType": "jsonp",
            "callback": "mtopjsonp1",
        }
        return params

    def request(self) -> requests.request:
        params = self.get_token_params()
        headers = self.headers()
        req = request_get(url=self.token_url, params=params, headers=headers)
        self.cookies: RequestsCookieJar = req.cookies
        return req

    def _get_token(self):
        if not self.cookies or not self.cookies.get("_m_h5_tk", ""):
            raise Exception("cookie not found _m_h5_tk")

        cookie_list = self.cookies.get("_m_h5_tk", "").split("_")
        if len(cookie_list) < 2:
            raise Exception("cookie _m_h5_tk not found '_' ")

        self.token: str = cookie_list[0]

    def get_sign(self, data: str, t: int) -> str:
        text = f"{self.token}&{t}&{self.app_key}&{data}"
        sign = Sign()
        sign_str = sign.sign(text)
        return sign_str


class Ali1688Upload(Token):
    def __init__(self, api: str = "mtop.1688.imageService.putImage", hostname="h5api.m.taobao.com", manual_cookie=None):
        super(Ali1688Upload, self).__init__(api=api, hostname=hostname)
        self.upload_url = f"https://{self.hostname}/h5/{self.api.lower()}/1.0"
        
        if manual_cookie:
            # 수동 쿠키 설정
            self.cookies = RequestsCookieJar()
            self.cookies.set("_m_h5_tk", manual_cookie)
        else:
            self.request()
        
        self._get_token()

    def get_params(self, data: str, t: int, jsv: str = "2.4.11") -> Dict[str, str]:
        sign_str = self.get_sign(data=data, t=t)
        params = {
            "jsv": jsv,
            "appKey": self.app_key,
            "t": str(t),
            "api": self.api,
            "ecode": "0",
            "v": "1.0",
            "type": "originaljson",
            "dataType": "jsonp",
            "sign": sign_str,
        }
        return params

    def get_data(self, filename: str) -> Dict[str, str]:
        # get file bytes
        with open(filename, "rb") as f:
            b64_bytes = base64.b64encode(f.read())
        data = json.dumps(
            {
                "imageBase64": str(b64_bytes).replace("b'", "").replace("'", ""),
                "appName": "searchImageUpload",
                "appKey": "pvvljh1grxcmaay2vgpe9nb68gg9ueg2",
            },
            separators=(",", ":"),
        )
        return {"data": data}

    def upload(self, filename: str) -> requests.request:
        # upload image
        t = now()
        data = self.get_data(filename=filename)
        params = self.get_params(data=data.get("data", ""), t=t)
        headers = self.headers()
        headers["Content-Type"] = "application/x-www-form-urlencoded"
        req = request_post(
            url=self.upload_url,
            params=params,
            headers=headers,
            data=data,
            cookies=self.cookies.get_dict(),
        )
        return req


class WorldTaobao(Ali1688Upload):
    def __init__(self, api: str = "mtop.tmall.hk.yx.worldhomepagepcapi.gethotwords", hostname="h5api.m.taobao.com", manual_cookie=None, use_session=False):
        if use_session:
            # 세션 모드: 모든 쿠키 사용
            self.session = self._create_session()
            super(Ali1688Upload, self).__init__(api=api, hostname=hostname)
            self.upload_url = f"https://{self.hostname}/h5/mtop.relationrecommend.wirelessrecommend.recommend/2.0/"
            # 세션에서 _m_h5_tk 쿠키 찾기
            for cookie in self.session.cookies:
                if cookie.name == '_m_h5_tk' and cookie.value:
                    self.cookies = self.session.cookies
                    self.token = cookie.value.split('_')[0]
                    return
            raise Exception("세션에서 _m_h5_tk 쿠키를 찾을 수 없습니다")
        else:
            super(WorldTaobao, self).__init__(api=api, hostname=hostname, manual_cookie=manual_cookie)
            self.upload_url = f"https://{self.hostname}/h5/mtop.relationrecommend.wirelessrecommend.recommend/2.0/"

            # ⭐ 쿠키 파일에서 모든 쿠키 로드하여 세션에 설정
            self._load_all_cookies_from_file()

    def headers(self):
        """타오바오 요청용 헤더 - Chrome User-Agent 사용"""
        import os

        # 기본 Chrome User-Agent
        user_agent = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36'

        # CHANGE_USER_AGENT 환경변수가 설정되어 있으면 다른 User-Agent 사용
        if os.environ.get('CHANGE_USER_AGENT') == 'true':
            user_agent = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
            print(f"🔄 User-Agent 변경됨: {user_agent}")

        headers = {
            "User-Agent": user_agent,
            "Referer": "https://www.taobao.com/",
            "Accept": "application/json",
            "Accept-Language": "ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7",
        }
        return headers

    def _load_all_cookies_from_file(self):
        """쿠키 파일에서 모든 타오바오 쿠키 읽어서 세션에 설정"""
        import json
        import os

        cookie_file_path = os.path.expanduser(r"~\AppData\Roaming\Predvia\taobao_cookies.json")

        if not os.path.exists(cookie_file_path):
            print("⚠️ 쿠키 파일을 찾을 수 없음 - 기본 쿠키만 사용")
            return

        try:
            print("🔍 쿠키 파일에서 토큰 찾는 중...")
            print(f"📁 쿠키 파일 경로: {cookie_file_path}")

            with open(cookie_file_path, 'r', encoding='utf-8') as f:
                saved_cookies = json.load(f)

            print(f"✅ 쿠키 파일 발견!")

            # _m_h5_tk 토큰 확인 (이미 설정되어 있음)
            if '_m_h5_tk' in saved_cookies:
                token = saved_cookies['_m_h5_tk']
                print(f"🔑 쿠키 파일에서 토큰 발견: {token[:20]}...")

            # 모든 타오바오 쿠키를 세션에 설정
            print("🍪 모든 타오바오 쿠키를 세션에 설정 중...")
            for cookie_name, cookie_value in saved_cookies.items():
                if cookie_value:  # 값이 있는 쿠키만 설정
                    self.cookies.set(cookie_name, cookie_value, domain='.taobao.com')
                    print(f"🔧 쿠키 설정: {cookie_name}")

            print(f"✅ 총 {len(saved_cookies)}개 쿠키 설정 완료")

        except Exception as e:
            print(f"⚠️ 쿠키 파일 로드 오류: {e}")

    def _create_session(self):
        """타오바오 세션 생성"""
        import sqlite3
        import os
        
        session = requests.Session()
        cookie_path = os.path.expanduser(r"~\AppData\Local\Google\Chrome\User Data\Default\Network\Cookies")
        
        try:
            conn = sqlite3.connect(cookie_path)
            cursor = conn.cursor()
            cursor.execute("""
                SELECT name, value FROM cookies 
                WHERE host_key LIKE '%taobao%' AND value != ''
            """)
            
            for name, value in cursor.fetchall():
                session.cookies.set(name, value, domain='.taobao.com')
            
            conn.close()
        except:
            pass
        
        # User-Agent 설정 (환경변수로 변경 가능)
        import os
        user_agent = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'

        # CHANGE_USER_AGENT 환경변수가 설정되어 있으면 다른 User-Agent 사용
        if os.environ.get('CHANGE_USER_AGENT') == 'true':
            user_agent = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
            print(f"🔄 User-Agent 변경됨: {user_agent}")

        session.headers.update({
            'User-Agent': user_agent,
            'Referer': 'https://www.taobao.com/',
        })
        
        return session

    def get_data(self, filename: str) -> Dict[str, str]:
        # get file bytes
        with open(filename, "rb") as f:
            b64_bytes = base64.b64encode(f.read())

        # ⭐ Base64 문자열 변환 (패딩은 끝에만 있으므로 rstrip 사용)
        strimg = b64_bytes.decode('ascii').rstrip('=')

        params = json.dumps(
            {
                "strimg": strimg,
                "pcGraphSearch": True,
                "sortOrder": 0,
                "tab": "all",
                "vm": "nv",
            },
            separators=(",", ":"),
        )

        data = json.dumps({"params": params, "appId": "34850"}, separators=(",", ":"), )

        return {"data": data}

    def search(self, image_id: str):
        """이미지 ID로 상품 검색"""
        search_url = f"https://s.taobao.com/search?imgfile=&commend=all&ssid=s5-e&search_type=item&sourceId=tb.index&spm=a21bo.jianhua.201856-taobao-item.1&ie=utf8&initiative_id=tbindexz_20170306&imageId={image_id}"
        headers = self.headers()
        return request_get(url=search_url, headers=headers)


class Ali1688ImageSearch(Ali1688):
    def __init__(self):
        self.url = "https://s.1688.com/youyuan/index.htm"
        super(Ali1688ImageSearch, self).__init__()

    def get_params(self, image_id: str) -> Dict[str, str]:
        params = {"tab": "imageSearch", "imageId": image_id, "imageIdList": image_id}
        return params

    def request(self, image_id: str) -> requests.request:
        params = self.get_params(image_id=image_id)
        headers = self.headers()
        req = request_get(url=self.url, params=params, headers=headers)
        return req

    def check_goods(self, html: str):
        # todo
        re.findall(r"window.data.offerresultData = successDataCheck\(.*?\)", html)
