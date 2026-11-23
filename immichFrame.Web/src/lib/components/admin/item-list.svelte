<script lang="ts">
	import type { GlyphName } from './glyphs';
	import ItemPicker, { type PickerItem } from './item-picker.svelte';
	import ItemRow from './item-row.svelte';

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
		manualPlaceholder,
		emptyText,
		accountIndex = null,
		glyph,
		onchange
	}: Props = $props();

	let open = $state(false);
	let addButton: HTMLElement | undefined = $state();

	const byId = $derived(new Map((items ?? []).map((item) => [item.id.toLowerCase(), item])));

	function close() {
		open = false;
		addButton?.focus();
	}
</script>

<!-- The selected items as rows, then an "Add …" row that opens a searchable picker. -->
{#each selected as id (id)}
	<ItemRow
		{id}
		item={byId.get(id.toLowerCase())}
		{loading}
		listed={!!items}
		{accountIndex}
		{glyph}
		onremove={() => onchange(selected.filter((s) => s !== id))}
	/>
{/each}

<div class="border-sep border-t first:border-t-0">
	<button
		bind:this={addButton}
		type="button"
		class="-mx-1.5 flex min-h-11 w-[calc(100%+0.75rem)] items-center rounded-md px-1.5 py-2 text-left text-[0.9375rem] text-primary focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary md:text-[0.875rem]"
		aria-expanded={open}
		aria-haspopup="dialog"
		onclick={() => (open ? close() : (open = true))}
	>
		Add {noun}…
	</button>
	{#if !selected.length && emptyText}
		<p class="text-label-2 -mt-2 pb-2.5 text-[0.75rem]">{emptyText}</p>
	{/if}
</div>

{#if open && addButton}
	<ItemPicker
		{noun}
		{items}
		{loading}
		{selected}
		{usedElsewhere}
		{manualPattern}
		{manualPlaceholder}
		{accountIndex}
		{glyph}
		anchor={addButton}
		onpick={(id) => {
			onchange([...selected, id]);
			close();
		}}
		onclose={close}
	/>
{/if}
