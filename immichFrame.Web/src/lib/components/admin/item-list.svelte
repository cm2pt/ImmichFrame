<script lang="ts">
	import Btn from './btn.svelte';
	import Glyph from './glyph.svelte';
	import type { GlyphName } from './glyphs';
	import Thumbnail from './thumbnail.svelte';
	import type { ThumbnailKind } from '$lib/services/admin';

	export interface PickerItem {
		id: string;
		name: string;
		/** e.g. a photo count */
		detail?: string;
		thumb?: { kind: ThumbnailKind; id: string } | null;
	}

	interface Props {
		/** The selected ids (album/person GUIDs, or tag values). */
		selected: string[];
		/** What can be picked; null while loading or when the server couldn't list it. */
		items: PickerItem[] | null;
		loading?: boolean;
		/** Ids already used elsewhere (e.g. in Never Show), with the reason shown in the list. */
		usedElsewhere?: Map<string, string>;
		/** "Album", "Person", "Tag": used in "Add Album…" and labels. */
		noun: string;
		manualPattern?: RegExp;
		manualPlaceholder?: string;
		emptyText?: string;
		/** The saved account whose faces and album covers to show; null for an unsaved one. */
		accountIndex?: number | null;
		/** Shown beside items that have no picture (tags). */
		glyph?: GlyphName;
		onchange: (ids: string[]) => void;
	}

	let {
		selected,
		items,
		loading = false,
		usedElsewhere,
		noun,
		manualPattern,
		manualPlaceholder = 'Paste an ID',
		emptyText,
		accountIndex = null,
		glyph,
		onchange
	}: Props = $props();

	let open = $state(false);
	let query = $state('');
	let manual = $state('');
	let manualError = $state('');
	let popover: HTMLElement | undefined = $state();
	let addButton: HTMLElement | undefined = $state();

	const byId = $derived(new Map((items ?? []).map((item) => [item.id.toLowerCase(), item])));
	const matches = $derived(
		(items ?? [])
			.filter((item) => !selected.some((id) => id.toLowerCase() === item.id.toLowerCase()))
			.filter((item) => item.name.toLowerCase().includes(query.trim().toLowerCase()))
			.slice(0, 60)
	);

	function add(id: string) {
		onchange([...selected, id]);
		query = '';
	}

	// Picking from the popover commits and closes it, like a macOS popover menu.
	function pick(id: string) {
		add(id);
		close();
	}

	function remove(id: string) {
		onchange(selected.filter((s) => s !== id));
	}

	function addManual() {
		const value = manual.trim();
		if (!value) return;
		if (manualPattern && !manualPattern.test(value)) {
			manualError = 'That doesn’t look like a valid ID.';
			return;
		}
		manualError = '';
		manual = '';
		if (!selected.includes(value)) pick(value);
	}

	function close() {
		open = false;
		addButton?.focus();
	}

	// A popover: Escape or a click outside closes it.
	$effect(() => {
		if (!open) return;
		const onDown = (e: MouseEvent) => {
			if (popover && !popover.contains(e.target as Node) && !addButton?.contains(e.target as Node))
				open = false;
		};
		const onKey = (e: KeyboardEvent) => e.key === 'Escape' && close();
		document.addEventListener('mousedown', onDown);
		document.addEventListener('keydown', onKey);
		return () => {
			document.removeEventListener('mousedown', onDown);
			document.removeEventListener('keydown', onKey);
		};
	});

	function focusOnMount(node: HTMLInputElement) {
		node.focus();
	}
</script>

