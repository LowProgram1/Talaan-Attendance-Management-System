'use client';

import { useEffect, useRef, useState } from 'react';
import { CheckCircle2, Download, FileSpreadsheet, UploadCloud, X } from 'lucide-react';
import { post, request } from '@/lib/api';
import SectionCombobox from './section-combobox';
import { SectionOption } from './student-entry-dialog';

type ImportRow = { line:number; firstName:string; lastName:string; guardianEmail:string; errors:string[] };
type ImportResult = { imported:number; invalid:number; failed:{line:number;error:string}[] };

export default function StudentImportDialog({sectionId,onClose,onImported}:{sectionId?:string;onClose:()=>void;onImported:(message:string)=>void}) {
  const [sections,setSections]=useState<SectionOption[]>([]);
  const [target,setTarget]=useState(sectionId||'');
  const [file,setFile]=useState<File|null>(null);
  const [csv,setCsv]=useState('');
  const [rows,setRows]=useState<ImportRow[]>([]);
  const [busy,setBusy]=useState(false);
  const [dragging,setDragging]=useState(false);
  const [error,setError]=useState('');
  const [result,setResult]=useState<ImportResult|null>(null);
  const [page,setPage]=useState(1);
  const input=useRef<HTMLInputElement>(null);
  const dialog=useRef<HTMLDivElement>(null);
  const valid=rows.filter(r=>r.errors.length===0).length;
  const pages=Math.max(1,Math.ceil(rows.length/10));

  useEffect(()=>{
    const previous=document.activeElement as HTMLElement|null;
    dialog.current?.focus();
    return()=>previous?.focus();
  },[]);
  useEffect(()=>{
    let active=true;
    request<SectionOption[]>('/sections').then(s=>{if(active)setSections(s)}).catch(e=>{if(active)setError(e.message)});
    return()=>{active=false};
  },[]);

  function reset(){setFile(null);setCsv('');setRows([]);setResult(null);setError('');setPage(1);if(input.current)input.current.value='';}
  async function choose(files:FileList|null){
    if(busy||!files?.length)return;
    reset();
    if(!target){setError('Select a destination section before uploading.');return;}
    if(files.length!==1){setError('Choose one CSV file at a time.');return;}
    const next=files[0];
    if(!next.name.toLowerCase().endsWith('.csv')){setError('Use a .csv file. In Excel, save your spreadsheet as CSV UTF-8.');return;}
    if(next.size>1_000_000){setError('This file is too large. The maximum size is 1 MB.');return;}
    if(!next.size){setError('This file is empty. Add students using the template first.');return;}
    setFile(next);setBusy(true);
    try{
      const text=await next.text();
      setCsv(text);
      setRows(await post<ImportRow[]>('/students/import/preview',{csv:text,sectionId:target||null}));
    }catch(e){setError(e instanceof Error?e.message:'Unable to read this file.');}
    finally{setBusy(false);}
  }
  async function commit(){
    if(!target){setError('Select a destination section before importing.');return;}
    setBusy(true);setError('');
    try{
      const value=await post<ImportResult>('/students/import/commit',{csv,sectionId:target||null});
      setResult(value);
      onImported(`${value.imported} students imported; ${value.invalid} invalid rows skipped; ${value.failed.length} rows could not be imported.`);
    }catch(e){setError(e instanceof Error?e.message:'Import failed. Review the Student List before retrying.');setRows([]);}
    finally{setBusy(false);}
  }
  const template=`firstName,lastName,guardianName,guardianEmail\r\n,,,\r\n`;
  const destination=sections.find(s=>s.id===target);
  return <div className="modal-backdrop" onMouseDown={()=>{if(!busy)onClose()}}>
    <div ref={dialog} tabIndex={-1} className="modal student-import-modal" role="dialog" aria-modal="true" aria-labelledby="student-import-title" onMouseDown={e=>e.stopPropagation()} onKeyDown={e=>{
      if(e.key==='Escape'&&!busy)onClose();
      if(e.key==='Tab'){
        const controls=dialog.current?.querySelectorAll<HTMLElement>('button:not(:disabled),a[href],input:not(:disabled),select:not(:disabled)');
        if(!controls?.length)return;
        const first=controls[0],last=controls[controls.length-1];
        if(e.shiftKey&&(document.activeElement===first||document.activeElement===dialog.current)){e.preventDefault();last.focus()}
        else if(!e.shiftKey&&(document.activeElement===last||document.activeElement===dialog.current)){e.preventDefault();first.focus()}
      }
    }}>
      <div className="modal-head"><div><h2 id="student-import-title" className="section-title">Batch import students</h2><p className="muted">Upload, review, then import your student records.</p></div><button className="icon-button" disabled={busy} aria-label="Close batch import" onClick={onClose}><X size={20}/></button></div>
      {sectionId?<p className="import-destination">Importing into <strong>{destination?`${destination.grade} / ${destination.name}`:'the selected class'}</strong></p>:<div className="import-destination"><SectionCombobox sections={sections} value={target} disabled={busy||!!result} onChange={value=>{reset();setTarget(value)}}/></div>}
      <div className="import-template"><div><strong>Start with the template</strong><p className="muted">Fill in first name, last name, guardian name and email. All students in the file will be added to the selected destination section. School ID numbers are generated automatically.</p></div><a className="btn btn-outline" href={'data:text/csv;charset=utf-8,'+encodeURIComponent('\uFEFF'+template)} download="student-import-template.csv"><Download size={16}/>Download template</a></div>
      {!result&&<>
        <div className={`import-dropzone ${dragging?'is-dragging':''}`} onDragOver={e=>{e.preventDefault();if(!busy)setDragging(true)}} onDragLeave={()=>setDragging(false)} onDrop={e=>{e.preventDefault();setDragging(false);void choose(e.dataTransfer.files)}} aria-busy={busy}>
          <UploadCloud size={30}/><strong>{busy?'Processing your file…':'Drag and drop your CSV here'}</strong><span className="muted">One CSV file · Up to 1 MB</span>
          <button type="button" className="btn btn-outline" disabled={busy} onClick={()=>input.current?.click()}>{file?'Choose another file':'Browse files'}</button>
          <input ref={input} type="file" accept=".csv,text/csv" hidden disabled={busy} aria-label="Upload student CSV" onChange={e=>void choose(e.target.files)}/>
        </div>
        {file&&<div className="import-file"><FileSpreadsheet size={22}/><div><strong>{file.name}</strong><span className="muted">{Math.max(1,Math.ceil(file.size/1024))} KB</span></div><button className="icon-button" disabled={busy} aria-label="Remove selected file" onClick={reset}><X size={18}/></button></div>}
        {!!rows.length&&<><div className="import-counts" role="status"><span><strong>{rows.length}</strong> rows reviewed</span><span className="success"><strong>{valid}</strong> ready</span><span><strong>{rows.length-valid}</strong> need attention</span></div><p className="muted import-help">Only valid rows will be imported. Existing students are flagged; their enrollment stays unchanged.</p><div className="table-wrap import-table"><table className="table"><thead><tr><th>Row</th><th>Student</th><th>Validation</th></tr></thead><tbody>{rows.slice((page-1)*10,page*10).map(row=><tr key={row.line}><td>{row.line||'—'}</td><td>{`${row.firstName} ${row.lastName}`.trim()||'File'}</td><td>{row.errors.length?<span className="error">{row.errors.join(' ')}</span>:<span className="success">Ready to import</span>}</td></tr>)}</tbody></table></div>{pages>1&&<div className="import-pagination"><button className="btn btn-outline" disabled={page===1} onClick={()=>setPage(p=>p-1)}>Previous</button><span>Page {page} of {pages}</span><button className="btn btn-outline" disabled={page===pages} onClick={()=>setPage(p=>p+1)}>Next</button></div>}</>}
      </>}
      {result&&<div className="import-result" role="status"><CheckCircle2 size={30}/><h3>Import complete</h3><p>{result.imported} students added · {result.invalid} invalid rows skipped · {result.failed.length} failed</p>{result.failed.map(r=><p className="error" key={r.line}>Row {r.line}: {r.error}</p>)}{result.invalid>0&&<p className="muted">Correct the flagged rows in your file before uploading again. Already imported students will be detected as duplicates.</p>}</div>}
      {error&&<p className="error" role="alert">{error}</p>}
      <div className="form-actions"><button className="btn btn-outline" disabled={busy} onClick={onClose}>{result?'Done':'Cancel'}</button>{result?<button className="btn btn-primary" onClick={reset}>Import another file</button>:<button className="btn btn-primary" disabled={busy||!target||!valid} aria-busy={busy} onClick={commit}>{busy?'Processing…':'Import'}</button>}</div>
    </div>
  </div>;
}
