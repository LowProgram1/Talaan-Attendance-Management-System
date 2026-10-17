import type { Metadata } from 'next';
import { Suspense } from 'react';
import ResetForm from './reset-form';

export const metadata:Metadata={title:'Reset password | Student Attendance Management Information System',referrer:'no-referrer',robots:{index:false,follow:false}};

export default function ResetPage(){
  return <Suspense fallback={<main className="recovery-page"><p role="status">Loading password reset…</p></main>}><ResetForm/></Suspense>;
}
