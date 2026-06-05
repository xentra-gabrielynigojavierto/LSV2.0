'use client';

import { useState, useCallback, useRef } from 'react';

const BRAND_TOKENS = [
  { token: '{{brand.name}}',         label: 'Brand Name' },
  { token: '{{brand.logoUrl}}',      label: 'Logo URL' },
  { token: '{{brand.primaryColor}}', label: 'Primary Color' },
  { token: '{{brand.secondaryColor}}', label: 'Secondary Color' },
  { token: '{{brand.supportEmail}}', label: 'Support Email' },
  { token: '{{brand.supportPhone}}', label: 'Support Phone' },
  { token: '{{brand.websiteUrl}}',   label: 'Website URL' },
] as const;

interface EditorBlock {
  id: string;
  type: 'heading' | 'paragraph' | 'button' | 'divider' | 'image';
  content: string;
  level?: 1 | 2 | 3;
  styles?: {
    bold?: boolean;
    italic?: boolean;
    underline?: boolean;
    align?: 'left' | 'center' | 'right';
  };
  href?: string;
  src?: string;
  alt?: string;
}

interface EditorJson {
  version: 1;
  blocks: EditorBlock[];
}

interface Props {
  initialJson?: EditorJson | null;
  onChange: (json: EditorJson, html: string, text: string) => void;
}

let idCounter = 0;
function genId() { return `blk_${Date.now()}_${++idCounter}`; }

function blockToHtml(block: EditorBlock): string {
  const content = block.content || '';
  switch (block.type) {
    case 'heading': {
      const tag = `h${block.level ?? 2}`;
      const align = block.styles?.align ? ` style="text-align:${block.styles.align}"` : '';
      return `<${tag}${align}>${content}</${tag}>`;
    }
    case 'paragraph': {
      let html = content.replace(/\n/g, '<br/>');
      if (block.styles?.bold)      html = `<strong>${html}</strong>`;
      if (block.styles?.italic)    html = `<em>${html}</em>`;
      if (block.styles?.underline) html = `<u>${html}</u>`;
      const align = block.styles?.align ? ` style="text-align:${block.styles.align}"` : '';
      return `<p${align}>${html}</p>`;
    }
    case 'button':
      return `<div style="text-align:${block.styles?.align ?? 'center'};margin:16px 0"><a href="${block.href ?? '#'}" style="display:inline-block;padding:12px 24px;background-color:{{brand.primaryColor}};color:#ffffff;text-decoration:none;border-radius:6px;font-weight:600">${content}</a></div>`;
    case 'divider':
      return '<hr style="border:none;border-top:1px solid #e5e7eb;margin:16px 0"/>';
    case 'image':
      return `<div style="text-align:${block.styles?.align ?? 'center'};margin:16px 0"><img src="${block.src ?? '{{brand.logoUrl}}'}" alt="${block.alt ?? ''}" style="max-width:100%;height:auto"/></div>`;
    default:
      return `<p>${content}</p>`;
  }
}

function blockToText(block: EditorBlock): string {
  switch (block.type) {
    case 'heading':
    case 'paragraph':
      return block.content || '';
    case 'button':
      return `[${block.content}](${block.href ?? '#'})`;
    case 'divider':
      return '---';
    case 'image':
      return `[Image: ${block.alt ?? ''}]`;
    default:
      return block.content || '';
  }
}

function compileBlocks(blocks: EditorBlock[]): { html: string; text: string } {
  const html = blocks.map(blockToHtml).join('\n');
  const text = blocks.map(blockToText).filter(Boolean).join('\n\n');
  return { html, text };
}

const defaultBlock = (): EditorBlock => ({
  id: genId(), type: 'paragraph', content: '', styles: {},
});

