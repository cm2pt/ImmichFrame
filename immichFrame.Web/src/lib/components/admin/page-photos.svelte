<script lang="ts">
	import type { ServerAccountSettings, ServerSettings } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import { accountDefaults, effective } from './admin-defaults';
	import { formatCount, hasConnection, libraryFor } from './admin-library.svelte';
	import AccountPhotos from './account-photos.svelte';
	import Glyph from './glyph.svelte';
	import Monogram from './monogram.svelte';
	import PageCard from './page-card.svelte';

	interface Props {
		settings: ServerSettings;
		version: number;
		onaddaccount: () => void;
	}

	let { settings, version, onaddaccount }: Props = $props();

	let accounts = $derived(settings.Accounts ?? []);
	let libraries = $derived(accounts.map((account) => libraryFor(account)));
	let selected = $state(0);

	// The count the frame's own search draws from for each account's current choices,
	// refreshed shortly after each change.
	let matching: (number | null)[] = $state([]);
	$effect(() => {
		const snapshot = JSON.stringify(accounts);
		const list: ServerAccountSettings[] = JSON.parse(snapshot);
		const timer = setTimeout(() => {
			list.forEach(async (account, i) => {
				if (!hasConnection(account)) return;
				try {
					const res = await adminApi.getAccountLibrary(account);
					matching[i] = res.status === 200 ? (res.data.matchingAssets ?? null) : null;
				} catch {
					matching[i] = null;
				}
			});
		}, 800);
		return () => clearTimeout(timer);
	});
	let total = $derived(
		accounts.length && matching.length >= accounts.length && matching.every((n) => n != null)
			? matching.reduce<number>((sum, n) => sum + (n ?? 0), 0)
			: null
	);

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

<PageCard
	title="Accounts"
	footer={total != null ? `${formatCount(total)} photos in rotation, plus memories.` : undefined}
>
	{#each accounts as account, i (i)}
		{@const name = libraries[i]?.userName || `Account ${i + 1}`}
		{@const isSelected = i === selected}
		<button
			type="button"
			class="border-sep flex min-h-14 w-full items-center gap-3 border-t py-2 text-left first:border-t-0 focus-visible:outline-2 focus-visible:outline-primary"
			aria-pressed={isSelected}
			onclick={() => (selected = i)}
		>
			<Monogram name={libraries[i]?.userName || String(i + 1)} />
			<span class="min-w-0 flex-1">
				<span class="block text-[0.9375rem] md:text-[0.875rem] {isSelected ? 'font-semibold' : ''}"
					>{name}</span
				>
				<span class="text-label-2 block truncate text-[0.8125rem] md:text-[0.75rem]"
					>{summary(account, i)}</span
				>
			</span>
			{#if matching[i] != null}
				<span
					class="text-label-2 text-[0.875rem] tabular-nums"
					title="Photos these choices match, not counting memories">{formatCount(matching[i])}</span
				>
			{/if}
			<span class={isSelected ? 'text-primary' : 'text-label-3'}>
				<Glyph name={isSelected ? 'check' : 'chevronRight'} size={16} stroke={2} />
			</span>
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

{#if accounts[selected]}
	{#key `${version}-${selected}`}
		{#if accounts.length > 1}
			<h2 class="-mb-3 px-1 text-[1.0625rem] font-semibold">
				{libraries[selected]?.userName || `Account ${selected + 1}`}
			</h2>
		{/if}
		<AccountPhotos account={accounts[selected]} />
	{/key}
{/if}
