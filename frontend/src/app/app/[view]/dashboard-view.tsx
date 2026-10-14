'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Bell, BookOpen, ClipboardCheck, GraduationCap, House, School, UserRoundX, Users } from 'lucide-react';
import { request } from '@/lib/api';

type Dashboard = { students:number; sections?:number; attendanceToday?:number; absentToday?:number; alerts?:number; guardians?:number; teachers?:number; subjects?:number };
type Schedule = { id:string; teacherId:string; grade:string; section:string; subject:string; teacher:string; room:string; startsAt:string; endsAt:string; dayOfWeek:string|null };
type Point = { date:string; present:number; absent:number; records:number };
type Period = 'day'|'week'|'month';
type Trend = { period:Period; points:Point[] };

const chartWidth = 760;
const chartHeight = 250;
const plot = { left:45, right:18, top:18, bottom:42 };
const xAt = (index:number, count:number) => plot.left + index * (chartWidth - plot.left - plot.right) / Math.max(1,count-1);
const yAt = (value:number, max:number) => plot.top + (max-value) * (chartHeight-plot.top-plot.bottom) / max;
const periodLabel = (date:string, period:Period) => new Intl.DateTimeFormat('en-US',
  period==='day'?{weekday:'short',timeZone:'UTC'}:period==='week'?{month:'short',day:'numeric',timeZone:'UTC'}:{month:'short',timeZone:'UTC'}).format(new Date(`${date}T12:00:00Z`));

function AttendanceTrend() {
  const [period,setPeriod] = useState<Period>('day');
  const [trend,setTrend] = useState<Trend|null>(null);
  const [loading,setLoading] = useState(true);
  const [error,setError] = useState('');
  useEffect(()=>{
    let active=true;
    request<Trend>(`/dashboard/trend?period=${period}`)
      .then(data=>{if(active){setTrend(data);setError('')}})
      .catch(e=>{if(active)setError(e instanceof Error?e.message:'Unable to load attendance trend.')})
      .finally(()=>{if(active)setLoading(false)});
    return()=>{active=false};
  },[period]);
  const points = trend?.period===period ? trend.points : [];
  const hasRecords = points.some(point=>point.records>0);
  const maxValue = Math.max(1,...points.flatMap(point=>[point.present,point.absent]));
  const tickStep = Math.max(1,Math.ceil(maxValue/4));
  const axisMax = tickStep*4;
  const lineFor = (key:'present'|'absent') => points.map((point,index)=>`${index===0?'M':'L'}${xAt(index,points.length).toFixed(1)} ${yAt(point[key],axisMax).toFixed(1)}`).join(' ');
  return <section className="card dashboard-trend">
    <div className="dashboard-trend-header"><div><h2 className="section-title">Attendance trend</h2><p className="muted">Present and absent records over time</p></div>
      <label className="dashboard-period">View <select className="field" value={period} onChange={event=>{setPeriod(event.target.value as Period);setLoading(true);setError('')}} aria-label="Attendance trend period"><option value="day">Day</option><option value="week">Week</option><option value="month">Month</option></select></label>
    </div>
    <div className="dashboard-line-legend"><span><i className="present"/>Present</span><span><i className="absent"/>Absent</span></div>
    {error&&<p className="error" role="alert">{error}</p>}
    {loading?<div className="skeleton-table" aria-label="Loading attendance trend">{Array.from({length:4},(_,i)=><div className="skeleton-line" key={i}/>)}</div>:
      !hasRecords?<div className="empty">No attendance has been recorded in this period.</div>:
      <div className="dashboard-chart-wrap"><svg viewBox={`0 0 ${chartWidth} ${chartHeight}`} role="img" aria-label={`Present and absent attendance totals by ${period}`}>
        {Array.from({length:5},(_,index)=>{const value=axisMax-index*tickStep;return <g key={value}><line className="chart-grid" x1={plot.left} x2={chartWidth-plot.right} y1={yAt(value,axisMax)} y2={yAt(value,axisMax)}/><text className="chart-label" x={plot.left-9} y={yAt(value,axisMax)+4} textAnchor="end">{value}</text></g>;})}
        <path className="chart-line chart-line-present" d={lineFor('present')}/>
        <path className="chart-line chart-line-absent" d={lineFor('absent')}/>
        {points.map((point,index)=><g key={point.date}><circle className="chart-dot chart-dot-present" cx={xAt(index,points.length)} cy={yAt(point.present,axisMax)} r="4"><title>{point.date}: {point.present} present</title></circle><circle className="chart-dot chart-dot-absent" cx={xAt(index,points.length)} cy={yAt(point.absent,axisMax)} r="4"><title>{point.date}: {point.absent} absent</title></circle></g>)}
        {points.map((point,index)=><text key={point.date} className="chart-label" x={xAt(index,points.length)} y={chartHeight-12} textAnchor="middle">{periodLabel(point.date,period)}</text>)}
      </svg><p className="muted dashboard-chart-note">Day shows the last 7 days · Week shows the last 8 weeks · Month shows the last 12 months</p></div>}
  </section>;
}

