"""Read provider limits; optional single idempotent probe. Never prints secrets."""
from pathlib import Path
import json, hashlib, sys
from datetime import datetime, timezone
import urllib.request, urllib.error
root=Path(__file__).resolve().parents[1]
config={}
for line in (root/'.env.local').read_text(encoding='utf-8-sig').splitlines():
    if '=' in line and not line.lstrip().startswith('#'):
        k,v=line.split('=',1);config[k.strip()]=v.strip().strip('\"\'')
key=config['STORAGE_KEY']
class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs): return None
opener=urllib.request.build_opener(NoRedirect())
def request(route,data=None,headers=None):
    req=urllib.request.Request('https://storage-api.gptcomer.com/v1/'+route,data=data,headers={'Authorization':'Bearer '+key,**(headers or {})})
    try: return opener.open(req,timeout=60)
    except urllib.error.HTTPError as e: return e
result={'checkedAt':datetime.now(timezone.utc).isoformat()}
def capture(response):
    with response:
        return {'httpStatus':response.status,'retryAfter':response.headers.get('Retry-After'),'date':response.headers.get('Date'),'body':json.loads(response.read())}
for route in ('whoami','usage'):
    result[route]=capture(request(route))
if '--probe' in sys.argv:
    payload=(root/'public/loto-real-logo-transparent.png').read_bytes()
    digest=hashlib.sha256(payload).hexdigest()
    idem='registro-v1-'+hashlib.sha256(('integration-verification:'+digest).encode()).hexdigest()
    boundary='----StorageDiagnosticFixedBoundary'
    body=b''
    for name,value in {'visibility':'private','external_ref':'registro:'+digest,'kind':'integration-verification','folder':'registro-agencias'}.items():
        body+=f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode()
    body+=f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="archivo.png"\r\nContent-Type: image/png\r\n\r\n'.encode()+payload+f'\r\n--{boundary}--\r\n'.encode()
    response=request('files',body,{'Idempotency-Key':idem,'Content-Type':'multipart/form-data; boundary='+boundary})
    result['probe']=capture(response);result['probe']['sourceBytes']=len(payload)
output=json.dumps(result,indent=2,ensure_ascii=True).replace(key,'[REDACTED]')
path=root/'outputs/storage-provider-diagnostic.json';path.write_text(output,encoding='utf-8');print(output)
