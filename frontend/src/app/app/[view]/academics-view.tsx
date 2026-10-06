'use client';
import { useEffect, useState } from 'react';
import { Archive, Pencil, Plus, RotateCcw } from 'lucide-react';
import { post, put, request } from '@/lib/api';
type Row = Record<string,string|number|boolean|null>;
type Kind = 'grades'|'sections'|'schedules';
const kinds:Kind[]=['grades','sections','schedules'];
const value=(row:Row,key:string)=>String(row[key]??'');
const blank=()=>({name:'',department:'',gradeLevelId:'',room:'',adviserId:'',sectionId:'',teacherId:'',subject:'',startsAt:'',endsAt:''});

export default function AcademicsView(){
  const [tab,setTab]=useState<Kind>('grades');
  const [rows,setRows]=useState<Record<Kind,Row[]>>({grades:[],sections:[],schedules:[]});
  const [users,setUsers]=useState<Row[]>([]);
  const [archived,setArchived]=useState(false);
  const [page,setPage]=useState(1);
  const [loading,setLoading]=useState(true);
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [message,setMessage]=useState('');
  const [modal,setModal]=useState<'add'|'edit'|null>(null);
  const [selected,setSelected]=useState<Row|null>(null);
  const [form,setForm]=useState(blank);
  async function reload(){setLoading(true);setError('');try{const [grades,sections,schedules,people]=await Promise.all([request<Row[]>('/grades?includeArchived=true'),request<Row[]>('/sections?includeArchived=true'),request<Row[]>('/schedules?includeArchived=true'),request<Row[]>('/users')]);setRows({grades,sections,schedules});setUsers(people)}catch(e){setError(e instanceof Error?e.message:'Unable to load academics.')}finally{setLoading(false)}}
  useEffect(()=>{reload()},[]);
  const teachers=users.filter(u=>value(u,'role')==='Teacher');
  const activeGrades=rows.grades.filter(x=>!x.isArchived);
  const activeSections=rows.sections.filter(x=>!x.isArchived && activeGrades.some(g=>g.id===x.gradeLevelId));
  const visible=rows[tab].filter(x=>Boolean(x.isArchived)===archived);
  const current=visible.slice((page-1)*20,page*20);
  const columns=tab==='grades'?['Grade level','Department','Sections','Students']:tab==='sections'?['Section','Grade','Room','Adviser','Students']:['Subject','Section','Teacher','Time'];
  function openAdd(){setSelected(null);setForm(blank());setModal('add')}
  function openEdit(row:Row){setSelected(row);setForm({...blank(),name:value(row,'name'),department:value(row,'department'),gradeLevelId:value(row,'gradeLevelId'),room:value(row,'room'),adviserId:value(row,'adviserId'),sectionId:value(row,'sectionId'),teacherId:value(row,'teacherId'),subject:value(row,'subject'),startsAt:value(row,'startsAt').slice(0,5),endsAt:value(row,'endsAt').slice(0,5)});setModal('edit')}
  async function save(){setBusy(true);setError('');try{const payload=tab==='grades'?{name:form.name,department:form.department}:tab==='sections'?{name:form.name,gradeLevelId:form.gradeLevelId,room:form.room,adviserId:form.adviserId||null}:{sectionId:form.sectionId,teacherId:form.teacherId,subject:form.subject,startsAt:form.startsAt,endsAt:form.endsAt};if(modal==='edit'&&selected)await put(`/${tab}/${selected.id}`,payload);else await post(`/${tab}`,payload);setModal(null);setMessage('Academic record saved.');await reload()}catch(e){setError(e instanceof Error?e.message:'Unable to save academic record.')}finally{setBusy(false)}}
  async function toggle(row:Row){const restore=Boolean(row.isArchived);if(!restore&&!confirm('Remove this record? Used records will be archived and remain in reports.'))return;setBusy(true);setError('');try{await request(`/${tab}/${row.id}${restore?'/restore':''}`,{method:restore?'POST':'DELETE'});setMessage(restore?'Record restored.':'Record removed or archived.');await reload()}catch(e){setError(e instanceof Error?e.message:'Unable to update record.')}finally{setBusy(false)}}
  const field=(key:keyof ReturnType<typeof blank>,label:string,required=true,type='text')=><div key={key}><label className="label" htmlFor={key}>{label}</label><input className="field" id={key} type={type} value={form[key]} required={required} onChange={e=>setForm({...form,[key]:e.target.value})}/></div>;
  const select=(key:keyof ReturnType<typeof blank>,label:string,options:Row[],display:(row:Row)=>string,required=true)=><div key={key}><label className="label" htmlFor={key}>{label}</label><select className="field" id={key} required={required} value={form[key]} onChange={e=>setForm({...form,[key]:e.target.value})}><option value="">Select {label.toLowerCase()}</option>{options.map(x=><option key={value(x,'id')} value={value(x,'id')}>{display(x)}</option>)}</select></div>;
  return <><div className="section-toolbar"><div className="tab-row" role="tablist" aria-label="Academic records">{kinds.map(kind=><button key={kind} className={`tab ${tab===kind?'active':''}`} role="tab" aria-selected={tab===kind} onClick={()=>{setTab(kind);setPage(1)}}>{kind[0].toUpperCase()+kind.slice(1)}</button>)}</div><div className="class-actions"><button className="btn btn-outline" onClick={()=>{setArchived(!archived);setPage(1)}}>{archived?'Active records':'Archived records'}</button><button className="btn btn-primary" onClick={openAdd}><Plus size={17}/> Add {tab.slice(0,-1)}</button></div></div>
    {error&&<p className="error" role="alert">{error}</p>}{message&&<p className="success" role="status">{message}</p>}
    <div className="card table-wrap">{loading?<div className="skeleton-table">{Array.from({length:6},(_,i)=><div key={i} className="skeleton-line"/>)}</div>:current.length?<table className="table"><thead><tr>{columns.map(x=><th key={x}>{x}</th>)}<th>Actions</th></tr></thead><tbody>{current.map(row=><tr key={value(row,'id')}>{(tab==='grades'?[value(row,'name'),value(row,'department'),value(row,'sections'),value(row,'students')]:tab==='sections'?[value(row,'name'),value(row,'grade'),value(row,'room'),value(row,'adviser'),value(row,'students')]:[value(row,'subject'),`${value(row,'grade')} – ${value(row,'section')}`,value(row,'teacher'),`${value(row,'startsAt').slice(0,5)} – ${value(row,'endsAt').slice(0,5)}`]).map((cell,i)=><td key={i}>{cell||'—'}</td>)}<td><div className="row-actions"><button className="icon-button" aria-label="Edit record" title="Edit" onClick={()=>openEdit(row)}><Pencil size={16}/></button><button className="icon-button" aria-label={archived?'Restore record':'Remove record'} title={archived?'Restore':'Remove'} disabled={busy} onClick={()=>toggle(row)}>{archived?<RotateCcw size={16}/>:<Archive size={16}/>}</button></div></td></tr>)}</tbody></table>:<div className="empty">No {archived?'archived ':''}{tab} found.</div>}</div>
    {visible.length>20&&<div className="pagination"><button className="btn btn-outline" disabled={page===1} onClick={()=>setPage(page-1)}>Previous</button><span>Page {page} of {Math.ceil(visible.length/20)}</span><button className="btn btn-outline" disabled={page>=Math.ceil(visible.length/20)} onClick={()=>setPage(page+1)}>Next</button></div>}
    {modal&&<div className="modal-backdrop" onMouseDown={()=>setModal(null)}><div className="modal" role="dialog" aria-modal="true" aria-label="Academic form" onMouseDown={e=>e.stopPropagation()}><div className="modal-head"><h2 className="section-title">{modal==='edit'?'Edit':'Add'} {tab.slice(0,-1)}</h2><button className="icon-button" onClick={()=>setModal(null)} aria-label="Close">×</button></div><form onSubmit={e=>{e.preventDefault();save()}}><div className="form-grid">{tab==='grades'?<>{field('name','Grade level')}{field('department','Department')}</>:tab==='sections'?<>{field('name','Section name')}{select('gradeLevelId','Grade level',activeGrades,x=>value(x,'name'))}{field('room','Room',false)}{select('adviserId','Adviser',teachers,x=>value(x,'fullName'),false)}</>:<>{select('sectionId','Section',activeSections,x=>`${value(x,'grade')} – ${value(x,'name')}`)}{select('teacherId','Teacher',teachers,x=>value(x,'fullName'))}{field('subject','Subject')}{field('startsAt','Starts at',true,'time')}{field('endsAt','Ends at',true,'time')}</>}</div><div className="form-actions"><button type="button" className="btn btn-outline" onClick={()=>setModal(null)}>Cancel</button><button className="btn btn-primary" disabled={busy} aria-busy={busy}>Save record</button></div>{busy&&<div className="skeleton-table auth-progress" role="status" aria-label="Saving academic record"><div className="skeleton-line"/><div className="skeleton-line"/></div>}{error&&<p className="error" role="alert">{error}</p>}</form></div></div>}</>;
}
