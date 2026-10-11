import assert from 'node:assert/strict';
import { access, mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { runInNewContext } from 'node:vm';
import test from 'node:test';
import {
  runProductionBuild,
  parseProductionConfig,
  renderRuntimeConfig,
  verifyProductionArtifact
} from './build-production.mjs';

const validApiUrl = 'https://api.geoscenery-validation.net';

async function pathExists(path) {
  try {
    await access(path);
    return true;
  } catch {
    return false;
  }
}

test('requires the API URL for production configuration', () => {
  assert.throws(() => parseProductionConfig({}), /Set GEOSCENERY_API_URL/);
  assert.throws(() => parseProductionConfig({ GEOSCENERY_API_URL: '  ' }), /Set GEOSCENERY_API_URL/);
});

test('rejects malformed, non-HTTPS, non-origin, and reserved production API URLs', () => {
  for (const value of [
    'not a URL',
    'http://api.geoscenery-validation.net',
    'https://user:password@api.geoscenery-validation.net',
    'https://api.geoscenery-validation.net/path',
    'https://api.geoscenery-validation.net?query=1',
    'https://localhost',
    'https://192.0.2.10',
    'https://api.geoscenery.test',
    'https://api.example.org'
  ]) {
    assert.throws(() => parseProductionConfig({ GEOSCENERY_API_URL: value }), /GEOSCENERY_API_URL/);
  }
});

test('accepts a public HTTPS API origin and normalizes its default port', () => {
  assert.deepEqual(parseProductionConfig({
    GEOSCENERY_API_URL: 'https://api.geoscenery-validation.net:443/'
  }), { apiUrl: validApiUrl, platformConfigUrl: undefined });
});

test('keeps platform configuration optional and validates it when provided', () => {
  assert.deepEqual(parseProductionConfig({ GEOSCENERY_API_URL: validApiUrl }), {
    apiUrl: validApiUrl,
    platformConfigUrl: undefined
  });
  assert.deepEqual(parseProductionConfig({
    GEOSCENERY_API_URL: validApiUrl,
    GEOSCENERY_PLATFORM_CONFIG_URL: '  '
  }), {
    apiUrl: validApiUrl,
    platformConfigUrl: undefined
  });
  assert.deepEqual(parseProductionConfig({
    GEOSCENERY_API_URL: validApiUrl,
    GEOSCENERY_PLATFORM_CONFIG_URL: ' https://platform.collections-validation.net/ '
  }), {
    apiUrl: validApiUrl,
    platformConfigUrl: 'https://platform.collections-validation.net'
  });
  assert.throws(() => parseProductionConfig({
    GEOSCENERY_API_URL: validApiUrl,
    GEOSCENERY_PLATFORM_CONFIG_URL: 'http://platform.collections-validation.net'
  }), /GEOSCENERY_PLATFORM_CONFIG_URL/);
  for (const value of [
    'not a URL',
    'https://user:password@platform.collections-validation.net',
    'https://platform.collections-validation.net/path',
    'https://192.0.2.10'
  ]) {
    assert.throws(() => parseProductionConfig({
      GEOSCENERY_API_URL: validApiUrl,
      GEOSCENERY_PLATFORM_CONFIG_URL: value
    }), /GEOSCENERY_PLATFORM_CONFIG_URL/);
  }
});

test('permits only the designated reserved origins in an explicit CI build', () => {
  const config = parseProductionConfig({
    GEOSCENERY_API_URL: 'https://api.geoscenery.test',
    GEOSCENERY_PLATFORM_CONFIG_URL: 'https://platform.geoscenery.test'
  }, { allowCiTestOrigins: true });

  assert.deepEqual(config, {
    apiUrl: 'https://api.geoscenery.test',
    platformConfigUrl: 'https://platform.geoscenery.test'
  });
  assert.throws(() => parseProductionConfig({
    GEOSCENERY_API_URL: 'https://api.geoscenery.test'
  }), /GEOSCENERY_API_URL/);
  assert.throws(() => parseProductionConfig({
    GEOSCENERY_API_URL: 'https://platform.geoscenery.test'
  }, { allowCiTestOrigins: true }), /wrong designated CI fixture hostname/);
});

test('renders a frozen browser runtime config with API and optional platform origins', () => {
  const config = { apiUrl: validApiUrl, platformConfigUrl: undefined };
  const sandbox = { window: {} };
  const runtimeSource = renderRuntimeConfig(config);
  runInNewContext(runtimeSource, sandbox);

  assert.deepEqual(JSON.parse(JSON.stringify(sandbox.window.__GEOSCENERY_CONFIG__)), {
    apiUrl: validApiUrl
  });
  assert.equal(Object.isFrozen(sandbox.window.__GEOSCENERY_CONFIG__), true);
});

test('verifies built index loads the generated runtime asset before the application bundle', async t => {
  const outputPath = await mkdtemp(join(tmpdir(), 'geoscenery-production-artifact-'));
  t.after(() => rm(outputPath, { recursive: true, force: true }));
  await mkdir(join(outputPath, 'assets'));
  const config = { platformConfigUrl: 'https://platform.collections-validation.net', apiUrl: validApiUrl };
  const runtimeSource = `\nwindow . __GEOSCENERY_CONFIG__ = Object . freeze (\n  {\n    "platformConfigUrl": "${config.platformConfigUrl}",\n    "apiUrl": "${config.apiUrl}"\n  }\n);\n`;
  await writeFile(join(outputPath, 'index.html'), `
    <script type="text/javascript"
      src = './assets/app-config.runtime.js?cache=123'></script>
    <script type="module" src='./main.abc.js'></script>
  `);
  await writeFile(join(outputPath, 'assets', 'app-config.runtime.js'), runtimeSource);

  await verifyProductionArtifact(outputPath, config);
  assert.equal(await readFile(join(outputPath, 'assets', 'app-config.runtime.js'), 'utf8'), runtimeSource);
});

test('rejects artifacts that omit or load the runtime configuration after the app bundle', async t => {
  const outputPath = await mkdtemp(join(tmpdir(), 'geoscenery-invalid-artifact-'));
  t.after(() => rm(outputPath, { recursive: true, force: true }));
  await mkdir(join(outputPath, 'assets'));
  const config = { apiUrl: validApiUrl, platformConfigUrl: undefined };
  await writeFile(join(outputPath, 'index.html'), '<script src="main.abc.js"></script>');
  await writeFile(join(outputPath, 'assets', 'app-config.runtime.js'), renderRuntimeConfig(config));
  await assert.rejects(verifyProductionArtifact(outputPath, config), /must load assets\/app-config\.runtime\.js before/);

  await writeFile(join(outputPath, 'index.html'), '<script data-src="assets/app-config.runtime.js"></script><script src="main.abc.js"></script>');
  await assert.rejects(verifyProductionArtifact(outputPath, config), /must load assets\/app-config\.runtime\.js before/);

  await writeFile(join(outputPath, 'index.html'), '<script async src="assets/app-config.runtime.js"></script><script src="main.abc.js"></script>');
  await assert.rejects(verifyProductionArtifact(outputPath, config), /must load assets\/app-config\.runtime\.js before/);

  await writeFile(join(outputPath, 'index.html'), '<script src="assets/app-config.runtime.js"></script><script src="main.abc.js"></script>');
  await writeFile(join(outputPath, 'assets', 'app-config.runtime.js'), renderRuntimeConfig({ apiUrl: 'https://different-validation.net' }));
  await assert.rejects(verifyProductionArtifact(outputPath, config), /does not contain the validated production configuration/);

  await rm(join(outputPath, 'assets', 'app-config.runtime.js'));
  await assert.rejects(verifyProductionArtifact(outputPath, config));
});

test('removes stale ignored config and previous www output before rejecting invalid config', async t => {
  const clientRoot = await mkdtemp(join(tmpdir(), 'geoscenery-stale-config-'));
  t.after(() => rm(clientRoot, { recursive: true, force: true }));
  const runtimeConfigPath = join(clientRoot, 'src', 'assets', 'app-config.runtime.js');
  const previousOutputPath = join(clientRoot, 'www');
  const unrelatedFile = join(clientRoot, 'user-data.txt');
  await mkdir(join(clientRoot, 'src', 'assets'), { recursive: true });
  await mkdir(previousOutputPath);
  await writeFile(runtimeConfigPath, renderRuntimeConfig({ apiUrl: 'https://old-validation.net' }));
  await writeFile(join(previousOutputPath, 'index.html'), 'previous successful artifact');
  await writeFile(unrelatedFile, 'preserve');

  await assert.rejects(runProductionBuild({ clientRoot, env: {}, args: [] }), /Set GEOSCENERY_API_URL/);

  assert.equal(await pathExists(runtimeConfigPath), false);
  assert.equal(await pathExists(previousOutputPath), false);
  assert.equal(await readFile(unrelatedFile, 'utf8'), 'preserve');
});

test('cleans a partial www artifact and generated config after an Angular build failure', async t => {
  const clientRoot = await mkdtemp(join(tmpdir(), 'geoscenery-failed-build-'));
  t.after(() => rm(clientRoot, { recursive: true, force: true }));
  await mkdir(join(clientRoot, 'src', 'assets'), { recursive: true });

  await assert.rejects(runProductionBuild({
    clientRoot,
    env: { GEOSCENERY_API_URL: validApiUrl },
    args: [],
    buildAngular: async ({ outputPath }) => {
      await mkdir(outputPath, { recursive: true });
      await writeFile(join(outputPath, 'partial.js'), 'incomplete');
      return { status: 1 };
    }
  }), /Angular production build failed with status 1/);

  assert.equal(await pathExists(join(clientRoot, 'www')), false);
  assert.equal(await pathExists(join(clientRoot, 'src', 'assets', 'app-config.runtime.js')), false);
});

test('isolates CI fixture builds in a temporary output and discards them after verification', async t => {
  const clientRoot = await mkdtemp(join(tmpdir(), 'geoscenery-ci-build-'));
  t.after(() => rm(clientRoot, { recursive: true, force: true }));
  await mkdir(join(clientRoot, 'src', 'assets'), { recursive: true });
  let ciOutputPath;

  const result = await runProductionBuild({
    clientRoot,
    env: {
      GEOSCENERY_API_URL: 'https://api.geoscenery.test',
      GEOSCENERY_PLATFORM_CONFIG_URL: 'https://platform.geoscenery.test'
    },
    args: ['--ci'],
    buildAngular: async ({ outputPath }) => {
      ciOutputPath = outputPath;
      await mkdir(join(outputPath, 'assets'), { recursive: true });
      await writeFile(join(outputPath, 'index.html'), '<script src="/assets/app-config.runtime.js"></script><script src="/main.abc.js"></script>');
      const generatedConfig = await readFile(join(clientRoot, 'src', 'assets', 'app-config.runtime.js'), 'utf8');
      await writeFile(join(outputPath, 'assets', 'app-config.runtime.js'), generatedConfig);
      return { status: 0 };
    }
  });

  assert.equal(result.isCiBuild, true);
  assert.equal(result.config.apiUrl, 'https://api.geoscenery.test');
  assert.notEqual(resolve(ciOutputPath), resolve(clientRoot, 'www'));
  assert.equal(await pathExists(ciOutputPath), false);
  assert.equal(await pathExists(join(clientRoot, 'www')), false);
  assert.equal(await pathExists(join(clientRoot, 'src', 'assets', 'app-config.runtime.js')), false);
});

test('does not let the CI environment variable alone enable reserved fixture origins', async t => {
  const clientRoot = await mkdtemp(join(tmpdir(), 'geoscenery-ci-flag-'));
  t.after(() => rm(clientRoot, { recursive: true, force: true }));
  await mkdir(join(clientRoot, 'src', 'assets'), { recursive: true });

  await assert.rejects(runProductionBuild({
    clientRoot,
    env: { CI: 'true', GEOSCENERY_API_URL: 'https://api.geoscenery.test' },
    args: []
  }), /GEOSCENERY_API_URL/);
});
