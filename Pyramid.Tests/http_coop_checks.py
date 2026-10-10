"""Local-only HTTP checks for shared expeditions. Creates disposable QA accounts."""
import os, json, uuid, urllib.request, urllib.error, http.cookiejar
BASE=os.environ.get('PYRAMID_TEST_BASE','http://127.0.0.1:5189/pyramid/')
assert BASE.startswith(('http://localhost:','http://127.0.0.1:')), 'Local test server only'
class Client:
    def __init__(self):
        self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar())); self.token=''
    def call(self,path,data=None,csrf=True):
        headers={'Content-Type':'application/json'}
        if csrf:headers['X-Pyramid-CSRF']=self.token
        request=urllib.request.Request(BASE+path,headers=headers,data=None if data is None else json.dumps(data).encode())
        try:r=self.opener.open(request)
        except urllib.error.HTTPError as e:r=e
        raw=r.read()
        try:body=json.loads(raw)
        except ValueError:body=raw
        return r.status,body
    def session(self):self.token=self.call('api/session')[1]['token']
checks=0
def check(ok,label):
    global checks
    assert ok,label
    checks+=1;print('PASS',label)
a,b,anon=Client(),Client(),Client()
for endpoint in ['api/play','api/expeditions']:check(anon.call(endpoint)[0]==401,'Anonymous '+endpoint+' blocked')
suffix=uuid.uuid4().hex[:8]
for client,name in [(a,'Host_'+suffix),(b,'Guest_'+suffix)]:
    client.session();check(client.call('api/register',{'name':name,'email':name+'@example.test','password':'Local-QA-only-'+suffix})[0]==200,'Register co-op test explorer');client.session()
check(a.call('api/expeditions/create',{'name':'Denied','pack':'pyramid','open':False},False)[0]==400,'Expedition creation requires CSRF')
status,result=a.call('api/expeditions/create',{'name':'HTTP shared validation','pack':'pyramid','open':True});check(status==200,'Create shared expedition');game=result['id']
def act(c,action,target=''):return c.call('api/expeditions/action',{'id':game,'action':action,'target':target})
check(act(b,'open')[0]==400,'Non-host cannot manage world')
check(act(b,'accept')[0]==400,'Open world still requires admission')
check(act(b,'request')[0]==200,'Request admission')
state=a.call('api/play')[1];guest=next(iter(state['party']['requests']))
check(act(a,'approve',guest)[0]==200 and act(b,'accept')[0]==200,'Host approval and guest acceptance')
def command(c,text,revision=None,expedition=game):
    view=c.call('api/play')[1]
    return c.call('api/play/command',{'text':text,'revision':view['game']['revision'] if revision is None else revision,'expeditionId':expedition})
state=a.call('api/play')[1]
check(command(a,'get bottle')[0]==200,'Shared pickup succeeds')
check(command(b,'get bottle',state['game']['revision'])[0]==409,'Shared stale revision rejected')
check(command(b,'look',expedition='wrong-world')[0]==409,'Stale world identity rejected')
check(command(a,'share item bottle with Guest_'+suffix)[0]==200,'Offer an item')
check(command(b,'get bottle from Host_'+suffix)[0]==200,'Accept offered item')
check('bottle' in b.call('api/play')[1]['game']['inventory'] and 'bottle' not in a.call('api/play')[1]['game']['inventory'],'Exactly one owner after HTTP handoff')
command(a,'north');command(a,'west');command(b,'north');command(b,'east')
check(a.call('api/play')[1]['game']['roomId']=='west' and b.call('api/play')[1]['game']['roomId']=='east','Independent player locations via HTTP')
check(b.call('api/logout',{})[0]==200,'Guest signs out')
party=a.call('api/play')[1]['party'];check(not next(p for p in party['members'] if p['id']==guest)['online'],'Logout immediately marks trance')
b.session();check(b.call('api/login',{'name':'Guest_'+suffix,'password':'Local-QA-only-'+suffix})[0]==200,'Guest returns');b.session()
check(b.call('api/play')[1]['game']['roomId']=='east','Guest location resumes after sign-in')
for path in ['ContentPacks/oasis-of-stars.json','ContentPacks/oasis-moon-addon.json','App_Data/pyramid.db','keys/key.xml']:
    check(anon.call(path)[0]==404,'Private path blocked: '+path)
for n in range(1,61):
    status,png=anon.call('room-art/room'+str(n)+'.png');assert status==200 and png.startswith(b'\x89PNG'),n
check(True,'All 60 numbered PNGs load under the subfolder')
check(anon.call('GAMEPLAY-API-MODS.txt')[0]==200,'Player API/mod help bundled')
check(anon.call('HOW-TO-PLAY.html')[0]==200,'How-to-play available before sign-in')
before=a.call('api/play')[1]['game'];status,mapped=command(a,'map');mapped=mapped['game']
check(status==200 and mapped['mapsUsed']==before['mapsUsed']+1 and mapped['score']==max(0,before['score']-2),'MAP charges two points through HTTP')
known={r['id'] for r in mapped['visited']};chart=mapped['mapSnapshot']
check({n['id'] for n in chart['nodes']}==known and all(e['from'] in known and e['to'] in known for e in chart['edges']),'MAP omits unvisited rooms and routes')
other=b.call('api/play')[1]['game'];status,cleared=command(a,'clear')
check(status==200 and cleared['game']['transcript']==[],'CLEAR empties personal transcript')
check(b.call('api/play')[1]['game']['transcript']==other['transcript'],'CLEAR leaves companion transcript intact')
check(cleared['game']['inventory']==mapped['inventory'] and cleared['game']['mapsUsed']==mapped['mapsUsed'],'CLEAR preserves inventory and costs')
print(str(checks)+' HTTP co-op/art checks passed')
