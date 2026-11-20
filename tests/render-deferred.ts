import {renderToPipeableStream} from 'react-dom/server';
import {PassThrough} from 'node:stream';
import type {ReactNode} from 'react';

// Static markup stops at Suspense. Await real lazy modules when asserting their content.
export const renderDeferred = (element: ReactNode) => new Promise<string>((resolve,reject) => {
  const output = new PassThrough(); let html = '';
  output.on('data',chunk => html += chunk);
  output.on('end',() => resolve(html.replace(/<!--[\s\S]*?-->/g,'')));
  const stream = renderToPipeableStream(element, {onAllReady() {stream.pipe(output);}, onError: reject});
});
