<script lang="ts">
	import { getThumbnailUrl, type ThumbnailKind } from '$lib/services/admin';
	import Glyph from './glyph.svelte';
	import type { GlyphName } from './glyphs';
	import Monogram from './monogram.svelte';

	interface Props {
		name: string;
		/** The saved account to load from; without one (an unsaved account) the fallback shows. */
		accountIndex?: number | null;
		thumb?: { kind: ThumbnailKind; id: string } | null;
		/** Shown instead of a monogram when there's no picture to load (e.g. tags). */
		glyph?: GlyphName;
		size?: number;
	}

	let { name, accountIndex = null, thumb = null, glyph, size = 28 }: Props = $props();

	let src: string | null = $state(null);
	let node: HTMLElement | undefined = $state();

	// Loads once the row scrolls into view, so a long list asks only for what's seen.
	$effect(() => {
		src = null;
		if (!node || !thumb || accountIndex === null) return;
		const { kind, id } = thumb;
		const index = accountIndex;
		let cancelled = false;
		const observer = new IntersectionObserver((entries) => {
			if (!entries.some((e) => e.isIntersecting)) return;
			observer.disconnect();
			getThumbnailUrl(index, kind, id).then((url) => {
				if (!cancelled) src = url;
			});
		});
		observer.observe(node);
		return () => {
			cancelled = true;
			observer.disconnect();
		};
	});

	// People are round, like contacts; albums are rounded squares, like Photos.
	let radius = $derived(
		thumb?.kind === 'asset' || (!thumb && glyph) ? 'rounded-[6px]' : 'rounded-full'
	);
</script>

<span
	bind:this={node}
	class="relative flex shrink-0 overflow-hidden {radius}"
	style="width:{size}px;height:{size}px"
	aria-hidden="true"
>
	{#if src}
		<img {src} alt="" class="h-full w-full object-cover" />
	{:else if glyph}
		<span class="bg-fill text-label-2 flex h-full w-full items-center justify-center rounded-[6px]">
			<Glyph name={glyph} size={Math.round(size * 0.55)} stroke={2} />
		</span>
	{:else if thumb?.kind === 'asset'}
		<span class="bg-fill text-label-3 flex h-full w-full items-center justify-center">
			<Glyph name="images" size={Math.round(size * 0.55)} stroke={2} />
		</span>
	{:else}
		<Monogram {name} {size} />
	{/if}
</span>
