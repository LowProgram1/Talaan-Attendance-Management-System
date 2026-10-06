'use client';

import { use, useEffect, useState } from 'react';

import { useRouter } from 'next/navigation';

import Link from 'next/link';

import Image from 'next/image';

import Settings from './settings';

import ClassRecords from './class-records';
import DashboardView from './dashboard-view';

import AcademicsView from './academics-view';

import ReportsView from './reports-view';
import UsersView from './users-view';
import Pagination, { PAGE_SIZE } from './pagination';

import { LayoutDashboard,Users,ClipboardCheck,ChartNoAxesColumn,Bell,School,UserRoundCog,Menu,LogOut,ChevronRight,Settings2 } from 'lucide-react';

import { request,post,Session } from '@/lib/api';



type View='dashboard'|'students'|'attendance'|'reports'|'alerts'|'academics'|'users'|'settings';

type Dict=Record<string,unknown>;

const icons={dashboard:LayoutDashboard,students:Users,attendance:ClipboardCheck,reports:ChartNoAxesColumn,alerts:Bell,academics:School,users:UserRoundCog,settings:Settings2};

const titles:Record<View,string>={dashboard:'Dashboard',students:'Students',attendance:'Attendance',reports:'Report',alerts:'Alerts',academics:'Academics',users:'User Management',settings:'Settings'};

const subtitles:Record<View,string>={dashboard:'Attendance at a glance.',students:'Manage student records and guardian connections.',attendance:'Browse grade levels, sections, and student rosters.',reports:'Class summaries ready for the registrar and parent meetings.',alerts:'Email notifications sent to guardians when a student is absent or late.',academics:'Set up the school structure that attendance is recorded against.',users:'Manage staff accounts and guardian email contacts.',settings:'Manage your account information and password.'};

const get=(item:Dict,key:string)=>String(item[key]??'');

const formatDate=(value:string)=>new Date(value).toLocaleDateString(undefined,{year:'numeric',month:'short',day:'numeric'});

function useData<T>(path:string|null,refresh=0){const[data,setData]=useState<T|null>(null),[error,setError]=useState(''),[loading,setLoading]=useState(Boolean(path));useEffect(()=>{if(!path)return;let active=true;request<T>(path).then(x=>{if(active){setData(x);setError('')}}).catch(e=>{if(active)setError(e.message)}).finally(()=>{if(active)setLoading(false)});return()=>{active=false}},[path,refresh]);return{data,error,loading}}

function Notice({error,loading}:{error:string,loading:boolean}){return <>{loading&&<div className="skeleton-table" aria-label="Loading records">{Array.from({length:5},(_,i)=><div className="skeleton-line" key={i}/>)}</div>}{error&&<p className="error" role="alert" style={{padding:16}}>{error}</p>}</>}

function Empty({label='No records yet.'}:{label?:string}){return <div className="empty">{label}</div>}