function CommunityMix({summary,isAdmin}:{summary:Dashboard|null;isAdmin:boolean}) {
  const segments = [
    {label:'Students',value:summary?.students??0,color:'#2563eb'},
    {label:'Teachers',value:isAdmin?(summary?.teachers??0):1,color:'#14b8a6'},
    {label:'Guardians',value:summary?.guardians??0,color:'#f59e0b'}
  ];
  const total = segments.reduce((sum,item)=>sum+item.value,0);
  let position=0;
  const slices=segments.map(item=>{
    const start=total?position/total*100:0;
    position+=item.value;
    const end=total?position/total*100:0;
    return `${item.color} ${start}% ${end}%`;
  });
  const chartLabel=segments.map(item=>`${item.label}: ${item.value}`).join(', ');
  return <section className="card dashboard-community" aria-labelledby="community-title">
    <div className="dashboard-panel-heading"><div><h2 className="section-title" id="community-title">School community</h2><p className="muted">People connected to attendance</p></div></div>
    {summary===null?<div className="dashboard-community-loading" aria-label="Loading community statistics"><div className="skeleton-line"/><div className="skeleton-line"/></div>:
      <div className="dashboard-community-content">
        <div className={`dashboard-pie ${total===0?'is-empty':''}`} style={{background:total?`conic-gradient(${slices.join(', ')})`:undefined}} role="img" aria-label={`School community: ${chartLabel}`}>
          <div className="dashboard-pie-center"><strong>{total.toLocaleString()}</strong><span>People</span></div>
        </div>
        <ul className="dashboard-legend" aria-label="Community totals">
          {segments.map(item=><li key={item.label}><span className="dashboard-legend-dot" style={{background:item.color}}/><span>{item.label}</span><strong>{item.value.toLocaleString()}</strong></li>)}
        </ul>
      </div>}
  </section>;
}

function TeachersBySubject({schedules}:{schedules:Schedule[]|null}) {
  const teachersBySubject = new Map<string,Set<string>>();
  schedules?.forEach(schedule=>{
    const subject=schedule.subject.trim();
    if(!subject)return;
    const teachers=teachersBySubject.get(subject)??new Set<string>();
    teachers.add(schedule.teacherId);
    teachersBySubject.set(subject,teachers);
  });
  const palette=['#2563eb','#14b8a6','#f59e0b','#8b5cf6','#ef5a67','#0ea5e9','#84a83d','#e879a9'];
  const subjects=[...teachersBySubject.entries()].sort(([a],[b])=>a.localeCompare(b)).map(([label,teachers],index)=>({label,value:teachers.size,color:palette[index%palette.length]}));
  const total=subjects.reduce((sum,item)=>sum+item.value,0);
  let position=0;
  const slices=subjects.map(item=>{
    const start=total?position/total*100:0;
    position+=item.value;
    return `${item.color} ${start}% ${total?position/total*100:0}%`;
  });
  const chartLabel=subjects.map(item=>`${item.label}: ${item.value} ${item.value===1?'teacher':'teachers'}`).join(', ');
  return <section className="card dashboard-community dashboard-subject-card" aria-labelledby="subject-teachers-title">
    <div className="dashboard-panel-heading"><div><h2 className="section-title" id="subject-teachers-title">Teachers by subject</h2><p className="muted">Distinct teachers on active schedules</p></div></div>
    {schedules===null?<div className="dashboard-community-loading" aria-label="Loading subject assignments"><div className="skeleton-line"/><div className="skeleton-line"/></div>:
      !subjects.length?<div className="empty dashboard-subject-empty">No subjects have active schedules yet.</div>:
      <div className="dashboard-community-content dashboard-subject-content">
        <div className="dashboard-pie dashboard-subject-pie" style={{background:`conic-gradient(${slices.join(', ')})`}} role="img" aria-label={`Teachers by subject: ${chartLabel}`}>
          <div className="dashboard-pie-center"><strong>{total.toLocaleString()}</strong><span>Assignments</span></div>
        </div>
        <ul className="dashboard-legend dashboard-subject-legend" aria-label="Teacher assignments by subject">
          {subjects.map(item=><li key={item.label}><span className="dashboard-legend-dot" style={{background:item.color}}/><span title={item.label}>{item.label}</span><strong>{item.value}</strong></li>)}
        </ul>
      </div>}
  </section>;
}

