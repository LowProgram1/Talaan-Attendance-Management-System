'use client';

import { useId, useRef, useState } from 'react';
import { Check, ChevronDown, Search } from 'lucide-react';
import type { SectionOption } from './student-entry-dialog';

export default function SectionCombobox({sections,value,disabled,onChange}:{sections:SectionOption[];value:string;disabled?:boolean;onChange:(value:string)=>void}) {
  const id=useId();
  const [open,setOpen]=useState(false);
  const [query,setQuery]=useState('');
  const [active,setActive]=useState(0);
  const input=useRef<HTMLInputElement>(null);
  const selected=sections.find(s=>s.id===value);
  const options=sections;
  const words=query.toLowerCase().trim().split(/\s+/);
  const filtered=options.filter(s=>words.every(word=>`${s.grade} ${s.name}`.toLowerCase().includes(word)));
  function choose(next:string){onChange(next);setOpen(false);setQuery('');setActive(0);input.current?.focus();}
  function move(index:number){setActive(index);document.getElementById(`${id}-option-${index}`)?.scrollIntoView({block:'nearest'});}

  return <div className="section-combobox" onBlur={e=>{if(!e.currentTarget.contains(e.relatedTarget)){setOpen(false);setQuery('');}}}>
    <label className="label" htmlFor={id}>Destination section</label>
    <div className={`section-combobox-input ${open?'is-open':''}`}><Search size={17}/>
      <input ref={input} id={id} role="combobox" aria-expanded={open} aria-autocomplete="list" aria-controls={`${id}-list`} aria-activedescendant={open&&filtered[active]?`${id}-option-${active}`:undefined} autoComplete="off" disabled={disabled} value={open?query:selected?`${selected.grade} / ${selected.name}`:''} placeholder="Search grade or section…" onFocus={()=>{setOpen(true);setQuery('');setActive(0)}} onClick={()=>setOpen(true)} onChange={e=>{setQuery(e.target.value);setActive(0);setOpen(true)}} onKeyDown={e=>{
        if(e.key==='Escape'&&open){e.preventDefault();e.stopPropagation();setOpen(false);setQuery('');}
        if(e.key==='ArrowDown'||e.key==='ArrowUp'){e.preventDefault();if(!open){setOpen(true);setActive(0)}else move(Math.max(0,Math.min(filtered.length-1,active+(e.key==='ArrowDown'?1:-1))));}
        if(e.key==='Home'&&open){e.preventDefault();move(0)}
        if(e.key==='End'&&open){e.preventDefault();move(Math.max(0,filtered.length-1))}
        if(e.key==='Enter'&&open){e.preventDefault();if(filtered[active])choose(filtered[active].id)}
      }}/><ChevronDown size={16}/>
    </div>
    {open&&!disabled&&<div className="section-combobox-menu"><div className="section-combobox-caption">{filtered.length} {filtered.length===1?'option':'options'} available</div><div id={`${id}-list`} role="listbox" aria-label="Destination sections" className="section-combobox-list">
      {filtered.length?filtered.map((section,index)=><div id={`${id}-option-${index}`} key={section.id} role="option" aria-selected={value===section.id} className={`section-combobox-option ${active===index?'is-active':''}`} onMouseDown={e=>e.preventDefault()} onMouseMove={()=>setActive(index)} onClick={()=>choose(section.id)}><div><strong>{section.name}</strong><span>{section.grade}</span></div>{value===section.id&&<Check size={17}/>}</div>):<p className="section-combobox-empty">No matching sections. Try another grade or name.</p>}
    </div></div>}
  </div>;
}
