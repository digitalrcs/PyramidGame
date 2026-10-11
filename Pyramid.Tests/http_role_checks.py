"""Role/owner HTTP checks against the disposable verification fixture only."""
import argparse,http.cookiejar,json,pathlib,urllib.request,urllib.error
p=argparse.ArgumentParser();p.add_argument('--data',required=True);args=p.parse_args();root=pathlib.Path(args.data).resolve();assert (root/'VERIFICATION-QA.marker').exists()
credentials=json.loads((root/'browser-credentials.json').read_text());assert credentials['url']=='http://127.0.0.1:5198/pyramid/'
checks=0
class Client:
 def __init__(self):self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()));self.token=''
 def call(self,path,body=None):
  try:
   with self.opener.open(urllib.request.Request(credentials['url']+'api/'+path,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json','X-Pyramid-CSRF':self.token}),timeout=15) as r:return r.status,json.loads(r.read() or '{}')
  except urllib.error.HTTPError as e:return e.code,json.loads(e.read() or '{}')
 def session(self):status,data=self.call('session');assert status==200;self.token=data['token'];return data
 def login(self,name):self.session();assert self.call('login',{'name':name,'password':credentials['password']})[0]==200;return self.session()
def check(value,name):
 global checks
 assert value,name
 checks+=1;print('PASS',name,flush=True)
owner,manual,player=Client(),Client(),Client();osession=owner.login(credentials['admin']);manual.login('Verify_Manual');player.login(credentials['verified'])
check(osession['user']['isGod'] and osession['user']['role']=='god','operator-provisioned owner exposed in session')
users=owner.call('admin/users')[1]['users'];ids={u['name']:u['id'] for u in users};oid=ids[credentials['admin']];mid=ids['Verify_Manual'];pid=ids[credentials['verified']]
check(player.call('admin/roles')[0]==403,'ordinary explorer cannot read role catalog')
check(player.call('admin/role',{'target':mid,'role':'admin','expectedRole':'explorer','confirmation':'Verify_Manual'})[0]==403,'ordinary explorer cannot grant roles')
check(owner.call('admin/role',{'target':oid,'role':'explorer','expectedRole':'god','confirmation':credentials['admin']})[0]==400,'owner cannot demote self')
check(owner.call('admin/role',{'target':mid,'role':'moderator','expectedRole':'explorer','confirmation':'Verify_Manual'})[0]==400,'unimplemented role rejected')
before=manual.call('play')[1]['game'];check(owner.call('admin/role',{'target':mid,'role':'admin','expectedRole':'explorer','confirmation':'Verify_Manual'})[0]==200,'owner promotes an explorer')
check(manual.call('play')[0]==401,'promotion revokes existing sessions')
msession=manual.login('Verify_Manual');check(msession['user']['role']=='admin' and not msession['user']['isGod'],'promoted account is admin not owner')
after=manual.call('play')[1]['game'];check(before['score']==after['score'] and before['moves']==after['moves'],'role promotion preserves gameplay')
check(owner.call('admin/role',{'target':mid,'role':'explorer','expectedRole':'explorer','confirmation':'Verify_Manual'})[0]==409,'stale role change rejected')
check(manual.call('admin/role',{'target':pid,'role':'god','expectedRole':'explorer','confirmation':credentials['verified']})[0]==400,'administrator cannot assign owner role')
for action in ['kick','suspend','delete','approve','reset-solo']:
 status,_=manual.call('admin/action',{'target':oid,'action':action,'confirmation':credentials['admin'],'expectedEmail':credentials['admin'].lower()+'@example.test'});check(status in (400,403),'ordinary administrator cannot '+action+' owner')
check(manual.call('admin/role',{'target':oid,'role':'explorer','expectedRole':'god','confirmation':credentials['admin']})[0] in (400,403),'ordinary administrator cannot change owner role')
check(manual.call('admin/backup/import',{'confirmation':'REPLACE ALL DATA','backup':{}})[0]==403,'ordinary administrator cannot restore when owner is bound')
status,backup=owner.call('admin/backup');check(status==200,'owner can export portable backup')
check('installation_owner' not in json.dumps(backup),'installation owner binding excluded from portable export')
import copy
for field,value in [('isAdmin',0),('suspended',1),('name','ReplacementOwner')]:
 changed=copy.deepcopy(backup)
 row=next(row for row in changed['tables']['users'] if row['id']==oid)
 row[field]=value
 check(owner.call('admin/backup/import',{'confirmation':'REPLACE ALL DATA','backup':changed})[0]==400,'restore rejects altered owner '+field)
check(owner.session()['user']['isGod'],'rejected restore preserves installation owner')

check(owner.call('admin/role',{'target':mid,'role':'explorer','expectedRole':'admin','confirmation':'Verify_Manual'})[0]==200,'owner demotes another administrator')
check(manual.call('admin/users')[0]==401,'demotion immediately revokes session')
manual.login('Verify_Manual');check(manual.call('admin/users')[0]==403,'demoted explorer loses administrative access')
# Leave a second administrator for parent browser verification.
check(owner.call('admin/role',{'target':mid,'role':'admin','expectedRole':'explorer','confirmation':'Verify_Manual'})[0]==200,'ordinary administrator fixture restored for browser review')
pending=Client();pending.session();check(pending.call('login',{'name':'verify_pending@example.test','password':credentials['password']})[0]==200,'pending email permits limited sign-in');check(pending.session()['user']['verificationRequired'] and pending.call('play')[0]==403,'pending email sign-in cannot bypass verification')
(root/'role-results.json').write_text(json.dumps({'checks':checks,'status':'passed'}))
print(f'{checks} role/owner HTTP checks passed. No production data or SMTP used.')

