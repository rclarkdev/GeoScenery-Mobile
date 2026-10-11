import { readFile, writeFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { resolve } from 'node:path';
import { runInNewContext } from 'node:vm';
import { pathToFileURL } from 'node:url';

const CI_TEST_ORIGINS = new Set([
  'api.geoscenery.test',
  'platform.geoscenery.test'
]);

export function parseProductionConfig(env = process.env, { allowCiTestOrigins = false } = {}) {
  const apiUrl = validateOrigin(env.GEOSCENERY_API_URL, 'GEOSCENERY_API_URL', { allowCiTestOrigins });
  const configuredPlatformUrl = env.GEOSCENERY_PLATFORM_CONFIG_URL?.trim();
  const platformConfigUrl = configuredPlatformUrl
    ? validateOrigin(configuredPlatformUrl, 'GEOSCENERY_PLATFORM_CONFIG_URL', { allowCiTestOrigins })
    : undefined;

  return { apiUrl, platformConfigUrl };
}

function validateOrigin(value, variableName, { allowCiTestOrigins }) {
  if (!value?.trim()) {
    if (variableName === 'GEOSCENERY_PLATFORM_CONFIG_URL') {
      return undefined;
    }
    throw new Error('Set GEOSCENERY_API_URL to the deployed HTTPS API origin before creating a production client build.');
  }

  let parsedUrl;
  try {
    parsedUrl = new URL(value.trim());
  } catch {
    throw new Error(`${variableName} must be a valid HTTPS origin.`);
  }

  const hostname = parsedUrl.hostname.toLowerCase();
  const isIpv4Address = /^\d{1,3}(?:\.\d{1,3}){3}$/.test(hostname);
  const isIpv6Address = hostname.startsWith('[') || hostname.includes(':');
  const isCiFixture = allowCiTestOrigins && CI_TEST_ORIGINS.has(hostname);
  const isReservedOrLocal = hostname === 'localhost'
    || hostname.endsWith('.localhost')
    || hostname.endsWith('.local')
    || hostname.endsWith('.internal')
    || hostname === 'example.com'
    || hostname.endsWith('.example')
    || hostname.endsWith('.example.com')
    || hostname === 'example.org'
    || hostname.endsWith('.example.org')
    || hostname === 'example.net'
    || hostname.endsWith('.example.net')
    || hostname.endsWith('.test')
    || hostname.endsWith('.invalid');

  if (parsedUrl.protocol !== 'https:'
    || parsedUrl.username.length > 0 || parsedUrl.password.length > 0
    || parsedUrl.pathname !== '/' || parsedUrl.search.length > 0 || parsedUrl.hash.length > 0
    || isIpv4Address || isIpv6Address
    || (!isCiFixture && isReservedOrLocal)
    || !hostname.includes('.')) {
    throw new Error(`${variableName} must be a public HTTPS origin${allowCiTestOrigins ? ' (or its designated CI fixture origin)' : ''}.`);
  }

  if (isCiFixture && ((variableName === 'GEOSCENERY_API_URL' && hostname !== 'api.geoscenery.test')
    || (variableName === 'GEOSCENERY_PLATFORM_CONFIG_URL' && hostname !== 'platform.geoscenery.test'))) {
    throw new Error(`${variableName} uses the wrong designated CI fixture hostname.`);
  }

  return parsedUrl.origin;
}

export function renderRuntimeConfig(config) {
  return `window.__GEOSCENERY_CONFIG__ = Object.freeze(${JSON.stringify(config)});\n`;
}

export async function verifyProductionArtifact(outputPath, expectedConfig) {
  const indexHtml = await readFile(resolve(outputPath, 'index.html'), 'utf8');
  const runtimeAssetPath = resolve(outputPath, 'assets/app-config.runtime.js');
  const runtimeAsset = await readFile(runtimeAssetPath, 'utf8');
  const runtimeLoader = /<script\b[^>]*\bsrc=["']assets\/app-config\.runtime\.js["'][^>]*>\s*<\/script>/i.exec(indexHtml);
  const loaderIndex = runtimeLoader?.index ?? -1;
  const appBundleIndex = indexHtml.indexOf('main.');

  if (loaderIndex === -1 || appBundleIndex === -1 || loaderIndex > appBundleIndex) {
    throw new Error('Production artifact must load assets/app-config.runtime.js before the application bundle.');
  }

  const sandbox = { window: {} };
  runInNewContext(runtimeAsset, sandbox);
  const actualConfig = JSON.parse(JSON.stringify(sandbox.window.__GEOSCENERY_CONFIG__));
  if (JSON.stringify(actualConfig) !== JSON.stringify(expectedConfig)) {
    throw new Error('Production runtime asset does not contain the validated production configuration.');
  }
}

async function main() {
  const args = process.argv.slice(2);
  const allowCiTestOrigins = args.length === 1 && args[0] === '--ci';
  if (args.length > 0 && !allowCiTestOrigins) {
    throw new Error('The only supported build option is --ci for non-deployable CI validation builds.');
  }

  const config = parseProductionConfig(process.env, { allowCiTestOrigins });
  const configPath = resolve('src/assets/app-config.runtime.js');
  await writeFile(configPath, renderRuntimeConfig(config), { encoding: 'utf8', mode: 0o644 });

  const angularCli = resolve('node_modules/@angular/cli/bin/ng.js');
  const result = spawnSync(process.execPath, [angularCli, 'build', '--configuration', 'production'], { stdio: 'inherit' });
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }

  await verifyProductionArtifact(resolve('www'), config);
  console.log('Production artifact contains its validated runtime configuration loader and asset.');
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().catch(error => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
