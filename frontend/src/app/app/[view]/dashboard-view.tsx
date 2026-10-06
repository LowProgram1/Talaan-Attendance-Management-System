'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Bell, BookOpen, ClipboardCheck, GraduationCap, House, School, Users } from 'lucide-react';
import { request } from '@/lib/api';

type Dashboard = { students:number; sections?:number; attendanceToday?:number; alerts?:number; guardians?:number; teachers?:number; subjects?:number };
type Schedule = { id:string; grade:string; section:string; subject:string; teacher:string; room:string; startsAt:string; endsAt:string };
type Point = { date:string; rate:number|null; records:number };
type Trend = { period:'week'|'month'; points:Point[] };

const chartWidth = 760;
const chartHeight = 250;
const plot = { left:45, right:18, top:18, bottom:42 };
const xAt = (index:number, count:number) => plot.left + index * (chartWidth - plot.left - plot.right) / Math.max(1,count-1);
const yAt = (rate:number) => plot.top + (100-rate) * (chartHeight-plot.top-plot.bottom) / 100;
const dayLabel = (date:string, period:'week'|'month') => new Intl.DateTimeFormat('en-US',
  period==='week'?{weekday:'short',timeZone:'UTC'}:{month:'short',day:'numeric',timeZone:'UTC'}).format(new Date(`${date}T12:00:00Z`));

function AttendanceTrend() {
  const [period,setPeriod] = useState<'week'|'month'>('week');
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
  const recorded = points.filter(point=>point.rate!==null);
  const line = points.reduce<{path:string;connected:boolean}>((current,point,index)=>{
    if(point.rate===null)return {path:current.path,connected:false};
    const command=current.connected?'L':'M';
    return {path:`${current.path} ${command}${xAt(index,points.length).toFixed(1)} ${yAt(point.rate).toFixed(1)}`,connected:true};
  },{path:'',connected:false}).path;
  return <section className="card dashboard-trend">
    <div className="dashboard-trend-header"><div><h2 className="section-title">Attendance trend</h2><p className="muted">Daily rate from recorded class attendance</p></div>
      <label className="dashboard-period">View <select className="field" value={period} onChange={event=>{setPeriod(event.target.value as 'week'|'month');setLoading(true)}} aria-label="Attendance trend period"><option value="week">Week</option><option value="month">Month</option></select></label>
    </div>
    {error&&<p className="error" role="alert">{error}</p>}
    {loading?<div className="skeleton-table" aria-label="Loading attendance trend">{Array.from({length:4},(_,i)=><div className="skeleton-line" key={i}/>)}</div>:
      !recorded.length?<div className="empty">No attendance has been recorded in this period.</div>:
      <div className="dashboard-chart-wrap"><svg viewBox={`0 0 ${chartWidth} ${chartHeight}`} role="img" aria-label={`${period==='week'?'Seven':'Thirty'} day attendance rate trend`}>
        {[0,25,50,75,100].map(rate=><g key={rate}><line className="chart-grid" x1={plot.left} x2={chartWidth-plot.right} y1={yAt(rate)} y2={yAt(rate)}/><text className="chart-label" x={plot.left-9} y={yAt(rate)+4} textAnchor="end">{rate}%</text></g>)}
        <path className="chart-line" d={line}/>
        {points.map((point,index)=>point.rate===null?null:<circle key={point.date} className="chart-dot" cx={xAt(index,points.length)} cy={yAt(point.rate)} r="5"><title>{point.date}: {point.rate}% ({point.records} records)</title></circle>)}
        {points.map((point,index)=>(period==='week'||index%5===0||index===points.length-1)?<text key={point.date} className="chart-label" x={xAt(index,points.length)} y={chartHeight-12} textAnchor="middle">{dayLabel(point.date,period)}</text>:null)}
      </svg><p className="muted dashboard-chart-note">Rate = (Present + Excused) / attendance records. Days without records are left blank.</p></div>}
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
  const adminCards = [
    {label:'Students Enrolled',value:summary?.students,Icon:Users},
    {label:'Present Today',value:summary?.attendanceToday,Icon:ClipboardCheck},
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
    <div className="grid-two"><div className="card section-pad"><div className="dashboard-classes-head"><h2 className="section-title">Today&apos;s Classes</h2><Link href="/app/attendance">Take attendance →</Link></div>
      {schedules===null?<div className="skeleton-table">{Array.from({length:4},(_,i)=><div className="skeleton-line" key={i}/>)}</div>:schedules.length?schedules.map(item=><div className="dashboard-class-row" key={item.id}><div><strong>{item.grade} – {item.section} · {item.subject}</strong><div className="muted">{item.teacher} · {item.room}</div></div><span>{item.startsAt} – {item.endsAt}</span></div>):<div className="empty">No classes scheduled yet.</div>}
    </div><div className="card section-pad"><h2 className="section-title">Quick actions</h2><p className="muted dashboard-actions-copy">{isAdmin?'Set up your school and begin recording attendance.':'Open your assigned classes and record attendance.'}</p><div className="class-actions">{isAdmin&&<Link className="btn btn-primary" href="/app/academics">Academics</Link>}<Link className="btn btn-outline" href="/app/attendance">Class records</Link></div></div></div>
    <AttendanceTrend/>
  </>;
}