<!-- The selected items as rows, then an "Add …" row that opens a searchable popover. -->
{#each selected as id (id)}
	{@const item = byId.get(id.toLowerCase())}
	<div
		class="border-sep flex min-h-11 items-center gap-3 border-t py-2 first:border-t-0"
		title={id}
	>
		<Thumbnail name={item?.name ?? '?'} {accountIndex} thumb={item?.thumb} {glyph} />
		<span class="min-w-0 flex-1 truncate text-[0.9375rem] md:text-[0.875rem]">
			{#if loading && !item}
				<span class="bg-fill inline-block h-3 w-28 animate-pulse rounded" aria-label="Loading"
				></span>
			{:else}
				{item?.name ?? (items ? 'Not found in Immich' : id)}
			{/if}
		</span>
		{#if item?.detail}<span class="text-label-2 text-[0.8125rem] tabular-nums">{item.detail}</span
			>{/if}
		<button
			type="button"
			class="text-label-3 -mr-1 rounded-full p-1 hover:text-[#d70015] focus-visible:outline-2 focus-visible:outline-primary dark:hover:text-[#ff6961]"
			aria-label="Remove {item?.name ?? id}"
			onclick={() => remove(id)}
		>
			<Glyph name="circleMinus" size={18} />
		</button>
	</div>
{/each}

<div class="border-sep relative border-t first:border-t-0 {selected.length ? '' : ''}">
	<button
		bind:this={addButton}
		type="button"
		class="flex min-h-11 w-full items-center py-2 text-left text-[0.9375rem] text-primary focus-visible:outline-2 focus-visible:outline-primary md:text-[0.875rem]"
		aria-expanded={open}
		aria-haspopup="dialog"
		onclick={() => (open ? close() : (open = true))}
	>
		Add {noun}…
	</button>
	{#if !selected.length && emptyText}
		<p class="text-label-2 -mt-2 pb-2.5 text-[0.75rem]">{emptyText}</p>
	{/if}

	{#if open}
		<div
			bind:this={popover}
			class="absolute top-full left-0 z-30 mt-1.5 w-[min(22rem,calc(100vw-3rem))] rounded-[10px] bg-white/85 p-2 shadow-[0_10px_30px_rgb(0_0_0/0.18),0_0_0_0.5px_rgb(0_0_0/0.12)] backdrop-blur-[30px] backdrop-saturate-[180%] dark:bg-[rgb(44_44_46/0.85)] dark:shadow-[0_10px_30px_rgb(0_0_0/0.5),0_0_0_0.5px_rgb(255_255_255/0.12)]"
			role="dialog"
			aria-label="Add {noun.toLowerCase()}"
		>
			<!-- The popover's arrow, pointing at "Add …". -->
			<span
				aria-hidden="true"
				class="absolute -top-[6px] left-6 h-3 w-3 rotate-45 bg-white/85 shadow-[-0.5px_-0.5px_0_rgb(0_0_0/0.12)] dark:bg-[rgb(44_44_46/0.85)]"
			></span>
			{#if loading}
				<p class="text-label-2 px-2 py-3 text-[0.8125rem]">Loading from Immich…</p>
			{:else if items}
				<div class="bg-fill rounded-control flex items-center gap-1.5 px-2.5 py-1.5">
					<Glyph name="search" size={15} class="text-label-2" />
					<input
						use:focusOnMount
						bind:value={query}
						placeholder="Search"
						aria-label="Search {noun.toLowerCase()}s"
						class="w-full bg-transparent text-[0.875rem] outline-none placeholder:text-[#6e6e73] dark:placeholder:text-[#a1a1a6]"
					/>
				</div>
				<ul class="mt-1.5 max-h-72 overflow-y-auto">
					{#each matches as item (item.id)}
						{@const elsewhere = usedElsewhere?.get(item.id.toLowerCase())}
						<li>
							<button
								type="button"
								class="flex w-full items-center justify-between gap-3 rounded-md px-2.5 py-1.5 text-left text-[0.875rem] hover:bg-black/5 disabled:cursor-not-allowed disabled:opacity-45 disabled:hover:bg-transparent dark:hover:bg-white/10"
								disabled={!!elsewhere}
								onclick={() => pick(item.id)}
							>
								<span class="flex min-w-0 items-center gap-2.5">
									<Thumbnail name={item.name} {accountIndex} thumb={item.thumb} {glyph} size={24} />
									<span class="min-w-0 truncate">{item.name}</span>
								</span>
								<span class="text-label-2 shrink-0 text-[0.75rem] tabular-nums"
									>{elsewhere ?? item.detail ?? ''}</span
								>
							</button>
						</li>
					{:else}
						<li class="text-label-2 px-2.5 py-2 text-[0.8125rem]">No matches</li>
					{/each}
				</ul>
			{:else}
				<p class="text-label-2 px-1 pb-2 text-[0.8125rem]">
					Immich's list isn't available, so paste the ID (it's in the address bar when you open it
					in Immich).
				</p>
				<form
					class="flex gap-2"
					onsubmit={(e) => {
						e.preventDefault();
						addManual();
					}}
				>
					<input
						use:focusOnMount
						bind:value={manual}
						placeholder={manualPlaceholder}
						aria-label={manualPlaceholder}
						class="bg-fill rounded-control w-full px-2.5 py-1.5 text-[0.875rem] outline-none"
					/>
					<Btn type="submit" size="small">Add</Btn>
				</form>
				{#if manualError}<p class="mt-1 px-1 text-[0.75rem] text-[#d70015] dark:text-[#ff6961]">
						{manualError}
					</p>{/if}
			{/if}
		</div>
	{/if}
</div>
