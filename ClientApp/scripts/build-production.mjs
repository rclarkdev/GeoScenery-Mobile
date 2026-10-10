import { writeFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { resolve } from 'node:path';

const apiUrl = process.env.GEOSCENERY_API_URL?.trim();
if (!apiUrl) {
  console.error('Set GEOSCENERY_API_URL to the deployed HTTPS API origin before creating a production client build.');
  process.exit(1);
}

let parsedApiUrl;
try {
  parsedApiUrl = new URL(apiUrl);
} catch {
  console.error('GEOSCENERY_API_URL must be a valid HTTPS URL.');
  process.exit(1);
}

const hostname = parsedApiUrl.hostname.toLowerCase();
const isIpv4Address = /^\d{1,3}(?:\.\d{1,3}){3}$/.test(hostname);
const isIpv6Address = hostname.startsWith('[') || hostname.includes(':');
if (parsedApiUrl.protocol !== 'https:'
  || isIpv4Address
  || isIpv6Address
  || hostname === 'localhost'
  || hostname.endsWith('.localhost')
  || hostname.endsWith('.local')
  || hostname.endsWith('.internal')
  || hostname.endsWith('.example')
  || hostname.endsWith('.example.com')
  || hostname.endsWith('.test')
  || hostname.endsWith('.invalid')
  || !hostname.includes('.')) {
  console.error('GEOSCENERY_API_URL must be a public HTTPS DNS hostname, not a placeholder, local, emulator, or IP address.');
  process.exit(1);
}

let platformConfigUrl;
const configuredPlatformUrl = process.env.GEOSCENERY_PLATFORM_CONFIG_URL?.trim();
if (configuredPlatformUrl) {
  let parsedPlatformUrl;
  try {
    parsedPlatformUrl = new URL(configuredPlatformUrl);
  } catch {
    console.error('GEOSCENERY_PLATFORM_CONFIG_URL must be a valid HTTPS URL when provided.');
    process.exit(1);
  }

  const platformHostname = parsedPlatformUrl.hostname.toLowerCase();
  const platformIsIpv4 = /^\d{1,3}(?:\.\d{1,3}){3}$/.test(platformHostname);
  const platformIsIpv6 = platformHostname.startsWith('[') || platformHostname.includes(':');
  if (parsedPlatformUrl.protocol !== 'https:'
    || parsedPlatformUrl.username.length > 0
    || parsedPlatformUrl.password.length > 0
    || parsedPlatformUrl.pathname !== '/'
    || parsedPlatformUrl.search.length > 0
    || parsedPlatformUrl.hash.length > 0
    || platformIsIpv4
    || platformIsIpv6
    || platformHostname === 'localhost'
    || platformHostname.endsWith('.localhost')
    || platformHostname.endsWith('.local')
    || platformHostname.endsWith('.internal')
    || platformHostname.endsWith('.example')
    || platformHostname.endsWith('.example.com')
    || platformHostname.endsWith('.test')
    || platformHostname.endsWith('.invalid')
    || !platformHostname.includes('.')) {
    console.error('GEOSCENERY_PLATFORM_CONFIG_URL must be a public HTTPS DNS origin when provided.');
    process.exit(1);
  }

  platformConfigUrl = parsedPlatformUrl.origin;
}

const configPath = resolve('src/assets/app-config.runtime.js');
const config = `window.__GEOSCENERY_CONFIG__ = Object.freeze(${JSON.stringify({
  apiUrl: parsedApiUrl.origin,
  platformConfigUrl
})});\n`;
await writeFile(configPath, config, { encoding: 'utf8', mode: 0o644 });

const angularCli = resolve('node_modules/@angular/cli/bin/ng.js');
const result = spawnSync(process.execPath, [angularCli, 'build', '--configuration', 'production'], { stdio: 'inherit' });
process.exit(result.status ?? 1);
