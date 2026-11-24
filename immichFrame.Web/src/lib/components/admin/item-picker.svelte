<script lang="ts">
	import { onMount, tick } from 'svelte';
	import { fade, fly } from 'svelte/transition';
	import { cubicOut } from 'svelte/easing';
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
		/** "Album", "Person", "Tag". */
		noun: string;
		/** What can be picked; null when the server couldn't list it (then an ID is pasted). */
		items: PickerItem[] | null;
		loading?: boolean;
		/** Already chosen here: left out of the results. */
		selected: string[];
		/** Chosen elsewhere (e.g. in Never show): listed but disabled, with the reason. */
		usedElsewhere?: Map<string, string>;
		manualPattern?: RegExp;
		manualPlaceholder?: string;
		accountIndex?: number | null;
		glyph?: GlyphName;
		/** The row that opened the picker: the popover points at it. */
		anchor: HTMLElement;
		onpick: (id: string) => void;
		onclose: () => void;
	}

	let {
		noun,
		items,
		loading = false,
		selected,
		usedElsewhere,
		manualPattern,
		manualPlaceholder = 'Paste an ID',
		accountIndex = null,
		glyph,
		anchor,
		onpick,
		onclose
	}: Props = $props();

	let query = $state('');
	let manual = $state('');
	let manualError = $state('');
	let active = $state(0);
	let panel: HTMLElement | undefined = $state();
	let list: HTMLElement | undefined = $state();

	const matches = $derived(
		(items ?? [])
			.filter((item) => !selected.some((id) => id.toLowerCase() === item.id.toLowerCase()))
			.filter((item) => item.name.toLowerCase().includes(query.trim().toLowerCase()))
			.slice(0, 80)
	);
	const pickable = (item: PickerItem) => !usedElsewhere?.has(item.id.toLowerCase());

	// A phone gets an iOS sheet from the bottom; a computer a popover that points at the row,
	// opening upward when there isn't room below (the save bar counts as taken).
	const phone = typeof window !== 'undefined' && window.matchMedia('(max-width: 47.99rem)').matches;
	let place = $state({ left: 0, top: 0, up: false, maxList: 288 });
	function measure() {
		const r = anchor.getBoundingClientRect();
		const bottomReserve = 96;
		const below = window.innerHeight - r.bottom - bottomReserve;
		const above = r.top - 16;
		const up = below < 320 && above > below;
		const room = (up ? above : below) - 70;
		place = {
			left: Math.max(12, Math.min(r.left, window.innerWidth - 352 - 12)),
			top: up ? r.top - 6 : r.bottom + 6,
			up,
			maxList: Math.max(120, Math.min(288, room))
		};
	}

	onMount(() => {
		if (!phone) measure();
		const onKey = (e: KeyboardEvent) => {
			if (e.key === 'Escape') {
				e.preventDefault();
				onclose();
			}
		};
		const onDown = (e: MouseEvent) => {
			if (
				!phone &&
				panel &&
				!panel.contains(e.target as Node) &&
				!anchor.contains(e.target as Node)
			)
				onclose();
		};
		const onMove = () => !phone && measure();
		document.addEventListener('keydown', onKey);
		document.addEventListener('mousedown', onDown);
		window.addEventListener('resize', onMove);
		window.addEventListener('scroll', onMove, true);
		return () => {
			document.removeEventListener('keydown', onKey);
			document.removeEventListener('mousedown', onDown);
			window.removeEventListener('resize', onMove);
			window.removeEventListener('scroll', onMove, true);
		};
	});

	// Keep the highlighted result on a pickable row, and in view.
	$effect(() => {
		void query;
		active = Math.max(0, matches.findIndex(pickable));
	});
	async function move(step: number) {
		if (!matches.length) return;
		let next = active;
		for (let i = 0; i < matches.length; i++) {
			next = (next + step + matches.length) % matches.length;
			if (pickable(matches[next])) break;
		}
		active = next;
		await tick();
		list
			?.querySelector<HTMLElement>(`[data-index="${active}"]`)
			?.scrollIntoView({ block: 'nearest' });
	}
	function onSearchKey(e: KeyboardEvent) {
		if (e.key === 'ArrowDown') {
			e.preventDefault();
			move(1);
		} else if (e.key === 'ArrowUp') {
			e.preventDefault();
			move(-1);
		} else if (e.key === 'Enter') {
			e.preventDefault();
			const item = matches[active];
			if (item && pickable(item)) onpick(item.id);
		}
	}

	function addManual() {
		const value = manual.trim();
		if (!value) return;
		if (manualPattern && !manualPattern.test(value)) {
			manualError = 'That doesn’t look like a valid ID.';
			return;
		}
		manualError = '';
		if (!selected.includes(value)) onpick(value);
	}

	// At the end of <body>, so no parent can clip it, and with the admin's styles.
	function portal(node: HTMLElement) {
		document.body.appendChild(node);
		return { destroy: () => node.remove() };
	}
	function focusOnMount(node: HTMLInputElement) {
		// Not on a phone: the keyboard would cover half the sheet before anything is chosen.
		// A frame later, once the portal has moved the panel (moving a node drops its focus).
		if (!phone) requestAnimationFrame(() => node.focus());
	}

	const material =
		'if-material bg-white/95 backdrop-blur-[30px] backdrop-saturate-[180%] dark:bg-[rgb(44_44_46/0.95)]';
</script>

