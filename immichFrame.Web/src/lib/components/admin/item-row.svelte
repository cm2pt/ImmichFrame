<script lang="ts">
	import Glyph from './glyph.svelte';
	import type { GlyphName } from './glyphs';
	import type { PickerItem } from './item-picker.svelte';
	import Thumbnail from './thumbnail.svelte';

	interface Props {
		/** The stored id (album/person GUID, or tag value). */
		id: string;
		/** What Immich knows about it; undefined while loading or when it's gone. */
		item?: PickerItem;
		loading?: boolean;
		/** False when Immich's list couldn't be read at all (then the raw id is shown). */
		listed?: boolean;
		accountIndex?: number | null;
		glyph?: GlyphName;
		onremove: () => void;
	}

	let {
		id,
		item,
		loading = false,
		listed = true,
		accountIndex = null,
		glyph,
		onremove
	}: Props = $props();
</script>

<div
	class="border-sep flex min-h-11 items-center gap-3 border-t py-1.5 first:border-t-0"
	title={id}
>
	<Thumbnail name={item?.name ?? '?'} {accountIndex} thumb={item?.thumb} {glyph} />
	<span class="min-w-0 flex-1 truncate text-[0.9375rem] md:text-[0.875rem]">
		{#if loading && !item}
			<span class="bg-fill inline-block h-3 w-28 animate-pulse rounded" aria-label="Loading"></span>
		{:else}
			{item?.name ?? (listed ? 'Not found in Immich' : id)}
		{/if}
	</span>
	{#if item?.detail}<span class="text-label-2 text-[0.8125rem] tabular-nums">{item.detail}</span
		>{/if}
	<!-- A 44 pt target around an 18 pt glyph, as iOS asks for. -->
	<button
		type="button"
		class="text-label-3 -my-2 -mr-3 rounded-full p-3 hover:text-[#d70015] focus-visible:outline-2 focus-visible:-outline-offset-4 focus-visible:outline-primary dark:hover:text-[#ff6961]"
		aria-label="Remove {item?.name ?? id}"
		onclick={onremove}
	>
		<Glyph name="circleMinus" size={18} />
	</button>
</div>
