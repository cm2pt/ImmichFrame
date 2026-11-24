import type { ServerAccountSettings } from '$lib/immichFrameApi';
import * as adminApi from '$lib/services/admin';

export interface LibraryItem {
	id: string;
	name: string;
	detail?: string;
	/** What to show beside the name: a face, an album cover, or a glyph (tags). */
	thumb?: { kind: adminApi.ThumbnailKind; id: string } | null;
}

export interface AccountLibrary {
	state: 'loading' | 'ready' | 'error';
	message?: string;
	userName?: string | null;
	totalAssets?: number | null;
	favoriteCount?: number | null;
	albums: LibraryItem[] | null;
	people: LibraryItem[] | null;
	tags: LibraryItem[] | null;
}

const numberFormat = new Intl.NumberFormat();
export const formatCount = (n: number | null | undefined) =>
	n === null || n === undefined ? '' : numberFormat.format(n);

// One request per connection (url + key), shared by every card that shows it.
// A lookup, not state: each entry is itself reactive.
// eslint-disable-next-line svelte/prefer-svelte-reactivity
const cache = new Map<string, AccountLibrary>();

const connectionKey = (account: ServerAccountSettings) =>
	JSON.stringify([account.immichServerUrl ?? '', account.apiKey ?? '', account.apiKeyFile ?? '']);

export function hasConnection(account: ServerAccountSettings) {
	return !!account.immichServerUrl && !!(account.apiKey || account.apiKeyFile);
}

export function libraryFor(account: ServerAccountSettings, refresh = false): AccountLibrary {
	const key = connectionKey(account);
	const cached = cache.get(key);
	if (cached && !refresh && cached.state !== 'error') return cached;

	const library: AccountLibrary = $state({
		state: 'loading',
		albums: null,
		people: null,
		tags: null
	});
	cache.set(key, library);

	if (!hasConnection(account)) {
		library.state = 'error';
		library.message = 'Add the server address and an API key first.';
		return library;
	}

	adminApi
		.getAccountLibrary({
			immichServerUrl: account.immichServerUrl,
			apiKey: account.apiKey,
			apiKeyFile: account.apiKeyFile
		})
		.then((res) => {
			if (res.status !== 200) throw new Error('request failed');
			const data = res.data;
			library.userName = data.userName;
			library.totalAssets = data.totalAssets;
			library.favoriteCount = data.favoriteCount;
			library.albums = data.albums
				? data.albums.map((a) => ({
						id: a.id!,
						name: a.name ?? '',
						detail: formatCount(a.assetCount),
						thumb: a.thumbnailAssetId ? { kind: 'asset' as const, id: a.thumbnailAssetId } : null
					}))
				: null;
			library.people = data.people
				? data.people.map((p) => ({
						id: p.id!,
						name: p.name ?? '',
						thumb: { kind: 'person' as const, id: p.id! }
					}))
				: null;
			library.tags = data.tags
				? data.tags.map((t) => ({ id: t.name ?? '', name: t.name ?? '' }))
				: null;
			library.message = data.message ?? undefined;
			// A partial answer is still useful: show what came back.
			const partial = !!(data.albums?.length || data.people?.length || data.tags?.length);
			library.state = data.success || partial ? 'ready' : 'error';
		})
		.catch(() => {
			library.state = 'error';
			library.message = 'Could not reach the ImmichFrame server.';
		});

	return library;
}

/** What an account is called: the name given here, else its Immich user, else "Account N". */
export function accountLabel(
	account: ServerAccountSettings,
	index: number,
	library?: AccountLibrary
) {
	return account.name?.trim() || library?.userName || `Account ${index + 1}`;
}

/** What the monogram spells: the label, or just the number for an unnamed "Account N". */
export const monogramOf = (label: string) => (/^Account \d+$/.test(label) ? label.slice(8) : label);

/** The cached library for an account, without starting a request (for labels while typing). */
export function peekLibrary(account: ServerAccountSettings): AccountLibrary | undefined {
	return cache.get(connectionKey(account));
}
