<script lang="ts">
	import type { ServerAccountSettings } from '$lib/immichFrameApi';
	import type { AccountLibrary } from './admin-library.svelte';
	import type { GlyphName } from './glyphs';
	import ItemPicker, { type PickerItem } from './item-picker.svelte';
	import ItemRow from './item-row.svelte';

	interface Props {
		account: ServerAccountSettings;
		library: AccountLibrary;
		loading: boolean;
		accountIndex?: number | null;
		/** What the Include lists already show, so the picker can mark it. */
		shown: { albums: Map<string, string>; people: Map<string, string>; tags: Map<string, string> };
	}

	let { account, library, loading, accountIndex = null, shown }: Props = $props();

	const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

	type Kind = 'album' | 'person' | 'tag';
	const kinds: {
		kind: Kind;
		noun: string;
		key: 'excludedAlbums' | 'excludedPeople' | 'excludedTags';
		glyph?: GlyphName;
	}[] = [
		{ kind: 'album', noun: 'Album', key: 'excludedAlbums' },
		{ kind: 'person', noun: 'Person', key: 'excludedPeople' },
		{ kind: 'tag', noun: 'Tag', key: 'excludedTags', glyph: 'tag' }
	];
	const itemsFor = (kind: Kind): PickerItem[] | null =>
		kind === 'album' ? library.albums : kind === 'person' ? library.people : library.tags;
	const shownFor = (kind: Kind) =>
		kind === 'album' ? shown.albums : kind === 'person' ? shown.people : shown.tags;
	const ids = (key: (typeof kinds)[number]['key']) => (account[key] ?? []) as string[];

	let menuOpen = $state(false);
	let picking: (typeof kinds)[number] | null = $state(null);
	let addButton: HTMLButtonElement | undefined = $state();
	let menu: HTMLElement | undefined = $state();

	function close() {
		menuOpen = false;
		picking = null;
		addButton?.focus();
	}

	// The menu: arrows move, Return picks, Escape or a click outside closes.
	$effect(() => {
		if (!menuOpen) return;
		requestAnimationFrame(() => menu?.querySelector<HTMLElement>('[role=menuitem]')?.focus());
		const onDown = (e: MouseEvent) => {
			if (!menu?.contains(e.target as Node) && !addButton?.contains(e.target as Node))
				menuOpen = false;
		};
		document.addEventListener('mousedown', onDown);
		return () => document.removeEventListener('mousedown', onDown);
	});
	function onMenuKey(e: KeyboardEvent) {
		const items = [...(menu?.querySelectorAll<HTMLElement>('[role=menuitem]') ?? [])];
		const at = items.indexOf(document.activeElement as HTMLElement);
		if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
			e.preventDefault();
			items[(at + (e.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length]?.focus();
		} else if (e.key === 'Escape') {
			e.preventDefault();
			close();
		}
	}
</script>

<!-- Everything hidden first (albums, people, tags), then one "Add…" with a menu. -->
{#each kinds as k (k.kind)}
	{#each ids(k.key) as id (id)}
		<ItemRow
			{id}
			item={itemsFor(k.kind)?.find((i) => i.id.toLowerCase() === id.toLowerCase())}
			{loading}
			listed={!!itemsFor(k.kind)}
			{accountIndex}
			glyph={k.glyph}
			onremove={() => (account[k.key] = ids(k.key).filter((s) => s !== id))}
		/>
	{/each}
{/each}

<div class="border-sep relative border-t first:border-t-0">
	<button
		bind:this={addButton}
		type="button"
		class="-mx-1.5 flex min-h-11 w-[calc(100%+0.75rem)] items-center rounded-md px-1.5 py-2 text-left text-[0.9375rem] text-primary focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary md:text-[0.875rem]"
		aria-haspopup="menu"
		aria-expanded={menuOpen}
		onclick={() => (menuOpen = !menuOpen)}
	>
		Add…
	</button>
	{#if menuOpen}
		<div
			bind:this={menu}
			role="menu"
			tabindex="-1"
			aria-label="Never show"
			onkeydown={onMenuKey}
			class="absolute top-full left-0 z-30 mt-1 min-w-44 if-material rounded-[8px] bg-white/95 p-1 shadow-[0_10px_30px_rgb(0_0_0/0.18),0_0_0_0.5px_rgb(0_0_0/0.12)] backdrop-blur-[30px] backdrop-saturate-[180%] dark:bg-[rgb(44_44_46/0.95)] dark:shadow-[0_10px_30px_rgb(0_0_0/0.5),0_0_0_0.5px_rgb(255_255_255/0.12)]"
		>
			{#each kinds as k (k.kind)}
				<button
					type="button"
					role="menuitem"
					class="hover:bg-[var(--if-accent-fill)] focus:bg-[var(--if-accent-fill)] block w-full rounded-[5px] px-2.5 py-1 text-left text-[0.875rem] outline-none hover:text-white focus:text-white"
					onclick={() => {
						menuOpen = false;
						picking = k;
					}}>{k.noun}…</button
				>
			{/each}
		</div>
	{/if}
</div>

{#if picking && addButton}
	<ItemPicker
		noun={picking.noun}
		items={itemsFor(picking.kind)}
		{loading}
		selected={ids(picking.key)}
		usedElsewhere={shownFor(picking.kind)}
		manualPattern={picking.kind === 'tag' ? undefined : guid}
		manualPlaceholder={picking.kind === 'tag' ? 'Tag, e.g. Screenshots' : undefined}
		{accountIndex}
		glyph={picking.glyph}
		anchor={addButton}
		onpick={(id) => {
			const key = picking!.key;
			account[key] = [...ids(key), id];
			close();
		}}
		onclose={close}
	/>
{/if}
