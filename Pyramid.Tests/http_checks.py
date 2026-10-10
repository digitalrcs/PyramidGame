"""Run against a LOCAL development server; no production account data is used."""
import concurrent.futures
import http.cookiejar
import json
import urllib.request
import urllib.error
import uuid
import os

BASE = os.environ.get('PYRAMID_TEST_BASE','http://localhost:5188/pyramid/')
class Client:
    def __init__(self):
        self.jar=http.cookiejar.CookieJar()
        self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar))
        self.token=''
    def call(self,path,body=None,csrf=True):
        headers={}
        if body is not None:
            headers['Content-Type']='application/json'
            if csrf: headers['X-Pyramid-CSRF']=self.token
        req=urllib.request.Request(BASE+path,data=None if body is None else json.dumps(body).encode(),headers=headers)
        try: response=self.opener.open(req)
        except urllib.error.HTTPError as error: response=error
        raw=response.read().decode()
        try: value=json.loads(raw)
        except ValueError: value=raw
        return response.status,value,response.headers
    def session(self):
        status,data,_=self.call('api/session')
        assert status==200,(status,data)
        self.token=data['token']
        return data

count=0
def check(condition,label):
    global count
    assert condition,label
    count+=1
    print('PASS',label)

a,b=Client(),Client()
check(a.call('api/game')[0]==401,'Anonymous saves are blocked')
check(a.call('api/explorers')[0]==401,'Anonymous roster is blocked')
status,html,headers=a.call('')
check(status==200 and '<base href="/pyramid/">' in html,'Subfolder base is server-derived')
check("frame-ancestors 'none'" in headers['Content-Security-Policy'],'Frame embedding is denied')
check(headers['X-Content-Type-Options']=='nosniff','MIME sniffing blocked')
a.session();b.session()
suffix=uuid.uuid4().hex[:7]
password='Local-test-only-2026!'
check(a.call('api/register',{'name':'QAA_'+suffix,'password':password},csrf=False)[0]==400,'Registration requires CSRF')
check(a.call('api/register',{'name':'<script>','password':password})[0]==400,'Unsafe account names rejected')
for client,name in [(a,'QAA_'+suffix),(b,'QAB_'+suffix)]:
    check(client.call('api/register',{'name':name,'email':name+'@example.test','password':password})[0]==200,'Register independent account')
    client.session()
check(all(any(key.lower()=='httponly' for key in cookie._rest) for cookie in a.jar),'Cookies are HttpOnly')
check(all(cookie.path=='/pyramid' or cookie.path=='/pyramid/' for cookie in a.jar),'Cookies scoped to application subfolder')
state=a.call('api/game')[1]
check(a.call('api/command',{'text':'get lamp','revision':state['revision']},csrf=False)[0]==400,'Commands require CSRF')
check(a.call('api/command',{'text':'get lamp','revision':state['revision']})[0]==200,'Authenticated command accepted')
check(a.call('api/command',{'text':'get lamp','revision':state['revision']})[0]==409,'Replay/stale revision rejected')
check(b.call('api/game')[1]['inventory']==[],'Second player state unaffected')
check(a.call('api/command',{'text':'x'*161,'revision':1})[0]==400,'Oversized command rejected')
check(a.call('api/game?userId=somebodyelse')[1]['inventory']==['lamp'],'Caller cannot choose another save')
roster=a.call('api/explorers')[1]
check(len(roster)>=2,'Roster includes separate explorers')
allowed={'id','name','level','room','score','moves','relics','solved','achievements','won','online','progress'}
check(all(set(player)==allowed for player in roster),'Roster exposes only allowlisted fields')
check('no-store' in a.call('api/game')[2]['Cache-Control'],'Private data is not cacheable')
for path in ['App_Data/pyramid.db','appsettings.json','Program.cs','Game/World.cs','api/game/other-user']:
    check(a.call(path)[0]==404,'Private/source path inaccessible: '+path)
# Hostile-looking journal text is data, never executable markup in the React UI.
state=a.call('api/game')[1]
a.call('api/command',{'text':'note <script>alert(1)</script>','revision':state['revision']})
check('<script>' in a.call('api/game')[1]['journal'][-1],'Journal text remains plain data')
old_session=next(c.value for c in a.jar if c.name=='Pyramid.Session')
check(a.call('api/logout',{})[0]==200,'Logout succeeds')
check(a.call('api/game')[0]==401,'Logged-out account blocked')
replay=urllib.request.Request(BASE+'api/game',headers={'Cookie':'Pyramid.Session='+old_session})
try: urllib.request.urlopen(replay); replay_status=200
except urllib.error.HTTPError as e: replay_status=e.code
check(replay_status==401,'Copied session cookie revoked by logout')
a.session()
check(a.call('api/login',{'name':'QAA_'+suffix,'password':password})[0]==200,'Account can sign in again')
a.session()
check(a.call('api/game')[1]['inventory']==['lamp'],'Progress resumes after sign-in')
for _ in range(5):
    status=b.call('api/login',{'name':'QAB_'+suffix,'password':'incorrect-password-123'})[0]
    check(status==401,'Wrong password rejected')
check(b.call('api/login',{'name':'QAB_'+suffix,'password':password})[0]==401,'Five failures lock the account')
statuses=[b.call('api/login',{'name':'nonexistent_account','password':password})[0] for _ in range(13)]
check(429 in statuses,'Authentication flood is rate limited')
print(count,'HTTP/security checks passed')
