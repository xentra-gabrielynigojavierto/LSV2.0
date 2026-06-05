#!/usr/bin/env node
const fs = require('fs');
const path = require('path');

const target = path.join(
  __dirname, '..', 'node_modules', 'next', 'dist', 'server', 'app-render', 'app-render.js'
);

if (!fs.existsSync(target)) {
  console.log('[patch] next app-render.js not found, skipping');
  process.exit(0);
}

let src = fs.readFileSync(target, 'utf8');

const MARKER = '__COLD_COMPILE_PATCHED__';
if (src.includes(MARKER)) {
  console.log('[patch] next app-render.js already patched');
  process.exit(0);
}

const fallbackFn = `
/* ${MARKER} */
var __fallbackMetadataComponents = function() {
  var Noop = function() { return null; };
  return {
    MetadataTree: Noop,
    ViewportTree: Noop,
    getViewportReady: function() { return Promise.resolve(); },
    getMetadataReady: function() { return Promise.resolve(); },
    StreamingMetadataOutlet: Noop
  };
};
`;

src = fallbackFn + src;

let count = 0;

src = src.replace(
  /(\bcreateDivergedMetadataComponents\b)/,
  function(match) {
    return match;
  }
);

src = src.replace(
  /\bcreateMetadataComponents\s*\(\s*\{/g,
  function(match) {
    count++;
    return '(typeof createMetadataComponents === "function" ? createMetadataComponents : __fallbackMetadataComponents)({';
  }
);

if (count === 0) {
  console.log('[patch] next app-render.js: no createMetadataComponents calls found, skipping');
  process.exit(0);
}

fs.writeFileSync(target, src, 'utf8');
console.log(`[patch] next app-render.js patched: ${count} createMetadataComponents call(s) guarded with fallback`);
