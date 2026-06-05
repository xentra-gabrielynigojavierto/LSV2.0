'use client';

import { useState, useCallback } from 'react';
import type { ColumnConfig, FilterRule, FormulaDefinition, ColumnFormattingRule } from '@/lib/reports/reports.types';

interface ReportBuilderProps {
  availableFields: ColumnConfig[];
  initialColumns: ColumnConfig[];
  initialFilters: FilterRule[];
  initialFormulas?: FormulaDefinition[];
  initialFormatting?: ColumnFormattingRule[];
  onSave: (columns: ColumnConfig[], filters: FilterRule[], formulas: FormulaDefinition[], formatting: ColumnFormattingRule[]) => Promise<void>;
  onSaveAsView?: (name: string, columns: ColumnConfig[], filters: FilterRule[], formulas: FormulaDefinition[], formatting: ColumnFormattingRule[], isDefault: boolean) => Promise<void>;
  onCancel: () => void;
}

const OPERATORS = [
  { value: 'equals', label: 'Equals' },
  { value: 'not_equals', label: 'Not Equals' },
  { value: 'contains', label: 'Contains' },
  { value: 'starts_with', label: 'Starts With' },
  { value: 'ends_with', label: 'Ends With' },
  { value: 'greaterThan', label: 'Greater Than' },
  { value: 'lessThan', label: 'Less Than' },
  { value: 'between', label: 'Between' },
  { value: 'in', label: 'In' },
] as const;

const FORMAT_TYPES = [
  { value: 'currency', label: 'Currency' },
  { value: 'number', label: 'Number' },
  { value: 'percentage', label: 'Percentage' },
  { value: 'date', label: 'Date' },
  { value: 'boolean', label: 'Boolean' },
  { value: 'text', label: 'Text' },
] as const;

const DATA_TYPES = ['number', 'string', 'boolean', 'date'] as const;

type ActiveTab = 'columns' | 'filters' | 'formulas' | 'formatting';

