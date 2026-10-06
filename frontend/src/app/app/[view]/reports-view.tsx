'use client';

import { useEffect, useState } from 'react';
import { Download } from 'lucide-react';
import { request } from '@/lib/api';
import Pagination, { PAGE_SIZE } from './pagination';

type Grade = { id:string; name:string; department:string };
type Section = { id:string; name:string; gradeLevelId:string };
type ReportRow = { id:string; subject:string; studentNumber:string; firstName:string; lastName:string; present:number; absent:number; late:number; excused:number };
type Period = 'day' | 'week' | 'month';

const today = () => new Intl.DateTimeFormat('en-CA', {timeZone:'Asia/Manila', year:'numeric', month:'2-digit', day:'2-digit'}).format(new Date());
const dateString = (date:Date) => date.toISOString().slice(0,10);

function range(period:Period, selected:string) {
  if (period === 'month') {
    const [year,month] = selected.slice(0,7).split('-').map(Number);
    return { start:`${selected.slice(0,7)}-01`, end:dateString(new Date(Date.UTC(year,month,0))) };
  }
  if (period === 'day') return { start:selected, end:selected };
  const [year,month,day] = selected.split('-').map(Number);
  const date = new Date(Date.UTC(year,month-1,day));
  date.setUTCDate(date.getUTCDate() - (date.getUTCDay()+6)%7);
  const start = dateString(date);
  date.setUTCDate(date.getUTCDate()+6);
  return { start, end:dateString(date) };
}

export default function ReportsView() {
  const [grades,setGrades] = useState<Grade[]>([]);
  const [sections,setSections] = useState<Section[]>([]);
  const [gradeId,setGradeId] = useState('');
  const [sectionId,setSectionId] = useState('');
  const [period,setPeriod] = useState<Period>('month');
  const [selectedDate,setSelectedDate] = useState(today);
  const [rows,setRows] = useState<ReportRow[]|null>(null);
  const [page,setPage] = useState(1);
  const [error,setError] = useState('');
  const selected = period === 'month' ? selectedDate.slice(0,7) : selectedDate;
  const {start,end} = range(period,selected);
  const currentPage = Math.min(page,Math.max(1,Math.ceil((rows?.length||0)/PAGE_SIZE)));

  useEffect(()=>{
    let active=true;
    Promise.all([request<Grade[]>('/grades'),request<Section[]>('/sections')])
      .then(([g,s])=>{if(active){setGrades(g);setSections(s)}})
      .catch(err=>{if(active)setError(err instanceof Error?err.message:'Unable to load report filters.')});
    return()=>{active=false};
  },[]);
  useEffect(()=>{
    if (!sectionId) return;
    let active=true;
    request<ReportRow[]>(`/reports/summary?sectionId=${sectionId}&startDate=${start}&endDate=${end}`)
      .then(data=>{if(active){setRows(data);setError('')}})
      .catch(err=>{if(active){setRows([]);setError(err instanceof Error?err.message:'Unable to load report.')}});
    return()=>{active=false};
  },[sectionId,start,end]);

  function exportCsv() {
    if (!rows) return;
    const columns = ['Student ID','Student','Subject','Present','Absent','Late','Excused','Rate'];
    const content = [columns,...rows.map(row=>{
      const total=row.present+row.absent+row.late+row.excused;
      return [row.studentNumber,`${row.lastName}, ${row.firstName}`,row.subject,row.present,row.absent,row.late,row.excused,total?`${Math.round((row.present+row.excused)/total*100)}%`:'—'];
    })].map(cells=>cells.map(cell=>`"${String(cell).replaceAll('"','""')}"`).join(',')).join('\n');
    const url=URL.createObjectURL(new Blob([content],{type:'text/csv;charset=utf-8'}));
    const link=document.createElement('a'); link.href=url; link.download=`attendance-${period}-${start}-${end}.csv`; link.click(); URL.revokeObjectURL(url);
  }

  return <>
    <div className="report-toolbar" style={{justifyContent:'space-between',flexWrap:'wrap'}}>
      <div className="class-actions" style={{flexWrap:'wrap'}}>
        <select className="field" aria-label="Grade level" value={gradeId} onChange={event=>{setGradeId(event.target.value);setSectionId('');setRows(null);setPage(1)}}><option value="">Select grade level</option>{grades.map(grade=><option key={grade.id} value={grade.id}>{grade.name}</option>)}</select>
        <select className="field" aria-label="Section" value={sectionId} disabled={!gradeId} onChange={event=>{setSectionId(event.target.value);setRows(null);setPage(1)}}><option value="">Select section</option>{sections.filter(section=>section.gradeLevelId===gradeId).map(section=><option key={section.id} value={section.id}>{section.name}</option>)}</select>
        <select className="field" aria-label="Reporting period" value={period} onChange={event=>{setPeriod(event.target.value as Period);setRows(null);setPage(1)}}><option value="day">Day</option><option value="week">Week</option><option value="month">Month</option></select>
        <input className="field" type={period==='month'?'month':'date'} aria-label={period==='month'?'Report month':period==='week'?'Date in report week':'Report day'} value={selected} onChange={event=>{setSelectedDate(period==='month'?`${event.target.value}-01`:event.target.value);setRows(null);setPage(1)}}/>
      </div>
      <button className="btn btn-orange" onClick={exportCsv} disabled={!rows?.length}><Download size={17}/> Export CSV</button>
    </div>
    <p className="muted" style={{margin:'8px 0 18px'}}>Reporting period: {start} to {end}{period==='week'?' (Monday to Sunday)':''}</p>
    {error&&<p className="error" role="alert">{error}</p>}
    <div className="card table-wrap report-summary"><div className="section-pad"><h2 className="section-title">Attendance report</h2><p className="muted">One row per student and subject for the selected period.</p></div>
      {!sectionId?<div className="empty">Choose a grade level and section to view the report.</div>:rows===null?<div className="skeleton-table" aria-label="Loading report"><div className="skeleton-line"/><div className="skeleton-line"/></div>:rows.length?<table className="table"><thead><tr><th>Student ID</th><th>Student</th><th>Subject</th><th>Present</th><th>Absent</th><th>Late</th><th>Excused</th><th>Rate</th></tr></thead><tbody>{rows.slice((currentPage-1)*PAGE_SIZE,currentPage*PAGE_SIZE).map(row=>{
        const total=row.present+row.absent+row.late+row.excused;
        return <tr key={`${row.id}-${row.studentNumber}`}><td>{row.studentNumber}</td><td>{row.lastName}, {row.firstName}</td><td>{row.subject}</td><td>{row.present}</td><td>{row.absent}</td><td>{row.late}</td><td>{row.excused}</td><td>{total?`${Math.round((row.present+row.excused)/total*100)}%`:'—'}</td></tr>;
      })}</tbody></table>:<div className="empty">No students or subjects found for this section.</div>}
    </div>
    <Pagination page={currentPage} total={rows?.length||0} onPageChange={setPage}/>
  </>;
}
