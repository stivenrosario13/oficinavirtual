"""Read the user's Excel; preserve existing product IDs/metadata where the name identifies them.
The one missing source quantity must be supplied by the user, never silently treated as zero.
"""
import argparse, collections, hashlib, json, re, unicodedata
from pathlib import Path
import openpyxl

parser=argparse.ArgumentParser()
parser.add_argument('--source',default=r'C:/Users/pc/Downloads/Inventario corregido.xlsx')
parser.add_argument('--sumadora-quantity',type=int,required=True)
args=parser.parse_args()
assert 0<=args.sumadora_quantity<=2147483647
root=Path(__file__).resolve().parent.parent
normalize=lambda value:re.sub(r'[^a-z0-9]','',unicodedata.normalize('NFKD',value).encode('ascii','ignore').decode().lower())
old=json.loads((root/'api/Data/warehouse-products-2026-09-28.json').read_text(encoding='utf-8-sig'))['products']
index=collections.defaultdict(list)
for product in old:index[normalize(product['descripcion'])].append(product)
workbook=openpyxl.load_workbook(args.source,read_only=True,data_only=True)
sheet=workbook['Hoja1'];rows=list(sheet.values)
assert rows[0][1]=='Cantidad' and len(rows)==135 and sheet.max_column==2
products=[];mapping=[];seen=set()
for row_number,(description,quantity) in enumerate(rows[1:],2):
    assert isinstance(description,str) and description.strip()
    description=description.strip();key=normalize(description)
    assert key not in seen,f'Duplicate source product: {description}'
    seen.add(key)
    if quantity is None:
        assert row_number==102 and description=='Rollo Papel Para Sumadora'
        quantity=args.sumadora_quantity
    assert isinstance(quantity,(int,float)) and int(quantity)==quantity and 0<=quantity<=2147483647
    matches=index[key]
    # Prefer the exact label; old duplicate names with zero stock are retired but never deleted.
    exact=[p for p in matches if p['descripcion']==description]
    candidates=exact or matches
    existing=max(candidates,key=lambda p:p['existencia']) if candidates else None
    if existing:
        product=dict(existing)
    else:
        product={'idProducto':'COR-'+hashlib.sha256(key.encode()).hexdigest()[:16].upper(),
                 'producto':True,'almacen':'PRINCIPAL','idUnidad':'UND','division':'Producto Terminado',
                 'grupo':'Inventario corregido','fechaUltimaCompra':None,'seriales':False}
    product.update(descripcion=description,existencia=int(quantity))
    products.append(product)
    mapping.append({'row':row_number,'description':description,'quantity':int(quantity),'productId':product['idProducto'],
                    'existing':bool(existing),'previousDuplicateIds':[p['idProducto'] for p in matches if p['idProducto']!=product['idProducto']]})
assert len(products)==134 and len({p['idProducto'] for p in products})==134
data={'version':'2026-10-27-inventario-corregido-134','sourceFile':Path(args.source).name,'products':products}
(root/'api/Data/warehouse-products-2026-10-27.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
report={'sourceFile':Path(args.source).name,'sha256':hashlib.sha256(Path(args.source).read_bytes()).hexdigest(),
        'rows':len(products),'totalQuantity':sum(p['existencia'] for p in products),'sumadoraQuantityConfirmed':args.sumadora_quantity,
        'matchedExisting':sum(r['existing'] for r in mapping),'newProducts':sum(not r['existing'] for r in mapping),'mapping':mapping}
(root/'outputs/corrected-inventory-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='mapping'},ensure_ascii=True))
