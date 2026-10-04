import { readFileSync, writeFileSync } from 'node:fs';
const design = JSON.parse(readFileSync(new URL('./dashboard-design.json', import.meta.url), 'utf8'));
const files = [];
function visit(value) {
  if (!value || typeof value !== 'object') return;
  if (value.downloadUrl) files.push(value);
  for (const item of Object.values(value)) visit(item);
}
visit(design);
for (const file of files) {
  const response = await fetch(file.downloadUrl);
  if (!response.ok) throw new Error('Design download failed: ' + response.status);
  const filename = file.mimeType === 'text/html' ? 'dashboard-reference.html' : 'dashboard-reference.png';
  writeFileSync(new URL('./' + filename, import.meta.url), Buffer.from(await response.arrayBuffer()));
  console.log('Saved ' + filename);
}
