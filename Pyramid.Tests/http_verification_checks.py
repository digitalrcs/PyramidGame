"""Isolated mandatory-verification integration checks. Never use against real data."""
import argparse, email.parser, email.policy, http.cookiejar, json, pathlib, re, secrets, sqlite3, time, urllib.error, urllib.request
p=argparse.ArgumentParser();p.add_argument('--data',required=True);args=p.parse_args()
root=pathlib.Path(args.data).resolve();base='http://127.0.0.1:5198/pyramid/';checks=0
assert (root/'VERIFICATION-QA.marker').is_file(), 'Explicit isolated-fixture marker required'
db=root/'pyramid.db'
with sqlite3.connect(db) as conn: assert conn.execute('select count(*) from users').fetchone()[0]==0, 'Fresh empty fixture required'
class Client:
 def __init__(self):self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()));self.token=''
 def call(self,path,body=None):
  headers={'Content-Type':'application/json','X-Pyramid-CSRF':self.token};data=None if body is None else json.dumps(body).encode()
  try:
   with self.opener.open(urllib.request.Request(base+'api/'+path,data=data,headers=headers),timeout=15) as r:return r.status,json.loads(r.read() or '{}')
  except urllib.error.HTTPError as e:return e.code,json.loads(e.read() or '{}')
 def session(self):status,data=self.call('session');assert status==200;self.token=data['token'];return data
 def register(self,name,password):
  self.session();status,data=self.call('register',{'name':name,'email':name.lower()+'@example.test','password':password});check(status==200,'fictional registration succeeds');self.session();return data

def check(value,name):
 global checks
 assert value,name
 checks+=1;print('PASS',name,flush=True)
def token_for(name):
 deadline=time.monotonic()+40
 while time.monotonic()<deadline:
  for file in (root/'MailCapture').glob('*.eml'):
   with file.open('rb') as stream:mail=email.parser.BytesParser(policy=email.policy.default).parse(stream)
   if name.lower()+'@example.test' in str(mail['To']).lower() and 'Verify your Pyramid email' in str(mail['Subject']):
    text=mail.get_body(preferencelist=('plain',)).get_content();match=re.search(r'#verify=([A-Fa-f0-9]{64})',text)
    if match:return match[1]
  time.sleep(.3)
 raise AssertionError('Expected local captured verification email')
def gated(client):
 for route,body in [('play',None),('game',None),('explorers',None),('expeditions',None),('play/command',{'text':'look','revision':0,'expeditionId':None}),('command',{'text':'look','revision':0}),('expeditions/create',{'name':'Blocked world','pack':'pyramid','open':False}),('expeditions/action',{'id':'','action':'solo','target':''})]:
  status,data=client.call(route,body);check(status==403 and data.get('code')=='verification_required','unverified access blocked: '+route)
password=secrets.token_urlsafe(26)
admin,player,manual,pending=Client(),Client(),Client(),Client()
admin_data=admin.register('Verify_Admin',password)
check(admin.call('verify-email',{'token':token_for('Verify_Admin')})[0]==200,'administrator verification captured locally')
with sqlite3.connect(db) as conn:conn.execute('update users set isAdmin=1 where id=?',(admin_data['id'],))
check(admin.session()['user']['isAdmin'],'isolated fixture administrator promoted')
status,settings=admin.call('admin/settings');check(status==200 and settings['mail']['mode']=='Capture','Capture-only mail confirmed')
player_data=player.register('Verify_Player',password)
check(player.session()['user']['verificationRequired'],'new account gets limited session')
check(player.call('account')[0]==200,'limited session can manage account')
gated(player)
check(player.call('logout',{})[0]==200,'limited session can sign out')
player.session();check(player.call('login',{'name':'Verify_Player','password':password})[0]==200,'username sign-in accepted for verification-only session')
check(player.session()['user']['verificationRequired'],'sign-in does not bypass verification')
gated(player)
check(player.call('verify-email',{'token':'0'*64})[0]==400,'invalid verification token rejected')
check(player.call('play')[0]==403,'invalid token leaves gameplay blocked')
check(player.call('verify-email',{'token':token_for('Verify_Player')})[0]==200,'captured verification link accepted')
check(not player.session()['user']['verificationRequired'],'existing session becomes verified')
for route in ['play','game','explorers','expeditions']:check(player.call(route)[0]==200,'verified access allowed: '+route)
check(player.call('account/email',{'email':'verify_changed@example.test','password':password})[0]==200,'verified account can request replacement email')
check(player.call('account')[1]['pendingEmail']=='verify_changed@example.test','replacement email stays pending')
check(not player.session()['user']['verificationRequired'] and player.call('play')[0]==200,'pending replacement preserves existing approved gameplay')
manual_data=manual.register('Verify_Manual',password)
check(manual.call('play')[0]==403,'manual-approval candidate blocked initially')
check(admin.call('admin/action',{'target':manual_data['id'],'action':'approve','confirmation':'Verify_Manual','expectedEmail':'verify_manual@example.test'})[0]==200,'administrator approves current address')
check(not manual.session()['user']['verificationRequired'] and manual.call('play')[0]==200,'manual approval unlocks existing limited session')
pending.register('Verify_Pending',password)
check(pending.session()['user']['verificationRequired'],'pending fixture retained for browser review')
check(pending.call('logout',{})[0]==200,'pending explorer can sign out')
pending.session();check(pending.call('login',{'name':'verify_pending@example.test','password':password})[0]==200,'unique pending email supports limited sign-in')
check(pending.session()['user']['verificationRequired'] and pending.call('play')[0]==403,'pending-email sign-in remains gated')
(root/'browser-credentials.json').write_text(json.dumps({'url':base,'admin':'Verify_Admin','verified':'Verify_Player','pending':'Verify_Pending','password':password}),encoding='utf-8')
(root/'http-results.json').write_text(json.dumps({'checks':checks,'status':'passed','mailMode':'Capture'}),encoding='utf-8')
print(f'{checks} verification-gate HTTP checks passed. Browser fixture credentials stored privately; no SMTP sent.',flush=True)

