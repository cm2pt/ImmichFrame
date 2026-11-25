// place files you want to import through the `$lib` alias in this folder.
import { defaults, type AssetTypeEnum } from './immichFrameApi.js';
import { authSecretStore } from '$lib/stores/persist.store';
import { get } from 'svelte/store';

export * from './immichFrameApi.js';

let isAuthListenerRegistered = false;

export const init = () => {
	setBearer();
	sendAuthSecretToServiceWorker();
};

const sendMessage = () => {
	if (navigator.serviceWorker.controller) {
		navigator.serviceWorker.controller.postMessage({
			type: 'SET_AUTH_SECRET',
			authSecret: get(authSecretStore)
		});
	}
};

export const sendAuthSecretToServiceWorker = () => {
	if (typeof navigator === 'undefined' || !('serviceWorker' in navigator)) return;

	// Send immediately if controller is ready
	sendMessage();

	// Also send when service worker becomes ready (for initial page load)
	navigator.serviceWorker.ready.then(sendMessage);

	// Listen for auth secret requests from service worker (register only once)
	if (!isAuthListenerRegistered) {
		isAuthListenerRegistered = true;
		navigator.serviceWorker.addEventListener('message', (event) => {
			if (event.data && event.data.type === 'REQUEST_AUTH_SECRET') {
				sendMessage();
			}
		});
	}
};

export const getBaseUrl = () => defaults.baseUrl;

export const setBaseUrl = (baseUrl: string) => {
	defaults.baseUrl = baseUrl;
};

export const setBearer = () => {
	defaults.headers = defaults.headers || {};
	const secret = get(authSecretStore);
	// No secret saved: send no header rather than "Bearer null", so the admin page can tell
	// a screen that has no secret from one with a wrong one.
	if (secret) defaults.headers['Authorization'] = 'Bearer ' + secret;
	else delete defaults.headers['Authorization'];
};

export const getAssetStreamUrl = (
	id: string,
	clientIdentifier?: string,
	assetType?: AssetTypeEnum
) => {
	const params = new URLSearchParams();
	if (clientIdentifier) params.set('clientIdentifier', clientIdentifier);
	if (assetType !== undefined) params.set('assetType', String(assetType));
	const query = params.toString();
	return `/api/Asset/${encodeURIComponent(id)}/Asset${query ? '?' + query : ''}`;
};