{#snippet body()}
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
				aria-controls="picker-results"
				aria-activedescendant={matches[active] ? `picker-option-${active}` : undefined}
				onkeydown={onSearchKey}
				class="w-full bg-transparent text-[1rem] outline-none placeholder:text-[#6e6e73] md:text-[0.875rem] dark:placeholder:text-[#a1a1a6]"
			/>
		</div>
		<ul
			bind:this={list}
			id="picker-results"
			role="listbox"
			aria-label="{noun}s"
			class="mt-1.5 overflow-y-auto overscroll-contain {phone ? 'flex-1' : ''}"
			style={phone ? '' : `max-height:${place.maxList}px`}
		>
			{#each matches as item, i (item.id)}
				{@const elsewhere = usedElsewhere?.get(item.id.toLowerCase())}
				<li
					id="picker-option-{i}"
					role="option"
					aria-selected={i === active}
					aria-disabled={!!elsewhere}
					data-index={i}
				>
					<button
						type="button"
						tabindex="-1"
						class="flex w-full items-center justify-between gap-3 rounded-md px-2.5 text-left disabled:cursor-not-allowed disabled:opacity-45 {phone
							? 'min-h-11 py-1.5 text-[1rem]'
							: 'py-1.5 text-[0.875rem]'} {!phone && i === active && !elsewhere
							? 'bg-accent text-white'
							: 'hover:bg-black/5 dark:hover:bg-white/10'}"
						disabled={!!elsewhere}
						onmouseenter={() => !elsewhere && (active = i)}
						onclick={() => onpick(item.id)}
					>
						<span class="flex min-w-0 items-center gap-2.5">
							<Thumbnail
								name={item.name}
								{accountIndex}
								thumb={item.thumb}
								{glyph}
								size={phone ? 32 : 24}
								onAccent={!phone && i === active && !elsewhere}
							/>
							<span class="min-w-0 truncate">{item.name}</span>
						</span>
						<span
							class="shrink-0 text-[0.75rem] tabular-nums {!phone && i === active && !elsewhere
								? 'text-white/80'
								: 'text-label-2'}">{elsewhere ?? item.detail ?? ''}</span
						>
					</button>
				</li>
			{:else}
				<li class="text-label-2 px-2.5 py-2 text-[0.8125rem]">No matches</li>
			{/each}
		</ul>
	{:else}
		<p class="text-label-2 px-1 pb-2 text-[0.8125rem]">
			Immich's list isn't available, so paste the ID (it's in the address bar when you open it in
			Immich).
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
				class="bg-fill rounded-control w-full px-2.5 py-1.5 text-[1rem] outline-none md:text-[0.875rem]"
			/>
			<Btn type="submit" size="small">Add</Btn>
		</form>
		{#if manualError}<p class="mt-1 px-1 text-[0.75rem] text-[#d70015] dark:text-[#ff6961]">
				{manualError}
			</p>{/if}
	{/if}
{/snippet}

<div use:portal class="if-admin contents" style="background:transparent">
	{#if phone}
		<!-- iOS: a sheet from the bottom over a dimmed page, with its own title and Cancel. -->
		<div class="fixed inset-0 z-50 flex flex-col justify-end">
			<!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_static_element_interactions -->
			<div
				class="absolute inset-0 bg-black/30 dark:bg-black/55"
				transition:fade={{ duration: 150 }}
				onclick={onclose}
			></div>
			<div
				bind:this={panel}
				role="dialog"
				aria-modal="true"
				aria-label="Add {noun}"
				class="relative flex max-h-[85dvh] min-h-[50dvh] flex-col rounded-t-[12px] bg-[#f5f5f7] px-4 pb-[calc(env(safe-area-inset-bottom)+12px)] dark:bg-[#1c1c1e]"
				transition:fly={{ y: 40, duration: 240, easing: cubicOut }}
			>
				<span class="mx-auto mt-1.5 h-[5px] w-9 shrink-0 rounded-full bg-black/20 dark:bg-white/25"
				></span>
				<header class="grid grid-cols-[1fr_auto_1fr] items-center py-2.5">
					<button
						type="button"
						class="justify-self-start text-[1.0625rem] text-primary"
						onclick={onclose}>Cancel</button
					>
					<h2 class="text-[1.0625rem] font-semibold">Add {noun}</h2>
					<span></span>
				</header>
				<div class="bg-group rounded-group flex min-h-0 flex-1 flex-col p-2">
					{@render body()}
				</div>
			</div>
		</div>
	{:else}
		<div
			bind:this={panel}
			role="dialog"
			aria-label="Add {noun.toLowerCase()}"
			class="fixed z-50 w-[22rem] rounded-[10px] p-2 shadow-[0_10px_30px_rgb(0_0_0/0.18),0_0_0_0.5px_rgb(0_0_0/0.12)] dark:shadow-[0_10px_30px_rgb(0_0_0/0.5),0_0_0_0.5px_rgb(255_255_255/0.12)] {material}"
			style="left:{place.left}px;top:{place.top}px;{place.up ? 'transform:translateY(-100%)' : ''}"
			transition:fade={{ duration: 120 }}
		>
			<!-- The arrow, pointing at the row: on top, or underneath when it opens upward. -->
			<span
				aria-hidden="true"
				class="absolute left-6 h-3 w-3 rotate-45 {place.up
					? '-bottom-[6px] shadow-[0.5px_0.5px_0_rgb(0_0_0/0.12)]'
					: '-top-[6px] shadow-[-0.5px_-0.5px_0_rgb(0_0_0/0.12)]'} bg-white/95 dark:bg-[rgb(44_44_46/0.95)]"
			></span>
			{@render body()}
		</div>
	{/if}
</div>
