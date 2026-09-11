// Test-owned loopback registry. It serves only the two local fixture packages.
import http from 'node:http';
import fs from 'node:fs';
import crypto from 'node:crypto';

const config = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const packages = config.packages.map(item => {
  const bytes = fs.readFileSync(item.tarball);
  return { ...item, bytes, shasum: crypto.createHash('sha1').update(bytes).digest('hex'), integrity: 'sha512-' + crypto.createHash('sha512').update(bytes).digest('base64') };
});
const server = http.createServer((request, response) => {
  const path = decodeURIComponent(new URL(request.url, 'http://127.0.0.1').pathname).toLowerCase();
  fs.appendFileSync(config.log, JSON.stringify({ method: request.method, path }) + '\n');
  if (request.method !== 'GET') { response.writeHead(405); response.end(); return; }
  const tarball = packages.find(item => path === `/tarballs/codex-${item.manifest.version}.tgz`);
  if (tarball) { response.writeHead(200, { 'Content-Type': 'application/octet-stream', 'Content-Length': tarball.bytes.length }); response.end(tarball.bytes); return; }
  if (path !== '/@openai/codex' && path !== '/@openai/codex/latest') { response.writeHead(404); response.end('{}'); return; }
  const versions = Object.fromEntries(packages.map(item => [item.manifest.version, {
    ...item.manifest,
    _id: `@openai/codex@${item.manifest.version}`,
    dist: { tarball: `http://127.0.0.1:${server.address().port}/tarballs/codex-${item.manifest.version}.tgz`, shasum: item.shasum, integrity: item.integrity }
  }]));
  const result = path.endsWith('/latest') ? versions['2.0.0'] : { name: '@openai/codex', 'dist-tags': { latest: '2.0.0' }, versions };
  response.writeHead(200, { 'Content-Type': 'application/json' }); response.end(JSON.stringify(result));
});
server.listen(0, '127.0.0.1', () => fs.writeFileSync(config.ready, JSON.stringify({ registry: `http://127.0.0.1:${server.address().port}/` })));