export default function Workspace({params}:{params:Promise<{view:string}>}){const {view:raw}=use(params);const view=(raw==='students'?'attendance':raw in titles?raw:'dashboard') as View;const router=useRouter();const[session,setSession]=useState<Session|null>(null),[collapsed,setCollapsed]=useState(false),[mobile,setMobile]=useState(false),[authLoading,setAuthLoading]=useState(true);useEffect(()=>{request<Session>('/auth/me').then(setSession).catch(()=>router.replace('/')).finally(()=>setAuthLoading(false))},[router]);if(authLoading)return <main className="skeleton-table" aria-label="Loading workspace">{Array.from({length:6},(_,i)=><div className="skeleton-line" key={i}/>)}</main>;if(!session)return null;const nav:View[]=session.roles.includes('Administrator')?['dashboard','academics','attendance','reports','alerts','users','settings']:['dashboard','attendance','reports','settings'];async function logout(){await post('/auth/logout',{});router.replace('/')}return <div className="app-shell"><div className={`mobile-overlay ${mobile?'show':''}`} onClick={()=>setMobile(false)}/><aside className={`sidebar ${collapsed?'collapsed':''} ${mobile?'mobile-open expanded':''}`}><div className="brand"><div className="brand-icon"><Image src="/talaan-mark.svg" alt="Talaan logo" width={36} height={36}/></div><div className="brand-copy"><strong>Talaan</strong><div className="muted brand-subtitle">Attendance Management<br/>Information System</div></div></div><nav className="nav-group" aria-label="Main navigation">{nav.map(x=>{const Icon=icons[x];return <Link key={x} title={titles[x]} className={`nav-link ${view===x?'active':''}`} href={`/app/${x}`} onClick={()=>setMobile(false)}><Icon size={20}/><span className="nav-text">{titles[x]}</span></Link>})}</nav><div className="profile"><div className="avatar">{session.fullName.slice(0,2).toUpperCase()}</div><div className="profile-copy" style={{minWidth:0}}><strong style={{display:'block',overflow:'hidden',textOverflow:'ellipsis',whiteSpace:'nowrap'}}>{session.fullName}</strong><span className="muted" style={{fontSize:12}}>{session.roles[0]}</span></div></div></aside><div className="main"><header className="topbar"><button className="icon-button" aria-label="Toggle sidebar" onClick={()=>window.innerWidth<=700?setMobile(!mobile):setCollapsed(!collapsed)}><Menu size={23}/></button><div style={{display:'flex',alignItems:'center',gap:12}}><span className="muted" style={{fontSize:14}}>{session.fullName}</span><button title="Sign out" aria-label="Sign out" className="icon-button" onClick={logout}><LogOut size={19}/></button></div></header><div className="content"><div className="page-header"><div><h1 className="heading">{titles[view]}</h1><p>{subtitles[view]}</p></div></div>{view==='dashboard'&&<Dashboard isAdmin={session.roles.includes('Administrator')}/>}{view==='attendance'&&<AttendanceWorkspace canManage={session.roles.includes('Administrator')}/>}{view==='reports'&&<Reports/>}{view==='alerts'&&<Alerts/>}{view==='academics'&&<Academics/>}{view==='users'&&<UsersView currentUserId={session.id}/>}{view==='settings'&&<Settings session={session} onSession={setSession}/>}</div></div></div>}

function Dashboard({isAdmin}:{isAdmin:boolean}){return <DashboardView isAdmin={isAdmin}/>}

function Academics(){return <AcademicsView/>}

function Reports(){return <ReportsView/>}

function Alerts(){const alerts=useData<Dict[]>('/alerts');return <div className="card"><div className="section-pad" style={{borderBottom:'1px solid var(--line)'}}><h2 className="section-title">Alert log</h2></div><Notice error={alerts.error} loading={alerts.loading}/>{alerts.data?.length?alerts.data.map(x=><div key={get(x,'id')} style={{padding:'20px 28px',borderBottom:'1px solid var(--line)',display:'flex',justifyContent:'space-between'}}><div><strong>{get(x,'recipient')}</strong><div className="muted">{formatDate(get(x,'createdAt'))}</div></div><div><span style={{color:'var(--orange)'}}>{get(x,'status')}</span><div className="muted">{x.delivered?'Delivered':'Pending'}</div></div></div>):!alerts.loading&&<Empty label="No alerts have been sent yet."/>}</div>}

