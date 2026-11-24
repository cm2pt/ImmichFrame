import { get } from 'svelte/store';
import * as api from '$lib/immichFrameApi';
import type { ServerAccountSettings, ServerSettings } from '$lib/immichFrameApi';
import { adminPasswordStore } from '$lib/stores/admin.store';

// Every admin endpoint authenticates with the dedicated admin password rather
// than the client auth secret that $lib/index installs globally, so these calls
// carry their own header instead of going through the shared defaults.
function authOpts() {
	return { headers: { Authorization: 'Bearer ' + get(adminPasswordStore) } };
}

export function getStatus() {
	return api.getAdminStatus();
}

// Anonymous on purpose — this is the only way into a fresh install. The server
// refuses it once an admin password exists.
export function setup(adminPassword: string) {
	return api.setupAdmin({ adminPassword });
}

export function getSettings() {
	return api.getAdminSettings(authOpts());
}

export function updateSettings(settings: ServerSettings) {
	return api.updateAdminSettings(clean(settings), authOpts());
}

export function testAccount(account: ServerAccountSettings) {
	return api.testAccount(account, authOpts());
}

export function getAccountLibrary(account: ServerAccountSettings) {
	return api.getAccountLibrary(account, authOpts());
}

/** The place name the frame will show for a weather location. */
export function getWeatherPlace(request: {
	apiKey: string;
	latLong: string;
	language?: string | null;
}) {
	return api.getWeatherPlace(request, authOpts());
}

/** How many photos these (possibly unsaved) settings draw from, memories aside. */
export function countMatchingAssets(account: ServerAccountSettings) {
	return api.countMatchingAssets(account, authOpts());
}

export function getScreens() {
	return api.getScreens(authOpts());
}

export function renameScreen(clientIdentifier: string, name: string | null) {
	return api.renameScreen(clientIdentifier, { name }, authOpts());
}

export function forgetScreen(clientIdentifier: string) {
	return api.forgetScreen(clientIdentifier, authOpts());
}

export function getRejectedScreens() {
	return api.getRejectedScreens(authOpts());
}

export function dismissRejectedScreen(clientIdentifier: string) {
	return api.dismissRejectedScreen(clientIdentifier, authOpts());
}

export function getSettingsHistory() {
	return api.getSettingsHistory(authOpts());
}

export function restoreSettingsVersion(version: number) {
	return api.restoreSettingsVersion(version, authOpts());
}

export function getAdminInfo() {
	return api.getAdminInfo(authOpts());
}

export function getPreview(count: number) {
	return api.getAdminPreview({ count }, authOpts());
}

/** The preview image as an object URL; the caller revokes it. */
export async function getPreviewImageUrl(id: string): Promise<string | null> {
	const res = await fetch(`/api/Admin/Preview/${encodeURIComponent(id)}/Image`, authOpts());
	return res.ok ? URL.createObjectURL(await res.blob()) : null;
}

// Thumbnails (faces, album covers) for the pickers. Each is fetched once per page load, at
// most six at a time so a long people list doesn't flood Immich, and kept as an object URL.
const thumbnails = new Map<string, Promise<string | null>>();
const waiting: (() => void)[] = [];
let inFlight = 0;

async function inTurn<T>(task: () => Promise<T>): Promise<T> {
	if (inFlight >= 6) await new Promise<void>((resolve) => waiting.push(resolve));
	inFlight++;
	try {
		return await task();
	} finally {
		inFlight--;
		waiting.shift()?.();
	}
}

export type ThumbnailKind = 'person' | 'asset';

function cachedImage(path: string): Promise<string | null> {
	let url = thumbnails.get(path);
	if (!url) {
		url = inTurn(async () => {
			const res = await fetch(path, authOpts());
			return res.ok ? URL.createObjectURL(await res.blob()) : null;
		}).catch(() => null);
		thumbnails.set(path, url);
	}
	return url;
}

/** A saved account's Immich profile picture as an object URL, or null when there's none. */
export function getAvatarUrl(accountIndex: number): Promise<string | null> {
	return cachedImage(`/api/Admin/Accounts/${accountIndex}/Avatar`);
}

/** A saved account's face or asset thumbnail as an object URL, or null when Immich has none. */
export function getThumbnailUrl(
	accountIndex: number,
	kind: ThumbnailKind,
	id: string
): Promise<string | null> {
	return cachedImage(
		`/api/Admin/Accounts/${accountIndex}/${kind === 'person' ? 'People' : 'Assets'}/${encodeURIComponent(id)}/Thumbnail`
	);
}

// Drop empty values so the server falls back to its defaults instead of
// receiving empty strings / nulls for unset fields. The PUT replaces the whole
// document, so an omitted key deserialises to the C# default.
function clean(settings: ServerSettings): ServerSettings {
	return {
		General: dropEmpty(settings.General ?? {}),
		Accounts: (settings.Accounts ?? []).map((account) => dropEmpty(account))
	};
}

function dropEmpty<T extends Record<string, unknown>>(obj: T): T {
	const result: Record<string, unknown> = {};
	for (const [key, value] of Object.entries(obj)) {
		if (value === null || value === undefined || value === '') continue;
		result[key] = value;
	}
	return result as T;
}
