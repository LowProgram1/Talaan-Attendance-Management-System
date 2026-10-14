'use client';

import { ChevronLeft, ChevronRight } from 'lucide-react';

export const PAGE_SIZE = 20;

export default function Pagination({page,total,onPageChange,pageSize=PAGE_SIZE}:{page:number;total:number;onPageChange:(page:number)=>void;pageSize?:number}) {
  const pages = Math.ceil(total/pageSize);
  if (pages <= 1) return null;
  return <nav className="pagination" aria-label="Table pagination">
    <span className="pagination-summary">Showing <strong>{(page-1)*pageSize+1}–{Math.min(page*pageSize,total)}</strong> of <strong>{total}</strong> records</span>
    <div className="pagination-controls">
      <button type="button" className="btn btn-outline" disabled={page<=1} onClick={()=>onPageChange(page-1)}><ChevronLeft size={15} aria-hidden="true"/>Previous</button>
      <span className="pagination-current" aria-live="polite">Page {page} of {pages}</span>
      <button type="button" className="btn btn-outline" disabled={page>=pages} onClick={()=>onPageChange(page+1)}>Next<ChevronRight size={15} aria-hidden="true"/></button>
    </div>
  </nav>;
}
