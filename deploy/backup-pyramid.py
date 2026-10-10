#!/usr/bin/python3
"""Root-only consistent SQLite backup plus matching keys/config/content."""
import datetime, json, os, pathlib, shutil, sqlite3
os.umask(0o077)
data=pathlib.Path('/var/lib/digitalrcs-pyramid')
base=pathlib.Path('/var/backups/digitalrcs-pyramid')
base.mkdir(mode=0o700,parents=True,exist_ok=True)
stamp=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
dest=base/stamp
dest.mkdir(mode=0o700)
source=sqlite3.connect('file:'+str(data/'pyramid.db')+'?mode=ro',uri=True)
target=sqlite3.connect(dest/'pyramid.db')
source.backup(target)
assert target.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
source.close();target.close()
shutil.copytree(data/'keys',dest/'keys')
current=pathlib.Path('/opt/digitalrcs-pyramid/current').resolve()
shutil.copytree(current/'ContentPacks',dest/'ContentPacks')
shutil.copy2('/etc/digitalrcs-pyramid.env',dest/'service.env')
(dest/'release.json').write_text(json.dumps({'release':str(current),'createdUtc':stamp}))
print('Pyramid backup verified:',dest)