function AttendanceWorkspace({canManage}:{canManage:boolean}){

 const grades=useData<Dict[]>('/grades'),sections=useData<Dict[]>('/sections'),schedules=useData<Dict[]>('/schedules');

 const[gradeId,setGradeId]=useState(''),[sectionId,setSectionId]=useState(''),[scheduleId,setScheduleId]=useState('');
 const[gradePage,setGradePage]=useState(1),[sectionPage,setSectionPage]=useState(1);

 const currentGrade=grades.data?.find(x=>get(x,'id')===gradeId);


 const gradeSections=(sections.data||[]).filter(x=>get(x,'gradeLevelId')===gradeId);

 const visibleGrades=(grades.data||[]).filter(grade=>canManage||(sections.data||[]).some(section=>get(section,'gradeLevelId')===get(grade,'id')));
 const rows:{section:Dict,schedule:Dict|null}[]=gradeSections.flatMap<{section:Dict,schedule:Dict|null}>(section=>{const matches=(schedules.data||[]).filter(schedule=>get(schedule,'sectionId')===get(section,'id'));return matches.length?matches.map(schedule=>({section,schedule})): [{section,schedule:null}];});

 const currentGradePage=Math.min(gradePage,Math.max(1,Math.ceil(visibleGrades.length/PAGE_SIZE)));
 const currentSectionPage=Math.min(sectionPage,Math.max(1,Math.ceil(rows.length/PAGE_SIZE)));
 return <><Notice error={grades.error||sections.error||schedules.error} loading={grades.loading||sections.loading||schedules.loading}/>

 <div className="tab-row attendance-tabs" role="tablist" aria-label="Attendance navigation">
   <button className={`tab ${!gradeId?'active':''}`} role="tab" aria-selected={!gradeId} onClick={()=>{setGradeId('');setSectionId('');setScheduleId('')}}>Grade Levels</button>
   <button className={`tab ${gradeId&&!sectionId?'active':''}`} role="tab" aria-selected={Boolean(gradeId&&!sectionId)} disabled={!gradeId} onClick={()=>{setSectionId('');setScheduleId('')}}>Sections</button>
   <button className={`tab ${sectionId?'active':''}`} role="tab" aria-selected={Boolean(sectionId)} disabled={!sectionId}>Class Records</button>
 </div>

 {!gradeId&&<div className="card table-wrap"><table className="table drill-table"><thead><tr><th>Grade level</th><th>Department</th><th>Sections</th><th>Students</th><th aria-label="Open"/></tr></thead><tbody>{visibleGrades.slice((currentGradePage-1)*PAGE_SIZE,currentGradePage*PAGE_SIZE).map(grade=><tr key={get(grade,'id')} onClick={()=>{setGradeId(get(grade,'id'));setSectionPage(1)}} tabIndex={0} onKeyDown={e=>{if(e.key==='Enter'){setGradeId(get(grade,'id'));setSectionPage(1)}}}><td><strong>{get(grade,'name')}</strong></td><td>{get(grade,'department')}</td><td>{(sections.data||[]).filter(x=>get(x,'gradeLevelId')===get(grade,'id')).length}</td><td>{get(grade,'students')}</td><td><ChevronRight size={17}/></td></tr>)}</tbody></table>{!grades.loading&&!grades.data?.length&&<Empty label="No grade levels yet. Add one in Academics."/>}</div>}
 {!gradeId&&<Pagination page={currentGradePage} total={visibleGrades.length} onPageChange={setGradePage}/>}

 {gradeId&&!sectionId&&<><h2 className="section-title drill-heading">{get(currentGrade||{},'name')} sections</h2><div className="card table-wrap"><table className="table drill-table"><thead><tr><th>Section</th><th>Teacher</th><th>Subject</th><th>Schedule</th><th>Students</th><th aria-label="Open"/></tr></thead><tbody>{rows.slice((currentSectionPage-1)*PAGE_SIZE,currentSectionPage*PAGE_SIZE).map(({section,schedule},index)=><tr key={get(section,'id')+'-'+index} onClick={()=>{setSectionId(get(section,'id'));setScheduleId(schedule?get(schedule,'id'):'')}} tabIndex={0} onKeyDown={e=>{if(e.key==='Enter'){setSectionId(get(section,'id'));setScheduleId(schedule?get(schedule,'id'):'')}}}><td><strong>{get(section,'name')}</strong><span className="muted"> · {get(section,'room')}</span></td><td>{schedule?get(schedule,'teacher'):get(section,'adviser')||'—'}</td><td>{schedule?get(schedule,'subject'):'—'}</td><td>{schedule?`${get(schedule,'startsAt')} – ${get(schedule,'endsAt')}`:'—'}</td><td>{get(section,'students')}</td><td><ChevronRight size={17}/></td></tr>)}</tbody></table>{!rows.length&&<Empty label="No sections in this grade level yet."/>}</div><Pagination page={currentSectionPage} total={rows.length} onPageChange={setSectionPage}/></>}

 {sectionId&&<ClassRecords key={sectionId+scheduleId} sectionId={sectionId} scheduleId={scheduleId||get((schedules.data||[]).find(x=>get(x,'sectionId')===sectionId)||{},'id')} canEdit={canManage}/>}

 </>;

}
