'use client';

import { useEffect, useState } from 'react';
import { Archive, ArrowRightLeft, Plus, RotateCcw, Search, Upload, X } from 'lucide-react';
import { post, request } from '@/lib/api';
import StudentImportDialog from './student-import-dialog';
import Pagination, { PAGE_SIZE } from './pagination';
import StudentEntryDialog, { DirectoryPage, DirectoryStudent, EnrollmentStatus, SectionOption, studentName } from './student-entry-dialog';

export default function StudentsView({isAdmin}:{isAdmin:boolean}) {
  const [data,setData] = useState<DirectoryPage|null>(null);
  const [sections,setSections] = useState<SectionOption[]>([]);
  const [grades,setGrades] = useState<{id:string;name:string}[]>([]);
  const [search,setSearch] = useState('');
  const [gradeId,setGradeId] = useState('');
  const [status,setStatus] = useState('active');
  const [page,setPage] = useState(1);
  const [revision,setRevision] = useState(0);
  const [loading,setLoading] = useState(true);
  const [busy,setBusy] = useState(false);
  const [error,setError] = useState('');
  const [message,setMessage] = useState('');
  const [adding,setAdding] = useState(false);
  const [importing,setImporting] = useState(false);
  const [enrollment,setEnrollment] = useState<DirectoryStudent|null>(null);
  const [targetSection,setTargetSection] = useState('');
  const [targetGrade,setTargetGrade] = useState('');

  useEffect(()=>{
    let active=true;
    Promise.all([request<SectionOption[]>('/sections'),request<{id:string;name:string}[]>('/grades')])
      .then(([s,g])=>{if(active){setSections(s);setGrades(g)}}).catch(e=>{if(active)setError(e.message)});
    return()=>{active=false};
  },[]);
  useEffect(()=>{
    let active=true;setLoading(true);setError('');
    const timer=setTimeout(()=>{
      const params=new URLSearchParams({search,status,page:String(page)});if(gradeId)params.set('gradeId',gradeId);
      request<DirectoryPage>(`/student-directory?${params}`).then(value=>{if(active)setData(value)})
        .catch(e=>{if(active)setError(e.message)}).finally(()=>{if(active)setLoading(false)});
    },200);
    return()=>{active=false;clearTimeout(timer)};
  },[search,gradeId,status,page,revision]);

  function openEnrollment(student:DirectoryStudent){setEnrollment(student);setTargetGrade(student.gradeId||'');setTargetSection(student.sectionId||'');setError('');}
  async function saveEnrollment(event:React.FormEvent){
    event.preventDefault();if(!enrollment)return;setBusy(true);setError('');
    try{await post(`/students/${enrollment.id}/enrollment`,{sectionId:targetSection,expectedSectionId:enrollment.sectionId,confirmTransfer:isAdmin&&!!enrollment.sectionId});setEnrollment(null);setRevision(r=>r+1);setMessage('Enrollment updated. The existing student ID and attendance history are retained.');}
    catch(e){setError(e instanceof Error?e.message:'Unable to update enrollment.');}finally{setBusy(false);}
  }
  async function archive(student:DirectoryStudent){
    if(!student.isDeleted&&!window.confirm(`Archive ${studentName(student)}? Their attendance history will be retained.`))return;
    setBusy(true);setError('');
    try{await request(`/students/${student.id}${student.isDeleted?'/restore':''}`,{method:student.isDeleted?'POST':'DELETE'});setRevision(r=>r+1);setMessage(student.isDeleted?'Student restored.':'Student archived.');}
    catch(e){setError(e instanceof Error?e.message:'Unable to update student.');}finally{setBusy(false);}
  }
  const enrollmentGrades=[...new Map(sections.map(s=>[s.gradeLevelId,{id:s.gradeLevelId,name:s.grade}])).values()];
  const destination=sections.find(s=>s.id===targetSection);

  return <>
    <div className="student-list-toolbar">
      <div className="search-box"><Search size={18}/><input type="search" className="field" aria-label="Search students by name or school ID number" placeholder="Search name or school ID number" value={search} onChange={e=>{setSearch(e.target.value);setPage(1)}}/></div>
      <select className="field" aria-label="Filter students by grade" value={gradeId} onChange={e=>{setGradeId(e.target.value);setPage(1)}}><option value="">All grades</option>{grades.map(g=><option key={g.id} value={g.id}>{g.name}</option>)}</select>
      <select className="field" aria-label="Filter students by enrollment status" value={status} onChange={e=>{setStatus(e.target.value);setPage(1)}}><option value="active">Active records</option><option value="enrolled">Enrolled</option><option value="unassigned">Unassigned</option><option value="archived">Archived</option><option value="all">All records</option></select>
      <div className="student-list-actions"><button className="btn btn-outline" onClick={()=>setImporting(true)}><Upload size={17}/>Batch import</button>
      <button className="btn btn-primary" onClick={()=>{setAdding(true);setError('');setMessage('')}}><Plus size={17}/>Add student</button></div>
    </div>
    <p className="student-list-hint muted">One record per student. Check their current enrollment before adding them to a class.</p>
    {error&&!enrollment&&<p className="error" role="alert">{error}</p>}{message&&<p className="success" role="status">{message}</p>}
    <div className="card table-wrap">{loading?<div className="skeleton-table" aria-label="Loading Student List">{[1,2,3,4].map(i=><div className="skeleton-line" key={i}/>)}</div>:data?.items.length?<table className="table student-directory-table"><thead><tr><th scope="col">#</th><th scope="col">School ID No.</th><th scope="col">Student</th><th scope="col">Grade level</th><th scope="col">Section</th><th scope="col">Status</th><th scope="col">Actions</th></tr></thead><tbody>{data.items.map((student,index)=><tr key={student.id}>
      <td>{(data.page-1)*PAGE_SIZE+index+1}</td><td className="student-number">{student.studentNumber}</td><td><strong>{studentName(student)}</strong></td><td>{student.grade||'Not assigned'}</td><td>{student.section||'—'}</td><td><EnrollmentStatus student={student}/></td><td><div className="row-actions">
        {!student.isDeleted&&(isAdmin||!student.sectionId)&&<button className="btn btn-outline student-enrollment-button" disabled={busy} onClick={()=>openEnrollment(student)} aria-label={`Change enrollment for ${studentName(student)}`}><ArrowRightLeft size={14}/>{student.sectionId?'Change enrollment':'Enroll'}</button>}
        {isAdmin&&<button className="icon-button" disabled={busy} title={student.isDeleted?'Restore student':'Archive student'} aria-label={`${student.isDeleted?'Restore':'Archive'} ${studentName(student)}`} onClick={()=>archive(student)}>{student.isDeleted?<RotateCcw size={16}/>:<Archive size={16}/>}</button>}
        {!isAdmin&&student.sectionId&&!student.isDeleted&&<span className="muted">Enrolled</span>}
      </div></td>
    </tr>)}</tbody></table>:<div className="empty">No students match these filters.</div>}</div>
    {!loading&&data&&<Pagination page={data.page} total={data.total} onPageChange={setPage}/>}
    {importing&&<StudentImportDialog onClose={()=>setImporting(false)} onImported={message=>{setPage(1);setRevision(r=>r+1);setMessage(message)}}/>}
    {adding&&<StudentEntryDialog onClose={()=>setAdding(false)} onSaved={()=>{setAdding(false);setPage(1);setRevision(r=>r+1);setMessage('Student saved to the school directory.');}}/>}
    {enrollment&&<div className="modal-backdrop" onMouseDown={()=>!busy&&setEnrollment(null)}><div className="modal" role="dialog" aria-modal="true" aria-labelledby="enrollment-title" onMouseDown={e=>e.stopPropagation()} onKeyDown={e=>{if(e.key==='Escape'&&!busy)setEnrollment(null)}}>
      <div className="modal-head"><h2 className="section-title" id="enrollment-title">{enrollment.sectionId?'Change enrollment':'Enroll student'}</h2><button type="button" className="icon-button" disabled={busy} aria-label="Close enrollment dialog" onClick={()=>setEnrollment(null)}><X size={19}/></button></div>
      <p><strong>{studentName(enrollment)}</strong><br/><span className="muted">{enrollment.studentNumber}</span></p>
      <p className="student-search-help muted">Current enrollment: {enrollment.grade?`${enrollment.grade} / ${enrollment.section}`:'Unassigned'}</p>
      <form onSubmit={saveEnrollment}><div className="form-grid"><div><label htmlFor="enroll-grade" className="label">Grade level</label><select autoFocus id="enroll-grade" className="field" disabled={busy} required value={targetGrade} onChange={e=>{setTargetGrade(e.target.value);setTargetSection('')}}><option value="">Choose grade</option>{enrollmentGrades.map(g=><option key={g.id} value={g.id}>{g.name}</option>)}</select></div><div><label htmlFor="enroll-section" className="label">Section</label><select id="enroll-section" className="field" disabled={busy||!targetGrade} required value={targetSection} onChange={e=>setTargetSection(e.target.value)}><option value="">Choose section</option>{sections.filter(s=>s.gradeLevelId===targetGrade).map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></div></div>
        {enrollment.sectionId&&destination&&targetSection!==enrollment.sectionId&&<p className="student-match-notice">This moves the existing record from {enrollment.grade} / {enrollment.section} to <strong>{destination.grade} / {destination.name}</strong>. The student will belong to only the new section.</p>}
        {error&&<p role="alert" className="error">{error}</p>}<div className="form-actions"><button type="button" className="btn btn-outline" disabled={busy} onClick={()=>setEnrollment(null)}>Cancel</button><button className="btn btn-primary" disabled={busy||!targetSection||targetSection===enrollment.sectionId} aria-busy={busy}>{busy?'Saving…':enrollment.sectionId?'Move student':'Enroll student'}</button></div>
      </form>
    </div></div>}
  </>;
}