export function WysiwygEmailEditor({ initialJson, onChange }: Props) {
  const [blocks, setBlocks] = useState<EditorBlock[]>(() => {
    if (initialJson?.blocks?.length) return initialJson.blocks;
    return [defaultBlock()];
  });
  const [tokenMenu, setTokenMenu] = useState<string | null>(null);
  const [varInput,  setVarInput]  = useState('');
  const textAreaRefs = useRef<Record<string, HTMLTextAreaElement | null>>({});

  const emitChange = useCallback((newBlocks: EditorBlock[]) => {
    setBlocks(newBlocks);
    const json: EditorJson = { version: 1, blocks: newBlocks };
    const { html, text } = compileBlocks(newBlocks);
    onChange(json, html, text);
  }, [onChange]);

  function updateBlock(id: string, updates: Partial<EditorBlock>) {
    const newBlocks = blocks.map(b => b.id === id ? { ...b, ...updates } : b);
    emitChange(newBlocks);
  }

  function addBlock(type: EditorBlock['type'], afterId?: string) {
    const newBlock: EditorBlock = { id: genId(), type, content: '', styles: {} };
    if (type === 'heading') newBlock.level = 2;
    if (type === 'button')  newBlock.href = '#';
    if (type === 'image')   newBlock.src = '{{brand.logoUrl}}';
    const idx = afterId ? blocks.findIndex(b => b.id === afterId) + 1 : blocks.length;
    const newBlocks = [...blocks.slice(0, idx), newBlock, ...blocks.slice(idx)];
    emitChange(newBlocks);
  }

  function removeBlock(id: string) {
    if (blocks.length <= 1) return;
    emitChange(blocks.filter(b => b.id !== id));
  }

  function moveBlock(id: string, dir: -1 | 1) {
    const idx = blocks.findIndex(b => b.id === id);
    if (idx < 0) return;
    const target = idx + dir;
    if (target < 0 || target >= blocks.length) return;
    const newBlocks = [...blocks];
    [newBlocks[idx], newBlocks[target]] = [newBlocks[target], newBlocks[idx]];
    emitChange(newBlocks);
  }

  function insertTokenAtCursor(blockId: string, token: string) {
    const ta = textAreaRefs.current[blockId];
    const block = blocks.find(b => b.id === blockId);
    if (!block) return;
    const content = block.content || '';
    if (ta) {
      const start = ta.selectionStart ?? content.length;
      const end = ta.selectionEnd ?? content.length;
      const newContent = content.slice(0, start) + token + content.slice(end);
      updateBlock(blockId, { content: newContent });
      setTimeout(() => {
        ta.focus();
        ta.selectionStart = ta.selectionEnd = start + token.length;
      }, 0);
    } else {
      updateBlock(blockId, { content: content + token });
    }
    setTokenMenu(null);
  }

  function insertCustomVar(blockId: string) {
    if (!varInput.trim()) return;
    insertTokenAtCursor(blockId, `{{${varInput.trim()}}}`);
    setVarInput('');
  }

  const addBlockBtns = (afterId: string) => (
    <div className="flex items-center gap-1 mt-1">
      <span className="text-[10px] text-gray-400 mr-1">Add:</span>
      {([
        ['paragraph', 'ri-text', 'Paragraph'],
        ['heading',   'ri-heading',  'Heading'],
        ['button',    'ri-link',     'Button'],
        ['divider',   'ri-separator','Divider'],
        ['image',     'ri-image-line','Image'],
      ] as const).map(([type, icon, title]) => (
        <button key={type} type="button" title={title}
          onClick={() => addBlock(type, afterId)}
          className="p-1 rounded text-gray-400 hover:text-indigo-600 hover:bg-indigo-50 transition-colors">
          <i className={`${icon} text-sm`} />
        </button>
      ))}
    </div>
  );

  return (
    <div className="space-y-2">
      <div className="text-[11px] text-gray-500 flex items-center gap-2 mb-2">
        <i className="ri-information-line" />
        <span>Build your email template using blocks below. Insert brand tokens and template variables as needed.</span>
      </div>

      {blocks.map((block, idx) => (
        <div key={block.id} className="border border-gray-200 rounded-lg bg-white overflow-hidden group">
          <div className="flex items-center gap-1 px-2 py-1.5 bg-gray-50 border-b border-gray-100">
            <span className="text-[10px] text-gray-400 font-medium uppercase w-16">{block.type}</span>

            {(block.type === 'heading') && (
              <select value={block.level ?? 2}
                onChange={e => updateBlock(block.id, { level: Number(e.target.value) as 1 | 2 | 3 })}
                className="text-[11px] border border-gray-200 rounded px-1 py-0.5 text-gray-600">
                <option value={1}>H1</option>
                <option value={2}>H2</option>
                <option value={3}>H3</option>
              </select>
            )}

            {(block.type === 'paragraph' || block.type === 'heading') && (
              <>
                <button type="button" onClick={() => updateBlock(block.id, { styles: { ...block.styles, bold: !block.styles?.bold } })}
                  className={`px-1.5 py-0.5 rounded text-[11px] font-bold ${block.styles?.bold ? 'bg-indigo-100 text-indigo-700' : 'text-gray-500 hover:bg-gray-100'}`}>B</button>
                <button type="button" onClick={() => updateBlock(block.id, { styles: { ...block.styles, italic: !block.styles?.italic } })}
                  className={`px-1.5 py-0.5 rounded text-[11px] italic ${block.styles?.italic ? 'bg-indigo-100 text-indigo-700' : 'text-gray-500 hover:bg-gray-100'}`}>I</button>
                <button type="button" onClick={() => updateBlock(block.id, { styles: { ...block.styles, underline: !block.styles?.underline } })}
                  className={`px-1.5 py-0.5 rounded text-[11px] underline ${block.styles?.underline ? 'bg-indigo-100 text-indigo-700' : 'text-gray-500 hover:bg-gray-100'}`}>U</button>
              </>
            )}

            {(block.type !== 'divider') && (
              <div className="relative">
                <button type="button" onClick={() => setTokenMenu(tokenMenu === block.id ? null : block.id)}
                  className="px-1.5 py-0.5 rounded text-[11px] text-amber-600 hover:bg-amber-50 font-medium"
                  title="Insert token">
                  <i className="ri-braces-line mr-0.5" />Token
                </button>
                {tokenMenu === block.id && (
                  <div className="absolute top-full left-0 mt-1 z-20 bg-white border border-gray-200 rounded-lg shadow-lg w-64 p-2">
                    <p className="text-[10px] text-gray-400 font-semibold uppercase mb-1 px-1">Brand Tokens (reserved)</p>
                    {BRAND_TOKENS.map(bt => (
                      <button key={bt.token} type="button"
                        onClick={() => insertTokenAtCursor(block.id, bt.token)}
                        className="w-full text-left px-2 py-1 text-[11px] text-gray-700 hover:bg-amber-50 rounded flex items-center gap-2">
                        <code className="font-mono text-amber-600">{bt.token}</code>
                        <span className="text-gray-400">{bt.label}</span>
                      </button>
                    ))}
                    <div className="border-t border-gray-100 mt-1 pt-1">
                      <p className="text-[10px] text-gray-400 font-semibold uppercase mb-1 px-1">Custom Variable</p>
                      <div className="flex items-center gap-1 px-1">
                        <input type="text" value={varInput} onChange={e => setVarInput(e.target.value)}
                          placeholder="variableName"
                          className="flex-1 text-[11px] border border-gray-200 rounded px-1.5 py-0.5 font-mono" />
                        <button type="button" onClick={() => insertCustomVar(block.id)}
                          className="text-[11px] px-2 py-0.5 rounded bg-indigo-50 text-indigo-600 font-medium hover:bg-indigo-100">
                          Insert
                        </button>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            )}

            <div className="flex-1" />
            <button type="button" onClick={() => moveBlock(block.id, -1)} disabled={idx === 0}
              className="p-0.5 rounded text-gray-400 hover:text-gray-600 disabled:opacity-30"><i className="ri-arrow-up-s-line text-sm" /></button>
            <button type="button" onClick={() => moveBlock(block.id, 1)} disabled={idx === blocks.length - 1}
              className="p-0.5 rounded text-gray-400 hover:text-gray-600 disabled:opacity-30"><i className="ri-arrow-down-s-line text-sm" /></button>
            <button type="button" onClick={() => removeBlock(block.id)} disabled={blocks.length <= 1}
              className="p-0.5 rounded text-gray-400 hover:text-red-500 disabled:opacity-30"><i className="ri-delete-bin-line text-sm" /></button>
          </div>

          <div className="p-2">
            {block.type === 'divider' ? (
              <hr className="border-gray-300 my-2" />
            ) : block.type === 'image' ? (
              <div className="space-y-1">
                <input type="text" value={block.src ?? ''} onChange={e => updateBlock(block.id, { src: e.target.value })}
                  placeholder="Image URL or {{brand.logoUrl}}"
                  className="w-full text-[11px] border border-gray-200 rounded px-2 py-1 font-mono" />
                <input type="text" value={block.alt ?? ''} onChange={e => updateBlock(block.id, { alt: e.target.value })}
                  placeholder="Alt text"
                  className="w-full text-[11px] border border-gray-200 rounded px-2 py-1" />
              </div>
            ) : block.type === 'button' ? (
              <div className="space-y-1">
                <input type="text" value={block.content} onChange={e => updateBlock(block.id, { content: e.target.value })}
                  placeholder="Button text"
                  className="w-full text-sm border border-gray-200 rounded px-2 py-1" />
                <input type="text" value={block.href ?? ''} onChange={e => updateBlock(block.id, { href: e.target.value })}
                  placeholder="URL"
                  className="w-full text-[11px] border border-gray-200 rounded px-2 py-1 font-mono" />
              </div>
            ) : (
              <textarea
                ref={el => { textAreaRefs.current[block.id] = el; }}
                value={block.content}
                onChange={e => updateBlock(block.id, { content: e.target.value })}
                placeholder={block.type === 'heading' ? 'Heading text…' : 'Paragraph text…'}
                rows={block.type === 'heading' ? 1 : 3}
                className={`w-full border border-gray-200 rounded px-2 py-1.5 text-sm text-gray-800 resize-none focus:border-indigo-400 focus:ring-1 focus:ring-indigo-400 focus:outline-none ${
                  block.styles?.bold ? 'font-bold' : ''
                } ${block.styles?.italic ? 'italic' : ''} ${block.styles?.underline ? 'underline' : ''} ${
                  block.type === 'heading' ? 'text-lg font-semibold' : ''
                }`}
              />
            )}
          </div>

          {addBlockBtns(block.id)}
        </div>
      ))}

      {blocks.length === 0 && (
        <div className="text-center py-4">
          <button type="button" onClick={() => addBlock('paragraph')}
            className="text-xs text-indigo-600 font-medium hover:text-indigo-800">
            + Add first block
          </button>
        </div>
      )}
    </div>
  );
}
