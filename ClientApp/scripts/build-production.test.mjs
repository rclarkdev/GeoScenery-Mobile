import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { runInNewContext } from 'node:vm';
import test from 'node:test';
import {
  parseProductionConfig,
  renderRuntimeConfig,
  verifyProductionArtifact
} from './build-production.mjs';

const validApiUrl = 'https://api.geoscenery-validation.net';

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
  const config = { apiUrl: validApiUrl, platformConfigUrl: undefined };
  const runtimeSource = renderRuntimeConfig(config);
  await writeFile(join(outputPath, 'index.html'), '<script src="assets/app-config.runtime.js"></script><script src="main.abc.js"></script>');
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

  await writeFile(join(outputPath, 'index.html'), '<script src="assets/app-config.runtime.js"></script><script src="main.abc.js"></script>');
  await rm(join(outputPath, 'assets', 'app-config.runtime.js'));
  await assert.rejects(verifyProductionArtifact(outputPath, config));
});
