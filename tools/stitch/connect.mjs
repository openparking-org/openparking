import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { StitchToolClient, StitchProxy } from '@google/stitch-sdk';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';

// Read only the Stitch credential; never copy backend secrets into the process.
const envFile = readFileSync(fileURLToPath(new URL('../../.env', import.meta.url)), 'utf8');
const entry = envFile.match(/^\s*(?:export\s+)?STITCH_API_KEY\s*=\s*(.*)$/m);
let apiKey = process.env.STITCH_API_KEY || entry?.[1]?.trim();
if (apiKey?.startsWith('"') || apiKey?.startsWith("'")) apiKey = apiKey.slice(1, apiKey.lastIndexOf(apiKey[0]));
else apiKey = apiKey?.replace(/\s+#.*$/, '').trim();
if (!apiKey) throw new Error('STITCH_API_KEY is missing from the root .env');

if (process.argv[2] === '--check' || process.argv[2] === '--call') {
  const client = new StitchToolClient({ apiKey });
  try {
    if (process.argv[2] === '--call') {
      const result = await client.callTool(process.argv[3], JSON.parse(readFileSync(process.argv[4], 'utf8')));
      writeFileSync(process.argv[5], JSON.stringify(result, null, 2));
      console.log('Stitch result saved to ' + process.argv[5]);
    } else {
      const { tools } = await client.listTools();
      console.log(JSON.stringify({ connected: true, tools }));
    }
  } catch {
    console.error('Stitch connection failed. Check network access and the Stitch API key.');
    process.exitCode = 1;
  } finally {
    await client.close();
  }
} else {
  const proxy = new StitchProxy({ apiKey });
  await proxy.start(new StdioServerTransport());
}
