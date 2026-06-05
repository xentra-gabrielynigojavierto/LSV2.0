import Link from "next/link";

export default function Home() {
  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 flex items-center justify-center">
      <main className="text-center px-8">
        <div className="mb-8">
          <div className="inline-flex items-center justify-center w-20 h-20 rounded-2xl bg-gradient-to-br from-blue-500 to-indigo-600 mb-6">
            <svg
              className="w-10 h-10 text-white"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M13 10V3L4 14h7v7l9-11h-7z"
              />
            </svg>
          </div>
          <h1 className="text-5xl font-bold text-white mb-3 tracking-tight">
            Flow
          </h1>
          <p className="text-slate-400 text-lg max-w-md mx-auto">
            Workflow and task orchestration service.
          </p>
        </div>

        <div className="mt-10 flex gap-4">
          <Link
            href="/tasks"
            className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-6 py-3 text-sm font-medium text-white transition-colors hover:bg-blue-700"
          >
            <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 012-2h2a2 2 0 012 2M9 5h6" />
            </svg>
            Open Task Queue
          </Link>
          <Link
            href="/workflows"
            className="inline-flex items-center gap-2 rounded-lg border border-slate-600 px-6 py-3 text-sm font-medium text-slate-300 transition-colors hover:bg-slate-800 hover:text-white"
          >
            <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
            Workflow Designer
          </Link>
          <Link
            href="/notifications"
            className="inline-flex items-center gap-2 rounded-lg border border-slate-600 px-6 py-3 text-sm font-medium text-slate-300 transition-colors hover:bg-slate-800 hover:text-white"
          >
            <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
            </svg>
            Notifications
          </Link>
        </div>

        <div className="flex flex-col sm:flex-row gap-4 justify-center items-center mt-10">
          <div className="bg-slate-800/50 border border-slate-700 rounded-xl px-6 py-4 text-left min-w-[200px]">
            <p className="text-slate-500 text-xs uppercase tracking-wider mb-1">Status</p>
            <p className="text-emerald-400 font-semibold">Operational</p>
          </div>
          <div className="bg-slate-800/50 border border-slate-700 rounded-xl px-6 py-4 text-left min-w-[200px]">
            <p className="text-slate-500 text-xs uppercase tracking-wider mb-1">Version</p>
            <p className="text-white font-semibold">1.1.0</p>
          </div>
          <div className="bg-slate-800/50 border border-slate-700 rounded-xl px-6 py-4 text-left min-w-[200px]">
            <p className="text-slate-500 text-xs uppercase tracking-wider mb-1">Stack</p>
            <p className="text-white font-semibold">Next.js + Tailwind</p>
          </div>
        </div>
      </main>
    </div>
  );
}
