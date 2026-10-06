'use client';

export const PAGE_SIZE = 20;

export default function Pagination({page,total,onPageChange,pageSize=PAGE_SIZE}:{page:number;total:number;onPageChange:(page:number)=>void;pageSize?:number}) {
  const pages = Math.ceil(total/pageSize);
  if (pages <= 1) return null;
  return <nav className="pagination" aria-label="Table pagination">
    <button type="button" className="btn btn-outline" disabled={page<=1} onClick={()=>onPageChange(page-1)}>Previous</button>
    <span>Page {page} of {pages}</span>
    <button type="button" className="btn btn-outline" disabled={page>=pages} onClick={()=>onPageChange(page+1)}>Next</button>
  </nav>;
}
