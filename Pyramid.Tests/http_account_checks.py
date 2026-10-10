"""Account integration checks. LOCAL isolated Capture-mode server only; no SMTP sends.
Preseed Admin_QA as admin. Set PYRAMID_MAIL_CAPTURE to private MailCapture directory.
Optional PYRAMID_TEST_BASE (default localhost:5191/pyramid/) and PYRAMID_ADMIN_PASSWORD.
"""
import os,json,uuid,time,re,email.policy,email.parser,urllib.request,urllib.error,http.cookiejar
from pathlib import Path
BASE=os.environ.get('PYRAMID_TEST_BASE','http://127.0.0.1:5191/pyramid/')
assert BASE.startswith(('http://localhost:5191/','http://127.0.0.1:5191/')), 'Isolated local port 5191 required'
CAPTURE=Path(os.environ['PYRAMID_MAIL_CAPTURE'])
checks=0
class Client:
    def __init__(self):
        self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()));self.token=''
    def call(self,path,data=None,csrf=True):
        for attempt in range(9):
            headers={'Content-Type':'application/json'}
            if csrf:headers['X-Pyramid-CSRF']=self.token
            req=urllib.request.Request(BASE+path,headers=headers,data=None if data is None else json.dumps(data).encode())
            try:response=self.opener.open(req,timeout=20)
            except urllib.error.HTTPError as error:response=error
            raw=response.read()
            try:body=json.loads(raw)
            except ValueError:body=raw
            if response.status!=429:return response.status,body
            print('Authentication throttle active; waiting briefly',flush=True);time.sleep(8)
        return response.status,body
    def session(self):
        status,result=self.call('api/session');assert status==200;self.token=result['token'];return result

def check(condition,label):
    global checks
    assert condition,label
    checks+=1;print('PASS',label,flush=True)

def captured(recipient,subject,after=0):
    deadline=time.monotonic()+35
    while time.monotonic()<deadline:
        for path in sorted(CAPTURE.glob('*.eml'),key=lambda p:p.stat().st_mtime,reverse=True):
            if path.stat().st_mtime<after:continue
            with path.open('rb') as stream:message=email.parser.BytesParser(policy=email.policy.default).parse(stream)
            if recipient.lower() in str(message['To']).lower() and subject.lower() in str(message['Subject']).lower():
                return message.get_body(preferencelist=('plain',)).get_content()
        time.sleep(.5)
    raise AssertionError('Expected captured mail missing: '+subject)

def token_from(body,kind):
    match=re.search(r'#'+kind+r'=([A-Fa-f0-9]{64})',body)
    assert match,'Captured link missing';return match[1]

def login(client,name,password):
    client.session();result=client.call('api/login',{'name':name,'password':password})
    if result[0]==200:client.session()
    return result

suffix=uuid.uuid4().hex[:8];password='Local-Account-QA-'+suffix;newpassword=password+'-reset'
admin=Client();check(login(admin,os.environ.get('PYRAMID_ADMIN_NAME','Admin_QA'),os.environ.get('PYRAMID_ADMIN_PASSWORD','Local-Admin-QA-2026!'))[0]==200,'Isolated administrator sign-in')
check(admin.session()['user']['isAdmin'],'Seeded administrator role exposed')
status,summary=admin.call('api/admin/users');check(status==200,'Administrator can inspect users')
# This guard prevents the script from accidentally delivering internet mail.
mail=summary.get('mail',{})
check('Capture' in json.dumps(mail),'Server confirms Capture mode')
owner,guest,anon=Client(),Client(),Client();anon.session()
identities={}
for client,prefix in [(owner,'Owner'),(guest,'Guest')]:
    name=prefix+'_'+suffix;address=name.lower()+'@example.test';client.session()
    status,result=client.call('api/register',{'name':name,'email':address,'password':password})
    check(status==200,'Register '+prefix+' with email');client.session();identities[prefix]=(name,address,result['id'])
