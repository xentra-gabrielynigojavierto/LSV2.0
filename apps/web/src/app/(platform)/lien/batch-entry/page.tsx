'use client';

import { useState } from 'react';
import { PageHeader } from '@/components/lien/page-header';

export const dynamic = 'force-dynamic';


const STEPS = ['Upload File', 'Map Fields', 'Validate', 'Import'];

export default function BatchEntryPage() {
  const [currentStep, setCurrentStep] = useState(0);
  const [dragOver, setDragOver] = useState(false);

  return (
    <div className="space-y-5">
      <PageHeader title="Batch Entry" subtitle="Import liens, cases, and contacts in bulk" />

      <div className="bg-white border border-gray-200 rounded-xl p-6">
        <div className="flex items-center justify-between mb-8">
          {STEPS.map((step, i) => (
            <div key={step} className="flex items-center flex-1">
              <div className="flex items-center gap-2">
                <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${
                  i <= currentStep ? 'bg-primary text-white' : 'bg-gray-100 text-gray-400'
                }`}>
                  {i < currentStep ? <i className="ri-check-line" /> : i + 1}
                </div>
                <span className={`text-sm font-medium ${i <= currentStep ? 'text-gray-900' : 'text-gray-400'}`}>{step}</span>
              </div>
              {i < STEPS.length - 1 && <div className={`flex-1 h-px mx-4 ${i < currentStep ? 'bg-primary' : 'bg-gray-200'}`} />}
            </div>
          ))}
        </div>

        {currentStep === 0 && (
          <div className="space-y-6">
            <div
              className={`border-2 border-dashed rounded-xl p-12 text-center transition-colors ${dragOver ? 'border-primary bg-primary/5' : 'border-gray-200'}`}
              onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
              onDragLeave={() => setDragOver(false)}
              onDrop={(e) => { e.preventDefault(); setDragOver(false); }}
            >
              <i className="ri-upload-cloud-2-line text-4xl text-gray-300 mb-3" />
              <p className="text-sm font-medium text-gray-600 mb-1">Drag & drop your file here</p>
              <p className="text-xs text-gray-400 mb-4">Supports CSV, XLSX (max 10MB)</p>
              <button className="text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90">Browse Files</button>
            </div>

            <div className="border border-gray-200 rounded-xl p-5">
              <h3 className="text-sm font-semibold text-gray-800 mb-3">Templates</h3>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                {[
                  { name: 'Liens Import Template', icon: 'ri-stack-line', color: 'text-indigo-600' },
                  { name: 'Cases Import Template', icon: 'ri-folder-open-line', color: 'text-blue-600' },
                  { name: 'Contacts Import Template', icon: 'ri-contacts-book-line', color: 'text-teal-600' },
                ].map((t) => (
                  <button key={t.name} className="flex items-center gap-3 p-3 border border-gray-100 rounded-lg hover:bg-gray-50 transition-colors text-left">
                    <i className={`${t.icon} text-lg ${t.color}`} />
                    <div>
                      <p className="text-sm font-medium text-gray-700">{t.name}</p>
                      <p className="text-xs text-gray-400">Download .xlsx</p>
                    </div>
                  </button>
                ))}
              </div>
            </div>
          </div>
        )}

        {currentStep === 1 && (
          <div className="space-y-4">
            <p className="text-sm text-gray-600">Map your file columns to system fields:</p>
            <div className="border border-gray-200 rounded-lg overflow-hidden">
              <table className="min-w-full divide-y divide-gray-100">
                <thead><tr className="bg-gray-50">
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">File Column</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">System Field</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Preview</th>
                </tr></thead>
                <tbody className="divide-y divide-gray-100">
                  {['Lien Number', 'Type', 'Amount', 'Jurisdiction', 'Subject Name'].map((col) => (
                    <tr key={col} className="hover:bg-gray-50">
                      <td className="px-4 py-3 text-sm text-gray-700">{col}</td>
                      <td className="px-4 py-3"><select className="text-sm border border-gray-200 rounded px-2 py-1"><option>{col.toLowerCase().replace(' ', '_')}</option></select></td>
                      <td className="px-4 py-3 text-xs text-gray-400">Sample data...</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {currentStep === 2 && (
          <div className="space-y-4">
            <div className="flex items-center gap-3 p-4 bg-green-50 border border-green-200 rounded-lg">
              <i className="ri-checkbox-circle-line text-green-600 text-xl" />
              <div>
                <p className="text-sm font-medium text-green-700">Validation Complete</p>
                <p className="text-xs text-green-600">245 records ready to import. 3 warnings found.</p>
              </div>
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div className="bg-white border border-gray-200 rounded-lg p-4 text-center">
                <p className="text-2xl font-bold text-gray-900">245</p>
                <p className="text-xs text-gray-500">Valid Records</p>
              </div>
              <div className="bg-white border border-gray-200 rounded-lg p-4 text-center">
                <p className="text-2xl font-bold text-amber-600">3</p>
                <p className="text-xs text-gray-500">Warnings</p>
              </div>
              <div className="bg-white border border-gray-200 rounded-lg p-4 text-center">
                <p className="text-2xl font-bold text-red-600">0</p>
                <p className="text-xs text-gray-500">Errors</p>
              </div>
            </div>
          </div>
        )}

        {currentStep === 3 && (
          <div className="text-center py-8">
            <i className="ri-checkbox-circle-line text-5xl text-green-500 mb-4" />
            <h3 className="text-lg font-semibold text-gray-900 mb-1">Import Complete</h3>
            <p className="text-sm text-gray-500 mb-4">245 records have been successfully imported.</p>
            <button onClick={() => setCurrentStep(0)} className="text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90">Start New Import</button>
          </div>
        )}

        <div className="flex justify-between mt-8 pt-6 border-t border-gray-100">
          <button onClick={() => setCurrentStep(Math.max(0, currentStep - 1))} disabled={currentStep === 0} className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 disabled:opacity-40 disabled:cursor-not-allowed">
            Back
          </button>
          <button onClick={() => setCurrentStep(Math.min(STEPS.length - 1, currentStep + 1))} disabled={currentStep === STEPS.length - 1} className="text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-40 disabled:cursor-not-allowed">
            {currentStep === 2 ? 'Start Import' : 'Next'}
          </button>
        </div>
      </div>
    </div>
  );
}