export function ReportBuilder({
  availableFields,
  initialColumns,
  initialFilters,
  initialFormulas = [],
  initialFormatting = [],
  onSave,
  onSaveAsView,
  onCancel,
}: ReportBuilderProps) {
  const [activeTab, setActiveTab] = useState<ActiveTab>('columns');
  const [selectedCols, setSelectedCols] = useState<ColumnConfig[]>(
    initialColumns.length > 0 ? initialColumns : [],
  );
  const [filters, setFilters] = useState<FilterRule[]>(initialFilters);
  const [formulas, setFormulas] = useState<FormulaDefinition[]>(initialFormulas);
  const [formatting, setFormatting] = useState<ColumnFormattingRule[]>(initialFormatting);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showViewDialog, setShowViewDialog] = useState(false);
  const [viewName, setViewName] = useState('');
  const [viewIsDefault, setViewIsDefault] = useState(false);

  const unselected = availableFields.filter(
    (f) => !selectedCols.some((s) => s.name === f.name),
  );

  const allFieldNames = [
    ...availableFields.map(f => f.name),
    ...formulas.map(f => f.fieldName),
  ];

  const addColumn = useCallback((field: ColumnConfig) => {
    setSelectedCols((prev) => [
      ...prev,
      { ...field, order: prev.length, visible: true },
    ]);
  }, []);

  const removeColumn = useCallback((name: string) => {
    setSelectedCols((prev) =>
      prev.filter((c) => c.name !== name).map((c, i) => ({ ...c, order: i })),
    );
  }, []);

  const moveColumn = useCallback((index: number, dir: -1 | 1) => {
    setSelectedCols((prev) => {
      const next = [...prev];
      const target = index + dir;
      if (target < 0 || target >= next.length) return prev;
      [next[index], next[target]] = [next[target], next[index]];
      return next.map((c, i) => ({ ...c, order: i }));
    });
  }, []);

  const renameColumn = useCallback((index: number, label: string) => {
    setSelectedCols((prev) =>
      prev.map((c, i) => (i === index ? { ...c, label } : c)),
    );
  }, []);

  const addFilter = useCallback(() => {
    if (availableFields.length === 0) return;
    setFilters((prev) => [
      ...prev,
      { field: availableFields[0].name, operator: 'equals', value: '' },
    ]);
  }, [availableFields]);

  const updateFilter = useCallback(
    (index: number, patch: Partial<FilterRule>) => {
      setFilters((prev) =>
        prev.map((f, i) => (i === index ? { ...f, ...patch } : f)),
      );
    },
    [],
  );

  const removeFilter = useCallback((index: number) => {
    setFilters((prev) => prev.filter((_, i) => i !== index));
  }, []);

  const addFormula = useCallback(() => {
    setFormulas((prev) => [
      ...prev,
      { fieldName: '', label: '', expression: '', dataType: 'number' as const, order: prev.length + 1 },
    ]);
  }, []);

  const updateFormula = useCallback((index: number, patch: Partial<FormulaDefinition>) => {
    setFormulas((prev) => prev.map((f, i) => (i === index ? { ...f, ...patch } : f)));
  }, []);

  const removeFormula = useCallback((index: number) => {
    setFormulas((prev) => prev.filter((_, i) => i !== index));
  }, []);

  const addFormatting = useCallback(() => {
    if (allFieldNames.length === 0) return;
    setFormatting((prev) => [
      ...prev,
      { fieldName: allFieldNames[0], formatType: 'text' as const },
    ]);
  }, [allFieldNames]);

  const updateFormatting = useCallback((index: number, patch: Partial<ColumnFormattingRule>) => {
    setFormatting((prev) => prev.map((f, i) => (i === index ? { ...f, ...patch } : f)));
  }, []);

  const removeFormatting = useCallback((index: number) => {
    setFormatting((prev) => prev.filter((_, i) => i !== index));
  }, []);

  async function handleSave() {
    if (selectedCols.length === 0) {
      setError('Select at least one column.');
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await onSave(selectedCols, filters, formulas, formatting);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Save failed');
    } finally {
      setSaving(false);
    }
  }

  async function handleSaveAsView() {
    if (!onSaveAsView) return;
    if (!viewName.trim()) {
      setError('View name is required.');
      return;
    }
    if (selectedCols.length === 0) {
      setError('Select at least one column.');
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await onSaveAsView(viewName.trim(), selectedCols, filters, formulas, formatting, viewIsDefault);
      setShowViewDialog(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Save failed');
    } finally {
      setSaving(false);
    }
  }

  const tabs: { key: ActiveTab; label: string; icon: string }[] = [
    { key: 'columns', label: 'Columns', icon: 'ri-layout-column-line' },
    { key: 'filters', label: 'Filters', icon: 'ri-filter-3-line' },
    { key: 'formulas', label: 'Calculated Fields', icon: 'ri-calculator-line' },
    { key: 'formatting', label: 'Formatting', icon: 'ri-paint-brush-line' },
  ];

  return (
    <div className="space-y-6">
      <div className="border-b border-gray-200">
        <nav className="flex gap-4">
          {tabs.map((tab) => (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
              className={`pb-3 px-1 text-sm font-medium border-b-2 transition-colors inline-flex items-center gap-1.5 ${
                activeTab === tab.key
                  ? 'border-primary text-primary'
                  : 'border-transparent text-gray-500 hover:text-gray-700'
              }`}
            >
              <i className={tab.icon} />
              {tab.label}
              {tab.key === 'formulas' && formulas.length > 0 && (
                <span className="text-xs bg-primary/10 text-primary px-1.5 py-0.5 rounded-full">{formulas.length}</span>
              )}
              {tab.key === 'formatting' && formatting.length > 0 && (
                <span className="text-xs bg-primary/10 text-primary px-1.5 py-0.5 rounded-full">{formatting.length}</span>
              )}
            </button>
          ))}
        </nav>
      </div>

      {activeTab === 'columns' && (
        <div className="grid grid-cols-1 lg:grid-cols-[300px_1fr] gap-6">
          <div className="border border-gray-200 rounded-lg">
            <div className="px-4 py-3 border-b border-gray-200 bg-gray-50">
              <h4 className="text-sm font-semibold text-gray-700">Available Fields</h4>
              <p className="text-xs text-gray-500 mt-0.5">{unselected.length} field{unselected.length !== 1 ? 's' : ''}</p>
            </div>
            <div className="p-2 max-h-[400px] overflow-y-auto space-y-1">
              {unselected.length === 0 ? (
                <p className="text-xs text-gray-400 px-2 py-3 text-center">All fields selected</p>
              ) : (
                unselected.map((f) => (
                  <button
                    key={f.name}
                    onClick={() => addColumn(f)}
                    className="w-full flex items-center gap-2 px-3 py-2 text-sm text-gray-700 rounded-md hover:bg-gray-100 transition-colors text-left"
                  >
                    <i className="ri-add-line text-gray-400" />
                    <span className="truncate">{f.label}</span>
                    <span className="text-xs text-gray-400 ml-auto">{f.dataType}</span>
                  </button>
                ))
              )}
            </div>
          </div>

          <div className="border border-gray-200 rounded-lg">
            <div className="px-4 py-3 border-b border-gray-200 bg-gray-50">
              <h4 className="text-sm font-semibold text-gray-700">Selected Columns</h4>
              <p className="text-xs text-gray-500 mt-0.5">{selectedCols.length} column{selectedCols.length !== 1 ? 's' : ''}</p>
            </div>
            <div className="p-2 max-h-[400px] overflow-y-auto space-y-1">
              {selectedCols.length === 0 ? (
                <p className="text-xs text-gray-400 px-2 py-3 text-center">Add fields from the left panel</p>
              ) : (
                selectedCols.map((col, i) => (
                  <div
                    key={col.name}
                    className="flex items-center gap-2 px-3 py-2 bg-white border border-gray-200 rounded-md"
                  >
                    <span className="text-xs text-gray-400 w-5 shrink-0">{i + 1}</span>
                    <input
                      type="text"
                      value={col.label}
                      onChange={(e) => renameColumn(i, e.target.value)}
                      className="flex-1 text-sm text-gray-700 bg-transparent border-0 p-0 focus:ring-0 focus:outline-none"
                    />
                    <span className="text-xs text-gray-400">{col.dataType}</span>
                    <div className="flex items-center gap-0.5 ml-1">
                      <button
                        onClick={() => moveColumn(i, -1)}
                        disabled={i === 0}
                        className="p-1 text-gray-400 hover:text-gray-600 disabled:opacity-30"
                      >
                        <i className="ri-arrow-up-s-line text-sm" />
                      </button>
                      <button
                        onClick={() => moveColumn(i, 1)}
                        disabled={i === selectedCols.length - 1}
                        className="p-1 text-gray-400 hover:text-gray-600 disabled:opacity-30"
                      >
                        <i className="ri-arrow-down-s-line text-sm" />
                      </button>
                      <button
                        onClick={() => removeColumn(col.name)}
                        className="p-1 text-gray-400 hover:text-red-500"
                      >
                        <i className="ri-close-line text-sm" />
                      </button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      )}

      {activeTab === 'filters' && (
        <div className="border border-gray-200 rounded-lg">
          <div className="px-4 py-3 border-b border-gray-200 bg-gray-50 flex items-center justify-between">
            <div>
              <h4 className="text-sm font-semibold text-gray-700">Filters</h4>
              <p className="text-xs text-gray-500 mt-0.5">{filters.length} filter{filters.length !== 1 ? 's' : ''}</p>
            </div>
            <button
              onClick={addFilter}
              className="text-xs font-medium text-primary hover:text-primary/80 inline-flex items-center gap-1"
            >
              <i className="ri-add-line" />
              Add Filter
            </button>
          </div>
          <div className="p-3 space-y-2">
            {filters.length === 0 ? (
              <p className="text-xs text-gray-400 text-center py-3">No filters applied</p>
            ) : (
              filters.map((f, i) => (
                <div key={i} className="flex items-center gap-2 flex-wrap">
                  <select
                    value={f.field}
                    onChange={(e) => updateFilter(i, { field: e.target.value })}
                    className="text-sm border border-gray-300 rounded-md px-2 py-1.5 bg-white"
                  >
                    {availableFields.map((af) => (
                      <option key={af.name} value={af.name}>{af.label}</option>
                    ))}
                  </select>
                  <select
                    value={f.operator}
                    onChange={(e) => updateFilter(i, { operator: e.target.value as FilterRule['operator'] })}
                    className="text-sm border border-gray-300 rounded-md px-2 py-1.5 bg-white"
                  >
                    {OPERATORS.map((op) => (
                      <option key={op.value} value={op.value}>{op.label}</option>
                    ))}
                  </select>
                  <input
                    type="text"
                    value={f.value}
                    onChange={(e) => updateFilter(i, { value: e.target.value })}
                    placeholder="Value"
                    className="text-sm border border-gray-300 rounded-md px-2 py-1.5 flex-1 min-w-[120px]"
                  />
                  {f.operator === 'between' && (
                    <input
                      type="text"
                      value={f.value2 ?? ''}
                      onChange={(e) => updateFilter(i, { value2: e.target.value })}
                      placeholder="End value"
                      className="text-sm border border-gray-300 rounded-md px-2 py-1.5 min-w-[120px]"
                    />
                  )}
                  <button
                    onClick={() => removeFilter(i)}
                    className="p-1.5 text-gray-400 hover:text-red-500"
                  >
                    <i className="ri-delete-bin-line text-sm" />
                  </button>
                </div>
              ))
            )}
          </div>
        </div>
      )}

      {activeTab === 'formulas' && (
        <div className="border border-gray-200 rounded-lg">
          <div className="px-4 py-3 border-b border-gray-200 bg-gray-50 flex items-center justify-between">
            <div>
              <h4 className="text-sm font-semibold text-gray-700">Calculated Fields</h4>
              <p className="text-xs text-gray-500 mt-0.5">
                {formulas.length} field{formulas.length !== 1 ? 's' : ''} &mdash; Use [FieldName] to reference columns
              </p>
            </div>
            <button
              onClick={addFormula}
              className="text-xs font-medium text-primary hover:text-primary/80 inline-flex items-center gap-1"
            >
              <i className="ri-add-line" />
              Add Formula
            </button>
          </div>
          <div className="p-3 space-y-3">
            {formulas.length === 0 ? (
              <p className="text-xs text-gray-400 text-center py-3">No calculated fields defined</p>
            ) : (
              formulas.map((formula, i) => (
                <div key={i} className="border border-gray-200 rounded-md p-3 space-y-2">
                  <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                    <div>
                      <label className="text-xs text-gray-500 mb-1 block">Field Name</label>
                      <input
                        type="text"
                        value={formula.fieldName}
                        onChange={(e) => updateFormula(i, { fieldName: e.target.value })}
                        placeholder="e.g. profit_margin"
                        className="w-full text-sm border border-gray-300 rounded-md px-2 py-1.5"
                      />
                    </div>
                    <div>
                      <label className="text-xs text-gray-500 mb-1 block">Label</label>
                      <input
                        type="text"
                        value={formula.label}
                        onChange={(e) => updateFormula(i, { label: e.target.value })}
                        placeholder="e.g. Profit Margin"
                        className="w-full text-sm border border-gray-300 rounded-md px-2 py-1.5"
                      />
                    </div>
                    <div>
                      <label className="text-xs text-gray-500 mb-1 block">Data Type</label>
                      <select
                        value={formula.dataType}
                        onChange={(e) => updateFormula(i, { dataType: e.target.value as FormulaDefinition['dataType'] })}
                        className="w-full text-sm border border-gray-300 rounded-md px-2 py-1.5 bg-white"
                      >
                        {DATA_TYPES.map((dt) => (
                          <option key={dt} value={dt}>{dt}</option>
                        ))}
                      </select>
                    </div>
                  </div>
                  <div>
                    <label className="text-xs text-gray-500 mb-1 block">Expression</label>
                    <input
                      type="text"
                      value={formula.expression}
                      onChange={(e) => updateFormula(i, { expression: e.target.value })}
                      placeholder="e.g. ([Revenue] - [Cost]) / [Revenue] * 100"
                      className="w-full text-sm border border-gray-300 rounded-md px-2 py-1.5 font-mono"
                    />
                  </div>
                  <div className="flex justify-end">
                    <button
                      onClick={() => removeFormula(i)}
                      className="text-xs text-red-500 hover:text-red-700 inline-flex items-center gap-1"
                    >
                      <i className="ri-delete-bin-line" />
                      Remove
                    </button>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      )}

      {activeTab === 'formatting' && (
        <div className="border border-gray-200 rounded-lg">
          <div className="px-4 py-3 border-b border-gray-200 bg-gray-50 flex items-center justify-between">
            <div>
              <h4 className="text-sm font-semibold text-gray-700">Column Formatting</h4>
              <p className="text-xs text-gray-500 mt-0.5">{formatting.length} rule{formatting.length !== 1 ? 's' : ''}</p>
            </div>
            <button
              onClick={addFormatting}
              className="text-xs font-medium text-primary hover:text-primary/80 inline-flex items-center gap-1"
            >
              <i className="ri-add-line" />
              Add Rule
            </button>
          </div>
          <div className="p-3 space-y-3">
            {formatting.length === 0 ? (
              <p className="text-xs text-gray-400 text-center py-3">No formatting rules defined</p>
            ) : (
              formatting.map((rule, i) => (
                <div key={i} className="flex items-start gap-2 flex-wrap border border-gray-200 rounded-md p-3">
                  <div className="flex-1 min-w-[140px]">
                    <label className="text-xs text-gray-500 mb-1 block">Field</label>
                    <select
                      value={rule.fieldName}
                      onChange={(e) => updateFormatting(i, { fieldName: e.target.value })}
                      className="w-full text-sm border border-gray-300 rounded-md px-2 py-1.5 bg-white"
                    >
                      {allFieldNames.map((name) => (
                        <option key={name} value={name}>{name}</option>
                      ))}
                    </select>
                  </div>
                  <div className="flex-1 min-w-[120px]">
                    <label className="text-xs text-gray-500 mb-1 block">Format</label>
                    <select
                      value={rule.formatType}
                      onChange={(e) => updateFormatting(i, { formatType: e.target.value as ColumnFormattingRule['formatType'] })}
                      className="w-full text-sm border border-gray-300 rounded-md px-2 py-1.5 bg-white"
                    >
                      {FORMAT_TYPES.map((ft) => (
                        <option key={ft.value} value={ft.value}>{ft.label}</option>
                      ))}
                    </select>
                  </div>
                  {(rule.formatType === 'currency' || rule.formatType === 'number' || rule.formatType === 'percentage') && (
                    <div className="w-[80px]">
                      <label className="text-xs text-gray-500 mb-1 block">Decimals</label>
                      <input
                        type="number"
                        min={0}
                        max={10}
                        value={rule.decimalPlaces ?? 2}
                        onChange={(e) => updateFormatting(i, { decimalPlaces: Number(e.target.value) })}
                        className="w-full text-sm border border-gray-300 rounded-md px-2 py-1.5"
                      />
                    </div>
                  )}
                  {rule.formatType === 'currency' && (
                    <div className="w-[60px]">
                      <label className="text-xs text-gray-500 mb-1 block">Prefix</label>
                      <input
                        type="text"
                        value={rule.prefix ?? '$'}
                        onChange={(e) => updateFormatting(i, { prefix: e.target.value })}
                        className="w-full text-sm border border-gray-300 rounded-md px-2 py-1.5"
                      />
                    </div>
                  )}
                  {rule.formatType === 'percentage' && (
                    <div className="w-[60px]">
                      <label className="text-xs text-gray-500 mb-1 block">Suffix</label>
                      <input
                        type="text"
                        value={rule.suffix ?? '%'}
                        onChange={(e) => updateFormatting(i, { suffix: e.target.value })}
                        className="w-full text-sm border border-gray-300 rounded-md px-2 py-1.5"
                      />
                    </div>
                  )}
                  {rule.formatType === 'boolean' && (
                    <>
                      <div className="w-[80px]">
                        <label className="text-xs text-gray-500 mb-1 block">True</label>
                        <input
                          type="text"
                          value={rule.trueLabel ?? 'Yes'}
                          onChange={(e) => updateFormatting(i, { trueLabel: e.target.value })}
                          className="w-full text-sm border border-gray-300 rounded-md px-2 py-1.5"
                        />
                      </div>
                      <div className="w-[80px]">
                        <label className="text-xs text-gray-500 mb-1 block">False</label>
                        <input
                          type="text"
                          value={rule.falseLabel ?? 'No'}
                          onChange={(e) => updateFormatting(i, { falseLabel: e.target.value })}
                          className="w-full text-sm border border-gray-300 rounded-md px-2 py-1.5"
                        />
                      </div>
                    </>
                  )}
                  <div className="self-end">
                    <button
                      onClick={() => removeFormatting(i)}
                      className="p-1.5 text-gray-400 hover:text-red-500"
                    >
                      <i className="ri-delete-bin-line text-sm" />
                    </button>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      )}

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3">
          <p className="text-sm text-red-700">{error}</p>
        </div>
      )}

      <div className="flex items-center justify-end gap-3">
        <button
          onClick={onCancel}
          className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
        >
          Cancel
        </button>
        {onSaveAsView && (
          <button
            onClick={() => setShowViewDialog(true)}
            disabled={saving || selectedCols.length === 0}
            className="px-4 py-2 text-sm font-medium text-primary bg-white border border-primary rounded-lg hover:bg-primary/5 disabled:opacity-50 inline-flex items-center gap-1.5"
          >
            <i className="ri-bookmark-line" />
            Save as View
          </button>
        )}
        <button
          onClick={handleSave}
          disabled={saving || selectedCols.length === 0}
          className="px-4 py-2 text-sm font-medium text-white bg-primary rounded-lg hover:bg-primary/90 disabled:opacity-50"
        >
          {saving ? 'Saving...' : 'Save Configuration'}
        </button>
      </div>

      {showViewDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-xl shadow-xl w-full max-w-md p-6 space-y-4">
            <h3 className="text-lg font-semibold text-gray-900">Save as View</h3>
            <div>
              <label className="text-sm font-medium text-gray-700 mb-1 block">View Name</label>
              <input
                type="text"
                value={viewName}
                onChange={(e) => setViewName(e.target.value)}
                placeholder="e.g. Monthly Summary"
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
                autoFocus
              />
            </div>
            <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
              <input
                type="checkbox"
                checked={viewIsDefault}
                onChange={(e) => setViewIsDefault(e.target.checked)}
                className="rounded border-gray-300"
              />
              Set as default view
            </label>
            <div className="flex justify-end gap-3">
              <button
                onClick={() => setShowViewDialog(false)}
                className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
              >
                Cancel
              </button>
              <button
                onClick={handleSaveAsView}
                disabled={saving || !viewName.trim()}
                className="px-4 py-2 text-sm font-medium text-white bg-primary rounded-lg hover:bg-primary/90 disabled:opacity-50"
              >
                {saving ? 'Saving...' : 'Save View'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
