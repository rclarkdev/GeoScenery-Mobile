import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { dirname, join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath, pathToFileURL } from 'node:url';

const CLIENT_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const RUNTIME_CONFIG_RELATIVE_PATH = 'src/assets/app-config.runtime.js';
const PRODUCTION_OUTPUT_RELATIVE_PATH = 'www';

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

export async function clearGeneratedBuildArtifacts(clientRoot) {
  await Promise.all([
    rm(resolve(clientRoot, RUNTIME_CONFIG_RELATIVE_PATH), { force: true }),
    rm(resolve(clientRoot, PRODUCTION_OUTPUT_RELATIVE_PATH), { recursive: true, force: true })
  ]);
}

export async function verifyProductionArtifact(outputPath, expectedConfig) {
  const indexHtml = await readFile(resolve(outputPath, 'index.html'), 'utf8');
  const runtimeAssetPath = resolve(outputPath, 'assets/app-config.runtime.js');
  const runtimeAsset = await readFile(runtimeAssetPath, 'utf8');
  const scripts = [...indexHtml.matchAll(/<script\b([^>]*)>/gi)]
    .map(match => ({ index: match.index, attributes: match[1], src: readAttribute(match[1], 'src') }))
    .filter(script => script.src !== null)
    .map(script => ({ ...script, pathname: getLocalPathname(script.src) }));
  const runtimeScript = scripts.find(script => script.pathname === '/assets/app-config.runtime.js');
  const appScript = scripts.find(script => script.pathname && /(?:^|\/)main(?:\.[^/]*)?\.js$/i.test(script.pathname));

  if (!runtimeScript || !appScript || runtimeScript.index > appScript.index
    || hasAttribute(runtimeScript.attributes, 'async')
    || hasAttribute(runtimeScript.attributes, 'defer')
    || readAttribute(runtimeScript.attributes, 'type')?.toLowerCase() === 'module') {
    throw new Error('Production artifact must load assets/app-config.runtime.js before the application bundle.');
  }

  const actualConfig = parseRuntimeConfig(runtimeAsset);
  const actualKeys = Object.keys(actualConfig).sort();
  const expectedKeys = Object.keys(expectedConfig).filter(key => expectedConfig[key] !== undefined).sort();
  if (JSON.stringify(actualKeys) !== JSON.stringify(expectedKeys)
    || actualConfig.apiUrl !== expectedConfig.apiUrl
    || actualConfig.platformConfigUrl !== expectedConfig.platformConfigUrl) {
    throw new Error('Production runtime asset does not contain the validated production configuration.');
  }
}

function readAttribute(attributes, name) {
  const match = new RegExp(`(?:^|\\s)${name}\\s*=\\s*(?:"([^"]*)"|'([^']*)'|([^\\s"'=<>\x60]+))`, 'i')
    .exec(attributes);
  return match ? (match[1] ?? match[2] ?? match[3]) : null;
}

function hasAttribute(attributes, name) {
  return new RegExp(`(?:^|\\s)${name}(?:\\s|=|$)`, 'i').test(attributes);
}

function getLocalPathname(source) {
  try {
    if (/^[a-z][a-z\d+.-]*:/i.test(source) || source.startsWith('//')) {
      return null;
    }
    const url = new URL(source, 'https://production-artifact.invalid/');
    return url.origin === 'https://production-artifact.invalid' ? decodeURIComponent(url.pathname) : null;
  } catch {
    return null;
  }
}

function parseRuntimeConfig(source) {
  const assignment = /^\s*window\s*\.\s*__GEOSCENERY_CONFIG__\s*=\s*Object\s*\.\s*freeze\s*\(\s*(\{[\s\S]*\})\s*\)\s*;?\s*$/.exec(source);
  if (!assignment) {
    throw new Error('Production runtime asset does not contain a valid configuration assignment.');
  }

  let config;
  try {
    config = JSON.parse(assignment[1]);
  } catch {
    throw new Error('Production runtime asset contains invalid JSON configuration.');
  }
  if (typeof config !== 'object' || config === null || Array.isArray(config)) {
    throw new Error('Production runtime asset configuration must be an object.');
  }
  return config;
}

export async function runProductionBuild({ clientRoot = CLIENT_ROOT, env = process.env, args = [], buildAngular } = {}) {
  await clearGeneratedBuildArtifacts(clientRoot);
  let isCiBuild = false;
  let ciOutputPath;
  const runtimeConfigPath = resolve(clientRoot, RUNTIME_CONFIG_RELATIVE_PATH);

  try {
    isCiBuild = args.length === 1 && args[0] === '--ci';
    if (args.length > 0 && !isCiBuild) {
      throw new Error('The only supported build option is --ci for non-deployable CI validation builds.');
    }

    const config = parseProductionConfig(env, { allowCiTestOrigins: isCiBuild });
    await writeFile(runtimeConfigPath, renderRuntimeConfig(config), { encoding: 'utf8', mode: 0o644 });

    const outputPath = isCiBuild
      ? await mkdtemp(join(tmpdir(), 'geoscenery-ci-validation-'))
      : resolve(clientRoot, PRODUCTION_OUTPUT_RELATIVE_PATH);
    if (isCiBuild) {
      ciOutputPath = outputPath;
    }

    const angularCli = resolve(clientRoot, 'node_modules/@angular/cli/bin/ng.js');
    const result = buildAngular
      ? await buildAngular({ clientRoot, outputPath })
      : spawnSync(process.execPath, [angularCli, 'build', '--configuration', 'production', '--output-path', outputPath], {
        cwd: clientRoot,
        stdio: 'inherit'
      });
    if (result.error) {
      throw new Error(`Angular production build could not start: ${result.error.message}`);
    }
    if (result.status !== 0) {
      throw new Error(`Angular production build failed with status ${result.status ?? 'unknown'}.`);
    }

    await verifyProductionArtifact(outputPath, config);
    return { config, outputPath, isCiBuild };
  } catch (error) {
    await clearGeneratedBuildArtifacts(clientRoot);
    throw error;
  } finally {
    if (ciOutputPath) {
      await rm(ciOutputPath, { recursive: true, force: true });
    }
    if (isCiBuild) {
      await rm(runtimeConfigPath, { force: true });
    }
  }
}

async function main() {
  const { isCiBuild } = await runProductionBuild({ args: process.argv.slice(2) });
  if (isCiBuild) {
    console.log('CI production validation passed; its temporary artifact was discarded.');
  } else {
    console.log('Production artifact contains its validated runtime configuration loader and asset.');
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().catch(error => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
