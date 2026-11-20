import React from 'react';
import {createRoot} from 'react-dom/client';
import ExecutiveReports from '../src/components/vite/ExecutiveReports';
import '../src/vite.css';
createRoot(document.getElementById('root')!).render(<main className="workspace"><ExecutiveReports/></main>);
