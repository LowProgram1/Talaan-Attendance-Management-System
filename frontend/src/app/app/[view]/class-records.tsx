'use client';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Archive, ClipboardCheck, Download, Pencil, Plus, RotateCcw, Search, Upload } from 'lucide-react';
import { post, put, request } from '@/lib/api';

type Status = 'Present' | 'Absent' | 'Late' | 'Excused';
type Student = { id: string; studentNumber: string; firstName: string; lastName: string; guardians: string[]; status: Status | null; reason?: string | null };
type Roster = { date: string; revision: number; roster: Student[] };
type StudentList = { items: {id:string;studentNumber:string;firstName:string;middleName?:string;lastName:string;suffix?:string;guardians:{fullName:string;email:string}[]}[];total:number };
type ImportRow = { line:number; firstName:string; lastName:string; errors:string[] };
const statuses: Status[] = ['Present','Absent','Late','Excused'];
const schoolToday = () => new Intl.DateTimeFormat('en-CA', {timeZone:'Asia/Manila',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());

export default function ClassRecords({sectionId,scheduleId,canEdit}:{sectionId:string;scheduleId:string;canEdit:boolean}) {
  const [date,setDate] = useState(schoolToday);
  const [roster,setRoster] = useState<Roster|null>(null);
  const [students,setStudents] = useState<StudentList|null>(null);
  const [selections,setSelections] = useState<Record<string,Status>>({});
  const [excuseReasons,setExcuseReasons] = useState<Record<string,string>>({});
  const [page,setPage] = useState(1);
  const [search,setSearch] = useState('');
  const [query,setQuery] = useState('');
  const rosterCache = useRef<{key:string;data:Roster}|null>(null);
  const [archived,setArchived] = useState(false);
  const [loading,setLoading] = useState(true);
  const [busy,setBusy] = useState(false);
  const [error,setError] = useState('');
  const [message,setMessage] = useState('');
  const [modal,setModal] = useState<'add'|'edit'|'import'|null>(null);
  const [selected,setSelected] = useState<StudentList['items'][number]|null>(null);
  const [form,setForm] = useState({firstName:'',middleName:'',lastName:'',suffix:'',guardianName:'',guardianEmail:''});
  const [csv,setCsv] = useState('');
  const [preview,setPreview] = useState<ImportRow[]>([]);
  const [submissionKey,setSubmissionKey] = useState(() => crypto.randomUUID());
  const [reason,setReason] = useState('');

  async function reload() {
    rosterCache.current=null;
    setLoading(true); setError('');
    try {
      const [nextStudents,nextRoster] = await Promise.all([
        request<StudentList>(`/students?sectionId=${sectionId}&page=${page}&search=${encodeURIComponent(search)}&archived=${archived}`),
        scheduleId && !archived ? request<Roster>(`/attendance/${scheduleId}?date=${date}`) : Promise.resolve(null)
      ]);
      setStudents(nextStudents); setRoster(nextRoster); setSelections({}); setExcuseReasons({});
      if(nextRoster)rosterCache.current={key:`${scheduleId}|${date}`,data:nextRoster};
    } catch (e) { setError(e instanceof Error ? e.message : 'Unable to load class records.'); }
    finally { setLoading(false); }
  }
  useEffect(()=>{const timer=setTimeout(()=>setQuery(search),250);return()=>clearTimeout(timer)},[search]);
  useEffect(()=>{
    let active=true;
    async function load(){
      setLoading(true);setError('');
      try{
        const key=`${scheduleId}|${date}`;
        const nextStudents=request<StudentList>(`/students?sectionId=${sectionId}&page=${page}&search=${encodeURIComponent(query)}&archived=${archived}`);
        const nextRoster=scheduleId&&!archived
          ? rosterCache.current?.key===key?Promise.resolve(rosterCache.current.data):request<Roster>(`/attendance/${scheduleId}?date=${date}`)
          : Promise.resolve(null);
        const [a,b]=await Promise.all([nextStudents,nextRoster]);
        if(active){setStudents(a);setRoster(b);if(b)rosterCache.current={key,data:b}}
      }catch(e){if(active)setError(e instanceof Error?e.message:'Unable to load class records.')}
      finally{if(active)setLoading(false)}
    }
    load();
    return()=>{active=false}
  },[sectionId,scheduleId,page,query,archived,date]);
  const effective = useMemo(() => Object.fromEntries((roster?.roster||[]).map(s => [s.id,selections[s.id]||s.status||'Present'])) as Record<string,Status>, [roster,selections]);
  const invalidExcuses = (roster?.roster||[]).some(s=>effective[s.id]==='Excused' && (!(excuseReasons[s.id]??s.reason??'').trim() || (excuseReasons[s.id]??s.reason??'').trim().length>500));
  const counts = statuses.map(label => ({label,count:Object.values(effective).filter(x=>x===label).length}));
  const rosterSize = Object.keys(effective).length;
  const attendanceRate = rosterSize ? Math.round(((counts[0].count + counts[3].count) / rosterSize) * 100) : 0;
  const studentById = new Map((students?.items||[]).map(s=>[s.id,s]));
  const visible = archived ? (students?.items||[]).map(s=>({id:s.id,studentNumber:s.studentNumber,firstName:s.firstName,lastName:s.lastName,guardians:s.guardians.map(g=>g.fullName),status:null,reason:null})) : scheduleId ? (roster?.roster||[]).filter(s=>`${s.firstName} ${s.lastName} ${s.studentNumber}`.toLowerCase().includes(search.toLowerCase())).slice((page-1)*20,page*20) : (students?.items||[]).map(s=>({id:s.id,studentNumber:s.studentNumber,firstName:s.firstName,lastName:s.lastName,guardians:s.guardians.map(g=>g.fullName),status:null,reason:null}));
  const total = archived || !scheduleId ? students?.total||0 : (roster?.roster||[]).filter(s=>`${s.firstName} ${s.lastName} ${s.studentNumber}`.toLowerCase().includes(search.toLowerCase())).length;
  const past = date < schoolToday();

  async function saveAttendance() {
    if (!roster || !scheduleId) return;
    setBusy(true); setError(''); setMessage('');
    try {
      const saved = await put<{revision:number}>(`/attendance/${scheduleId}`,{date,records:roster.roster.map(s=>({studentId:s.id,status:effective[s.id],reason:effective[s.id]==='Excused'?(excuseReasons[s.id]??s.reason??'').trim():null})),submissionKey,expectedRevision:roster.revision,correctionReason:reason});
      const nextRoster={...roster,revision:saved.revision,roster:roster.roster.map(s=>({...s,status:effective[s.id],reason:effective[s.id]==='Excused'?(excuseReasons[s.id]??s.reason??'').trim():null}))};
      setRoster(nextRoster); rosterCache.current={key:`${scheduleId}|${date}`,data:nextRoster};
      setSelections({}); setExcuseReasons({}); setSubmissionKey(crypto.randomUUID()); setReason('');
      setMessage('Class records submitted. The report now includes this revision.');
    } catch (e) {
      setSelections({}); setError(e instanceof Error?e.message:'Unable to submit attendance.');
      if ((e instanceof Error?e.message:'').includes('Refresh')) await reload();
    } finally { setBusy(false); }
  }
  async function saveStudent() {
    setBusy(true);setError('');
    try {
      const payload={...form,sectionId};
      if(modal==='edit'&&selected) await put(`/students/${selected.id}`,payload);
      else await post('/students',payload);
      setModal(null);setSelected(null);setForm({firstName:'',middleName:'',lastName:'',suffix:'',guardianName:'',guardianEmail:''});await reload();
      setMessage('Student record saved.');
    } catch(e){setError(e instanceof Error?e.message:'Unable to save student.')}
    finally{setBusy(false)}
  }
  async function toggleStudent(id:string,restore:boolean) {
    setBusy(true);setError('');
    try {await request(`/students/${id}${restore?'/restore':''}`,{method:restore?'POST':'DELETE'});await reload();setMessage(restore?'Student restored.':'Student archived.');}
    catch(e){setError(e instanceof Error?e.message:'Unable to update student.')}finally{setBusy(false)}
  }
  async function loadPreview(file:File) {setBusy(true);setCsv(await file.text());setPreview([]);}
  useEffect(()=>{if(!csv)return;let active=true;post<ImportRow[]>('/students/import/preview',{csv}).then(x=>{if(active)setPreview(x)}).catch(e=>{if(active)setError(e.message)}).finally(()=>{if(active)setBusy(false)});return()=>{active=false}},[csv]);
  async function commitImport(){setBusy(true);setError('');try{const result=await post<{imported:number,invalid:number}>('/students/import/commit',{csv});setMessage(`${result.imported} students imported; ${result.invalid} invalid rows skipped.`);setModal(null);setCsv('');setPreview([]);await reload()}catch(e){setError(e instanceof Error?e.message:'Import failed.')}finally{setBusy(false)}}

  return <div className="class-records">
    <div className="class-toolbar">
      <div><h2 className="section-title">Class Records</h2><p className="muted">Add students and submit one attendance record per student for each class day.</p></div>
      <div className="class-actions">
        <button className="btn btn-outline" onClick={()=>setModal('import')}><Upload size={17}/> Batch import</button>
        <button className="btn btn-primary" onClick={()=>{setSelected(null);setForm({firstName:'',middleName:'',lastName:'',suffix:'',guardianName:'',guardianEmail:''});setModal('add')}}><Plus size={17}/> Add student</button>
      </div>
    </div>
    <div className="class-filters"><div className="search-box"><Search size={18}/><input className="field" value={search} onChange={e=>{setSearch(e.target.value);setPage(1)}} placeholder="Search class records…" aria-label="Search class records"/></div>{canEdit&&<button className="btn btn-outline" onClick={()=>{setArchived(!archived);setPage(1)}}>{archived?'Active students':'Archived students'}</button>}{scheduleId&&canEdit&&!archived&&<label className="date-filter">Date <input className="field" type="date" max={schoolToday()} value={date} onChange={e=>{setDate(e.target.value);setPage(1);setSelections({});setExcuseReasons({});setSubmissionKey(crypto.randomUUID())}}/></label>}</div>
    {error&&<p className="error" role="alert">{error}</p>}{message&&<p className="success" role="status">{message}</p>}
    <div className={`class-records-layout ${scheduleId&&!archived?'with-summary':''}`}>
      <div className="card table-wrap">{loading?<div className="skeleton-table">{Array.from({length:6},(_,i)=><div className="skeleton-line" key={i}/>)}</div>:visible.length?<table className={`table class-table ${scheduleId&&!archived?'':'no-attendance'}`}><thead><tr><th>Student ID</th><th>Name</th><th>Guardian</th>{scheduleId&&!archived&&<th>Attendance</th>}<th>Actions</th></tr></thead><tbody>{visible.map(s=><tr key={s.id}><td>{s.studentNumber||'—'}</td><td><strong>{s.lastName}, {s.firstName}</strong></td><td>{s.guardians.join(', ')||'—'}</td>{scheduleId&&!archived&&<td><div className="status-options" role="group" aria-label={`Attendance for ${s.firstName} ${s.lastName}`}>{statuses.map(status=><button key={status} className={`status-choice ${effective[s.id]===status?'selected':''}`} type="button" onClick={()=>setSelections({...selections,[s.id]:status})} aria-pressed={effective[s.id]===status}>{status}</button>)}</div>{effective[s.id]==='Excused'&&<input className="field" style={{marginTop:8}} value={excuseReasons[s.id]??s.reason??''} onChange={e=>setExcuseReasons({...excuseReasons,[s.id]:e.target.value})} maxLength={500} placeholder="Reason for excused attendance" aria-label={`Excuse reason for ${s.firstName} ${s.lastName}`}/>}</td>}<td><div className="row-actions">{canEdit&&<button className="icon-button" title="Edit student" aria-label={`Edit ${s.firstName} ${s.lastName}`} onClick={()=>{const item=studentById.get(s.id);if(!item){setError('Open a page containing this student to edit their record.');return}setSelected(item);setForm({firstName:item.firstName,middleName:item.middleName||'',lastName:item.lastName,suffix:item.suffix||'',guardianName:item.guardians[0]?.fullName||'',guardianEmail:item.guardians[0]?.email||''});setModal('edit')}}><Pencil size={16}/></button>}{canEdit&&<button className="icon-button" title={archived?'Restore student':'Archive student'} aria-label={`${archived?'Restore':'Archive'} ${s.firstName} ${s.lastName}`} disabled={busy} onClick={()=>toggleStudent(s.id,archived)}>{archived?<RotateCcw size={16}/>:<Archive size={16}/>}</button>}</div></td></tr>)}</tbody></table>:<div className="empty">No students found. Add one to get started.</div>}</div>
      {scheduleId&&!archived&&<aside className="card class-summary" aria-label="Class attendance summary">
        {loading?<div className="skeleton-table"><div className="skeleton-line"/><div className="skeleton-line"/><div className="skeleton-line"/></div>:<>
          <p className="muted">Attendance rate</p>
          <strong className="class-summary-rate">{attendanceRate}%</strong>
          <div className="class-summary-track" role="progressbar" aria-label="Attendance rate" aria-valuemin={0} aria-valuemax={100} aria-valuenow={attendanceRate}><span style={{width:`${attendanceRate}%`}}/></div>
          <div className="class-summary-counts">{counts.map(x=><div key={x.label}><span className={`status-dot ${x.label.toLowerCase()}`}/><span>{x.label}</span><strong>{x.count}</strong></div>)}</div>
        </>}
        <div className="class-summary-submit">
          {canEdit&&past&&<input className="field" placeholder="Reason for past-day correction" value={reason} onChange={e=>setReason(e.target.value)} aria-label="Correction reason"/>}
          <button className="btn btn-orange" disabled={busy||loading||!roster?.roster.length||invalidExcuses||(canEdit&&past&&!reason.trim())} aria-busy={busy} onClick={saveAttendance}><ClipboardCheck size={17}/> Submit</button>
        </div>
      </aside>}
    </div>
    {total>20&&<div className="pagination"><button className="btn btn-outline" disabled={page===1} onClick={()=>setPage(page-1)}>Previous</button><span>Page {page} of {Math.ceil(total/20)}</span><button className="btn btn-outline" disabled={page>=Math.ceil(total/20)} onClick={()=>setPage(page+1)}>Next</button></div>}
    {modal&&<div className="modal-backdrop" onMouseDown={()=>setModal(null)}><div className="modal" role="dialog" aria-modal="true" aria-label={modal==='import'?'Batch import students':'Student form'} onMouseDown={e=>e.stopPropagation()}><div className="modal-head"><h2 className="section-title">{modal==='import'?'Batch import students':modal==='edit'?'Edit student':'Add student'}</h2><button className="icon-button" onClick={()=>setModal(null)} aria-label="Close">×</button></div>{modal==='import'?<><p className="muted">Upload a CSV with firstName, lastName, sectionId, guardianName, guardianEmail. Student IDs are generated automatically.</p><a className="template-link" href={'data:text/csv;charset=utf-8,'+encodeURIComponent(`firstName,lastName,sectionId,guardianName,guardianEmail\n,,${sectionId},,\n`)} download="student-import-template.csv"><Download size={16}/> Download CSV template</a><input type="file" accept=".csv,text/csv" onChange={e=>{if(e.target.files?.[0])loadPreview(e.target.files[0])}}/>{preview.length>0&&<div className="import-preview">{preview.map(row=><p key={row.line}>Row {row.line}: {row.firstName} {row.lastName} {row.errors.length?<span className="error">{row.errors.join('; ')}</span>:<span className="success">Valid</span>}</p>)}</div>}<button className="btn btn-primary" disabled={busy||!preview.some(x=>x.errors.length===0)} aria-busy={busy} onClick={commitImport}>Import valid rows</button></>:<form onSubmit={e=>{e.preventDefault();saveStudent()}}><div className="form-grid">{([{key:'firstName',label:'First name',required:true},{key:'middleName',label:'Middle name'},{key:'lastName',label:'Last name',required:true},{key:'suffix',label:'Suffix'},{key:'guardianName',label:'Guardian name',required:true},{key:'guardianEmail',label:'Guardian email',required:true,type:'email'}] as const).map(field=><div key={field.key}><label className="label">{field.label}</label><input className="field" type={'type' in field?field.type:'text'} value={form[field.key]} required={'required' in field&&field.required} onChange={e=>setForm({...form,[field.key]:e.target.value})}/></div>)}</div><div className="form-actions"><button type="button" className="btn btn-outline" onClick={()=>setModal(null)}>Cancel</button><button className="btn btn-primary" disabled={busy} aria-busy={busy}>Save student</button></div></form>}{error&&<p className="error" role="alert">{error}</p>}</div></div>}
  </div>;
}