ownername,owneremail,ownerid=identities['Owner'];guestname,guestemail,guestid=identities['Guest']
check(login(anon,owneremail,password)[0]==401,'Unverified email cannot sign in')
ownerverify=token_from(captured(owneremail,'Verify'),'verify')
check(anon.call('api/verify-email',{'token':ownerverify})[0]==200,'Verification consumes token only on explicit POST')
check(anon.call('api/verify-email',{'token':ownerverify})[0]==400,'Verification token cannot be reused')
check(login(anon,owneremail,password)[0]==200,'Verified email signs in');anon.call('api/logout',{});anon.session()
guestverify=token_from(captured(guestemail,'Verify'),'verify')
check(anon.call('api/verify-email',{'token':guestverify})[0]==200,'Guest email verified')
check(owner.call('api/account')[1]['emailVerified'],'Account view reflects verification')
check(owner.call('api/admin/users')[0]==403,'Ordinary explorer cannot inspect admin data')
check(owner.call('api/admin/action',{'target':guestid,'action':'kick','confirmation':''})[0]==403,'Ordinary explorer cannot moderate')
check(admin.call('api/admin/action',{'target':guestid,'action':'kick','confirmation':''},False)[0]==400,'Administrator action requires CSRF')
check(admin.call('api/admin/action',{'target':admin.session()['user']['id'],'action':'delete','confirmation':'Admin_QA'})[0]==400,'Administrator cannot delete self')
started=time.time();known=anon.call('api/forgot-password',{'email':owneremail});unknown=anon.call('api/forgot-password',{'email':'absent-'+suffix+'@example.test'})
check(known==unknown and known[0]==200,'Forgot password does not enumerate accounts')
reset=token_from(captured(owneremail,'Reset',started),'reset')
check(anon.call('api/reset-password',{'token':reset,'password':newpassword})[0]==200,'Reset password succeeds with captured single-use link')
check(owner.call('api/play')[0]==401,'Password reset revokes old session')
check(anon.call('api/reset-password',{'token':reset,'password':newpassword})[0]==400,'Reset token cannot be reused')
check(login(owner,owneremail,password)[0]==401,'Old password no longer works')
check(login(owner,owneremail,newpassword)[0]==200,'New password signs in by email')
status,result=owner.call('api/expeditions/create',{'name':'Account lifecycle QA '+suffix,'pack':'pyramid','open':False});check(status==200,'Create disposable shared expedition');world=result['id']
started=time.time();check(owner.call('api/expeditions/action',{'id':world,'action':'invite','target':guestname})[0]==200,'Invite verified guest')
invite=captured(guestemail,'invitation',started);check('#expedition='+world in invite,'Invitation email contains expedition deep link')
check(guest.call('api/expeditions/action',{'id':world,'action':'accept','target':''})[0]==200,'Guest explicitly accepts invitation')

def command(client,text):
    play=client.call('api/play')[1];return client.call('api/play/command',{'text':text,'revision':play['game']['revision'],'expeditionId':world})
check(command(owner,'get bottle')[0]==200,'Host holds a unique world item before moderation')
started=time.time();check(admin.call('api/admin/message',{'target':guestid,'subject':'QA bulletin '+suffix,'message':'Meet at the desert threshold.'})[0]==200,'Administrator message queues')
check('Meet at the desert threshold.' in captured(guestemail,'QA bulletin',started),'Administrator message captured with expected content')
check(admin.call('api/admin/action',{'target':ownerid,'action':'kick','confirmation':''})[0]==200,'Kick ends explorer sessions')
check(owner.call('api/play')[0]==401,'Kicked session is rejected')
check(login(owner,owneremail,newpassword)[0]==200,'Kicked explorer can return')
check('bottle' in owner.call('api/play')[1]['game']['inventory'],'Kick preserves progress and inventory')
check(admin.call('api/admin/action',{'target':ownerid,'action':'suspend','confirmation':ownername})[0]==200,'Suspend explorer')
check(owner.call('api/play')[0]==401 and login(owner,owneremail,newpassword)[0]==401,'Suspension revokes session and prevents sign-in')
check(admin.call('api/admin/action',{'target':ownerid,'action':'restore','confirmation':''})[0]==200,'Restore explorer')
check(login(owner,owneremail,newpassword)[0]==200,'Restored explorer resumes')
check(admin.call('api/admin/action',{'target':ownerid,'action':'delete','confirmation':'wrong-name'})[0]==400,'Deletion requires exact explorer name')
check(admin.call('api/admin/action',{'target':ownerid,'action':'delete','confirmation':ownername})[0]==200,'Delete disposable host account')
check(owner.call('api/play')[0]==401,'Deleted account session is invalid')
play=guest.call('api/play')[1]
check(play['party']['owner']==guestid and len(play['party']['members'])==1,'Shared expedition ownership transfers to survivor')
check(command(guest,'get bottle')[0]==200 and guest.call('api/play')[1]['game']['inventory'].count('bottle')==1,'Deleted host item returned and recoverable once')
command(guest,'get bottle');check(guest.call('api/play')[1]['game']['inventory'].count('bottle')==1,'Repeated pickup cannot duplicate returned item')
summary=admin.call('api/admin/users')[1]
check(all(p['id']!=ownerid for p in summary['users']),'Deleted personal account removed from admin listing')
check(all(any(a['action']==action for a in summary['audit']) for action in ['kick','suspend','restore','delete']),'Moderation audit records retained')
check(admin.call('api/admin/action',{'target':guestid,'action':'delete','confirmation':guestname})[0]==200,'Clean up disposable guest')
print(str(checks)+' HTTP account checks passed',flush=True)
