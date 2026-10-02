declare global {
  interface Window {
    __GEOSCENERY_CONFIG__?: {
      apiUrl?: string;
    };
  }
}

const apiUrl = window.__GEOSCENERY_CONFIG__?.apiUrl;
if (!apiUrl) {
  throw new Error('Production configuration is missing. Provide window.__GEOSCENERY_CONFIG__.apiUrl before loading the app.');
}

const parsedApiUrl = new URL(apiUrl);
if (parsedApiUrl.protocol !== 'https:'
  || parsedApiUrl.hostname === 'localhost'
  || parsedApiUrl.hostname === '127.0.0.1'
  || parsedApiUrl.hostname === '10.0.2.2'
  || parsedApiUrl.hostname.endsWith('.example')
  || parsedApiUrl.hostname.endsWith('.example.com')
  || parsedApiUrl.hostname.endsWith('.test')
  || parsedApiUrl.hostname.endsWith('.invalid')) {
  throw new Error('Production API configuration must use a real HTTPS hostname.');
}

export const environment = {
  production: true,
  apiUrl: parsedApiUrl.origin
};
