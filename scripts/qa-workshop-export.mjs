import {createRequire} from 'node:module';
import {pathToFileURL} from 'node:url';
import fs from 'node:fs/promises';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {FileBlob,SpreadsheetFile}=await import(pathToFileURL(require.resolve('@oai/artifact-tool')).href);
const book=await SpreadsheetFile.importXlsx(await FileBlob.load('tmp/pdfs/taller-export-qa.xlsx'));
for(const name of ['Resumen','Equipos','Historial']){
 const png=await book.render({sheetName:name,autoCrop:'all',scale:1,format:'png'});
 await fs.writeFile(`tmp/pdfs/taller-excel-${name}.png`,new Uint8Array(await png.arrayBuffer()));
}
console.log('PASS Excel importado y tres hojas renderizadas');
