import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';

const html = readFileSync(new URL('../wwwroot/preview.html', import.meta.url), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
const elements = Object.fromEntries(['frame', 'scale', 'fps', 'pause', 'status'].map(id =>
  [id, { style: {}, value: id === 'scale' ? 'auto' : '10', addEventListener() {} }]));
elements.scale.options = ['auto', '1', '2', '5', '8', '10', '15', '20'].map(value => ({ value }));
const context = vm.createContext({
  document: { getElementById: id => elements[id], documentElement: { clientWidth: 319 } },
  window: { innerHeight: 700, addEventListener() {} },
  localStorage: { getItem() { return ''; } }, performance: { now() { return 0; } },
  setInterval() {}, clearInterval() {},
});
vm.runInContext(script, context);
assert.equal(elements.frame.style.width, '256px');
assert.equal(elements.frame.style.height, '128px');
assert.equal(elements.scale.options[0].textContent, 'Fit (4x)');
vm.runInContext('frameWidth = 128; frameHeight = 64; applyScale()', context);
assert.equal(elements.frame.style.width, '256px');
assert.equal(elements.scale.options[0].textContent, 'Fit (2x)');
elements.scale.value = '10';
vm.runInContext('applyScale()', context);
assert.equal(elements.frame.style.width, '1280px');
assert.match(html, /\.viewport[^}]*overflow: auto/);
elements.scale.value = 'auto';
context.document.documentElement.clientWidth = 1440;
context.window.innerHeight = 900;
vm.runInContext('applyScale()', context);
assert.equal(elements.frame.style.width, '1280px');
context.window.innerHeight = 300;
vm.runInContext('applyScale()', context);
assert.equal(elements.frame.style.width, '128px');
assert.ok(!elements.scale.options.some(option => option.textContent.includes('NaN')));
console.log('Preview fit/zoom tests passed for both frame profiles, narrow and desktop viewports.');
