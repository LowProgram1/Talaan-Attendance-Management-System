'use client';

import { useEffect, useState } from 'react';
import { Search, X } from 'lucide-react';
import { post, request } from '@/lib/api';
import Pagination from './pagination';

export type DirectoryStudent = {
  id: string; studentNumber: string; firstName: string; middleName?: string; lastName: string; suffix?: string;
  sectionId: string | null; gradeId: string | null; grade: string | null; section: string | null;
  isDeleted: boolean; status: string;
};
export type DirectoryPage = { items: DirectoryStudent[]; total: number; page: number };
export type SectionOption = { id: string; name: string; gradeLevelId: string; grade: string };
export const studentName = (student: DirectoryStudent) => [student.firstName, student.middleName, student.lastName, student.suffix].filter(Boolean).join(' ');
export function EnrollmentStatus({student}:{student:DirectoryStudent}) {
  return <span className={`enrollment-status ${student.status === 'Enrolled' ? 'enrolled' : student.status === 'Unassigned' ? 'unassigned' : 'archived'}`}>{student.status}</span>;
}

export default function StudentEntryDialog({sectionId, onClose, onSaved}:{sectionId?:string; onClose:()=>void; onSaved:()=>void}) {
  const [mode,setMode] = useState<'existing'|'new'>(sectionId ? 'existing' : 'new');
  const [search,setSearch] = useState('');
  const [page,setPage] = useState(1);
  const [results,setResults] = useState<DirectoryPage|null>(null);
  const [loading,setLoading] = useState(false);
  const [sections,setSections] = useState<SectionOption[]>([]);
  const [gradeId,setGradeId] = useState('');
  const [busy,setBusy] = useState(false);
  const [error,setError] = useState('');
  const [form,setForm] = useState({firstName:'',middleName:'',lastName:'',suffix:'',guardianName:'',guardianEmail:'',sectionId:sectionId||''});
  const term = mode === 'existing' ? search : `${form.firstName} ${form.lastName}`.trim();

  useEffect(()=>{
    if(sectionId) return;
    let active = true;
    request<SectionOption[]>('/sections').then(data=>{if(active)setSections(data)}).catch(e=>{if(active)setError(e.message)});
    return ()=>{active=false};
  },[sectionId]);
  useEffect(()=>{
    let active = true;
    setResults(null);
    if(mode==='new' && term.length<2){setLoading(false);return;}
    setLoading(true);
    const timer = setTimeout(()=>{
      request<DirectoryPage>(`/student-directory?status=${sectionId?'unassigned':'all'}&search=${encodeURIComponent(term)}&page=${mode==='new'?1:page}`)
        .then(data=>{if(active)setResults(data)})
        .catch(e=>{if(active)setError(e.message)})
        .finally(()=>{if(active)setLoading(false)});
    },250);
    return ()=>{active=false;clearTimeout(timer)};
  },[term,page,mode,sectionId]);

  async function enroll(student:DirectoryStudent) {
    setBusy(true);setError('');
    try{await post(`/students/${student.id}/enrollment`,{sectionId,expectedSectionId:student.sectionId});onSaved();}
    catch(e){setError(e instanceof Error?e.message:'Unable to enroll student.');}
    finally{setBusy(false);}
  }
  async function create(event:React.FormEvent) {
    event.preventDefault();setBusy(true);setError('');
    try{await post('/students',{...form,sectionId:form.sectionId||null});onSaved();}
    catch(e){setError(e instanceof Error?e.message:'Unable to add student.');}
    finally{setBusy(false);}
  }
  const grades = [...new Map(sections.map(s=>[s.gradeLevelId,{id:s.gradeLevelId,name:s.grade}])).values()];
  const fields = [
    {key:'firstName',label:'First name',required:true},{key:'middleName',label:'Middle name'},
    {key:'lastName',label:'Last name',required:true},{key:'suffix',label:'Suffix'},
    {key:'guardianName',label:'Guardian name',required:true},{key:'guardianEmail',label:'Guardian email',required:true,type:'email'}
  ] as const;

  return <div className="modal-backdrop" onMouseDown={()=>!busy&&onClose()}>
    <div className={`modal student-entry-modal ${mode==='existing'?'student-search-modal':''}`} role="dialog" aria-modal="true" aria-labelledby="student-entry-title" onMouseDown={e=>e.stopPropagation()} onKeyDown={e=>{if(e.key==='Escape'&&!busy)onClose()}}>
      <div className="modal-head"><div><h2 className="section-title" id="student-entry-title">Add student</h2><p className="muted student-dialog-intro">{sectionId?'Find an existing student or create a new record.':'Create one student record, then assign a grade and section when ready.'}</p></div><button className="icon-button" type="button" aria-label="Close student dialog" disabled={busy} onClick={onClose}><X size={19}/></button></div>
      {sectionId&&<div className="tab-row" role="tablist" aria-label="Add student method">
        <button type="button" role="tab" aria-selected={mode==='existing'} disabled={busy} className={`tab ${mode==='existing'?'active':''}`} onClick={()=>{setMode('existing');setError('')}}>From Student List</button>
        <button type="button" role="tab" aria-selected={mode==='new'} disabled={busy} className={`tab ${mode==='new'?'active':''}`} onClick={()=>{setMode('new');setError('')}}>Create new student</button>
      </div>}
      {error&&<p className="error" role="alert">{error}</p>}
      {mode==='existing'?<>
        <div className="search-box student-directory-search"><Search size={17}/><input autoFocus type="search" className="field" aria-label="Search Student List" placeholder="Search name or school ID number" value={search} onChange={e=>{setSearch(e.target.value);setPage(1)}}/></div>
        <p className="student-search-help muted">Showing active students with no assigned grade or section. Search and select a student to add to this class.</p>
        {loading?<div className="skeleton-table" aria-label="Searching students"><div className="skeleton-line"/><div className="skeleton-line"/></div>:results?.items.length?<div className="student-search-results">{results.items.map(student=><div className="student-search-result" key={student.id}>
          <div><strong>{studentName(student)}</strong><p className="muted">{student.studentNumber} · {student.grade?`${student.grade} / ${student.section}`:'No section assigned'}</p><EnrollmentStatus student={student}/></div>
          <button type="button" className="btn btn-outline" disabled={busy||student.isDeleted||!!student.sectionId} onClick={()=>enroll(student)}>{student.isDeleted?'Archived':student.sectionId===sectionId?'Already in class':student.sectionId?'Enrolled elsewhere':'Add to class'}</button>
        </div>)}</div>:<div className="empty">No matching unassigned students. Use “Create new student” to register one.</div>}
        {results&&<Pagination page={results.page} total={results.total} onPageChange={setPage}/>}
      </>:<form onSubmit={create}>
        <fieldset disabled={busy} className="student-form-fields"><div className="form-grid">{fields.map(field=><div key={field.key}>
          <label className="label" htmlFor={`student-${field.key}`}>{field.label}{!('required' in field)&&<span className="field-optional"> (optional)</span>}</label>
          <input id={`student-${field.key}`} className="field" type={'type' in field?field.type:'text'} autoFocus={field.key==='firstName'} maxLength={field.key==='guardianEmail'?254:100} required={'required' in field&&field.required} value={form[field.key]} onChange={e=>setForm({...form,[field.key]:e.target.value})}/>
        </div>)}</div>
        {results&&results.items.length>0&&<div className="student-match-notice"><strong>Check existing students before saving</strong>{results.items.slice(0,3).map(s=><p key={s.id}>{studentName(s)} · {s.studentNumber} · {s.grade?`${s.grade} / ${s.section}`:s.status}</p>)}{sectionId&&<button type="button" className="student-text-button" onClick={()=>{setSearch(term);setPage(1);setMode('existing')}}>Find these students in Student List</button>}</div>}
        {!sectionId&&<div className="form-grid student-enrollment-fields"><div><label className="label" htmlFor="student-grade">Grade level</label><select className="field" id="student-grade" value={gradeId} onChange={e=>{setGradeId(e.target.value);setForm({...form,sectionId:''})}}><option value="">Assign later</option>{grades.map(g=><option key={g.id} value={g.id}>{g.name}</option>)}</select></div>
          <div><label className="label" htmlFor="student-section">Section</label><select className="field" id="student-section" disabled={!gradeId} required={!!gradeId} value={form.sectionId} onChange={e=>setForm({...form,sectionId:e.target.value})}><option value="">{gradeId?'Select section':'No section assigned'}</option>{sections.filter(s=>s.gradeLevelId===gradeId).map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></div></div>}
        </fieldset>
        <div className="form-actions"><button className="btn btn-outline" type="button" disabled={busy} onClick={onClose}>Cancel</button><button className="btn btn-primary" disabled={busy} aria-busy={busy}>{busy?'Saving…':sectionId?'Create and add to class':'Save student'}</button></div>
      </form>}
    </div>
  </div>;
}