export default function DashboardView({isAdmin}:{isAdmin:boolean}) {
  const [summary,setSummary] = useState<Dashboard|null>(null);
  const [schedules,setSchedules] = useState<Schedule[]|null>(null);
  const [error,setError] = useState('');
  useEffect(()=>{
    let active=true;
    Promise.all([request<Dashboard>('/dashboard'),request<Schedule[]>('/schedules')])
      .then(([data,classes])=>{if(active){setSummary(data);setSchedules(classes)}})
      .catch(e=>{if(active)setError(e instanceof Error?e.message:'Unable to load dashboard.')});
    return()=>{active=false};
  },[]);
  const today = new Intl.DateTimeFormat('en-US',{weekday:'long',timeZone:'Asia/Manila'}).format(new Date());
  const todaysClasses = schedules?.filter(schedule=>!schedule.dayOfWeek||schedule.dayOfWeek===today)??null;
  const adminCards = [
    {label:'Students Enrolled',value:summary?.students,Icon:Users},
    {label:'Present Today',value:summary?.attendanceToday,Icon:ClipboardCheck},
    {label:'Absent Today',value:summary?.absentToday,Icon:UserRoundX},
    {label:'Sections',value:summary?.sections,Icon:School},
    {label:'Guardians',value:summary?.guardians,Icon:House},
    {label:'Teachers',value:summary?.teachers,Icon:GraduationCap},
    {label:'Alerts Sent',value:summary?.alerts,Icon:Bell},
    {label:'Total Subjects',value:summary?.subjects,Icon:BookOpen}
  ];
  const cards = isAdmin ? adminCards : [
    {label:'My Students',value:summary?.students,Icon:Users},
    {label:'My Students’ Guardians',value:summary?.guardians,Icon:House},
    {label:'My Subjects',value:summary?.subjects,Icon:BookOpen}
  ];
  return <>
    {error&&<p className="error" role="alert">{error}</p>}
    <div className="stats dashboard-stats">{cards.map(({label,value,Icon})=><div className="card stat" key={label}><div className="stat-icon"><Icon size={21}/></div><div className="stat-value">{value??<span className="skeleton-line dashboard-stat-loading" aria-label="Loading"/>}</div><div className="stat-label">{label}</div></div>)}</div>
    <div className="grid-two dashboard-lower"><div className="card section-pad"><div className="dashboard-classes-head"><h2 className="section-title">Today&apos;s Classes</h2><Link href="/app/attendance">Take attendance →</Link></div>
      {todaysClasses===null?<div className="skeleton-table">{Array.from({length:4},(_,i)=><div className="skeleton-line" key={i}/>)}</div>:todaysClasses.length?todaysClasses.map(item=><div className="dashboard-class-row" key={item.id}><div><strong>{item.grade} – {item.section} · {item.subject}</strong><div className="muted">{item.teacher} · {item.room}</div></div><span>{item.startsAt} – {item.endsAt}</span></div>):<div className="empty">No classes scheduled today.</div>}
    </div><div className="card section-pad"><h2 className="section-title">Quick actions</h2><p className="muted dashboard-actions-copy">{isAdmin?'Set up your school and begin recording attendance.':'Open your assigned classes and record attendance.'}</p><div className="class-actions">{isAdmin&&<Link className="btn btn-primary" href="/app/academics">Academics</Link>}<Link className="btn btn-outline" href="/app/attendance">Class records</Link></div></div></div>
    <div className="dashboard-insights"><CommunityMix summary={summary} isAdmin={isAdmin}/><TeachersBySubject schedules={schedules}/><AttendanceTrend/></div>
  </>;
}
