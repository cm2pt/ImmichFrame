<script lang="ts">
	import { untrack } from 'svelte';
	import type { ServerAccountSettings, ServerSettings } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import { accountDefaults, effective } from './admin-defaults';
	import {
		accountLabel,
		formatCount,
		hasConnection,
		libraryFor,
		monogramOf
	} from './admin-library.svelte';
	import AccountPhotos from './account-photos.svelte';
	import Glyph from './glyph.svelte';
	import Monogram from './monogram.svelte';
	import PageCard from './page-card.svelte';

	interface Props {
		settings: ServerSettings;
		version: number;
		onaddaccount: () => void;
		/** The account drilled into, or null for the list. The shell titles the page with it. */
		selected?: number | null;
	}

	let { settings, version, onaddaccount, selected = $bindable(null) }: Props = $props();

	let accounts = $derived(settings.Accounts ?? []);
	let libraries = $derived(accounts.map((account) => libraryFor(account)));
	// Several accounts: a list that drills into one, like a list in System Settings. A single
	// account has nothing to choose from, so its settings are the page itself, with no extra level.
	let single = $derived(accounts.length === 1);

	// The count the frame's own search draws from for each account's current choices,
	// refreshed shortly after each change.
	let matching: (number | null)[] = $state([]);
	$effect(() => {
		const snapshot = JSON.stringify(accounts);
		const list: ServerAccountSettings[] = JSON.parse(snapshot);
		// Only this run may write the counts: an older, slower answer must not overwrite a newer
		// one. Removed or disconnected accounts lose their old number at once.
		let current = true;
		const previous = untrack(() => matching);
		matching = list.map((account, i) => (hasConnection(account) ? (previous[i] ?? null) : null));
		const timer = setTimeout(() => {
			list.forEach(async (account, i) => {
				if (!hasConnection(account)) return;
				let count: number | null = null;
				try {
					const res = await adminApi.countMatchingAssets(account);
					count = res.status === 200 ? (res.data.count ?? null) : null;
				} catch {
					count = null;
				}
				if (current) matching[i] = count;
			});
		}, 800);
		return () => {
			current = false;
			clearTimeout(timer);
		};
	});
	// Search can jump to an account's setting: open the first account if none is open.
	$effect(() => {
		const reveal = (e: Event) => {
			const title = (e as CustomEvent<string>).detail;
			if (selected === null && accounts.length > 1 && !['Accounts', 'Up next'].includes(title))
				selected = 0;
		};
		window.addEventListener('if-reveal', reveal);
		return () => window.removeEventListener('if-reveal', reveal);
	});

	let total = $derived(
		accounts.length && matching.length >= accounts.length && matching.every((n) => n != null)
			? matching.reduce<number>((sum, n) => sum + (n ?? 0), 0)
			: null
	);

	const memoriesOn = (account: ServerAccountSettings) =>
		!!effective(account, accountDefaults, 'showMemories');
	const inRotation = (n: number, memories: boolean) =>
		`${formatCount(n)} ${n === 1 ? 'photo' : 'photos'} in rotation${memories ? ', plus memories' : ''}.`;

	function summary(account: ServerAccountSettings, i: number) {
		const library = libraries[i];
		if (!hasConnection(account)) return 'Not connected. Add it under Access & Accounts.';
		if (library?.state === 'error' && !library.albums)
			return library.message ?? 'Can’t reach Immich.';
		const get = <K extends keyof ServerAccountSettings>(k: K) =>
			effective(account, accountDefaults, k);
		const parts = [
			get('showFavorites') && 'Favorites',
			get('showMemories') && 'Memories',
			(account.albums?.length ?? 0) > 0 &&
				`${account.albums!.length} album${account.albums!.length === 1 ? '' : 's'}`,
			(account.people?.length ?? 0) > 0 &&
				`${account.people!.length} ${account.people!.length === 1 ? 'person' : 'people'}`
		].filter(Boolean);
		return parts.length ? parts.join(', ') : 'The whole library';
	}
</script>

{#if !single && (selected === null || !accounts[selected])}
	<PageCard
		title="Accounts"
		footer={total != null ? inRotation(total, accounts.some(memoriesOn)) : undefined}
	>
		{#each accounts as account, i (i)}
			<button
				type="button"
				class="border-sep flex min-h-14 w-full items-center gap-3 border-t py-2 text-left first:border-t-0 focus-visible:outline-2 focus-visible:outline-primary"
				onclick={() => (selected = i)}
			>
				<Monogram name={monogramOf(accountLabel(account, i, libraries[i]))} avatarOf={i} />
				<span class="min-w-0 flex-1">
					<span class="block text-[0.9375rem] md:text-[0.875rem]"
						>{accountLabel(account, i, libraries[i])}</span
					>
					<span class="text-label-2 block truncate text-[0.8125rem] md:text-[0.75rem]"
						>{summary(account, i)}</span
					>
				</span>
				{#if matching[i] != null}
					<span
						class="text-label-2 text-[0.875rem] tabular-nums"
						title="Photos these choices match, not counting memories"
						>{formatCount(matching[i])}</span
					>
				{/if}
				<span class="text-label-3"><Glyph name="chevronRight" size={16} stroke={2} /></span>
			</button>
		{/each}
		<button
			type="button"
			class="border-sep flex min-h-11 w-full items-center border-t py-2 text-left text-[0.9375rem] text-primary first:border-t-0 md:text-[0.875rem]"
			onclick={onaddaccount}
		>
			Add Immich Account…
		</button>
	</PageCard>
{:else}
	{@const i = single ? 0 : selected!}
	<!-- Drilled in, the shell titles the page with the account's name and puts "‹ Photos" above
	     it; a single account sits under the Photos title. -->
	<div class="flex items-center gap-3 {single ? '' : '-mt-3'}">
		<Monogram
			name={monogramOf(accountLabel(accounts[i], i, libraries[i]))}
			size={36}
			avatarOf={i}
		/>
		<div class="min-w-0">
			{#if single}
				<p class="text-[0.9375rem] font-medium">{accountLabel(accounts[i], i, libraries[i])}</p>
			{/if}
			<p class="text-label-2 {single ? 'text-[0.8125rem]' : 'text-[0.9375rem]'}" aria-live="polite">
				{matching[i] != null
					? inRotation(matching[i]!, memoriesOn(accounts[i]))
					: summary(accounts[i], i)}
			</p>
		</div>
	</div>
	{#key `${version}-${i}`}
		<AccountPhotos account={accounts[i]} accountIndex={i} />
	{/key}
{/if}
