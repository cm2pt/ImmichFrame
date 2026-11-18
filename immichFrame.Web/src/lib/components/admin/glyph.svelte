<script lang="ts">
	import { glyphs, type GlyphName } from './glyphs';

	interface Props {
		name: GlyphName;
		size?: number;
		class?: string;
		/** Line weight; 1.75 reads like SF Symbols' regular weight at 15–20 px. */
		stroke?: number;
	}

	let { name, size = 18, class: className = '', stroke = 1.75 }: Props = $props();
</script>

<svg
	xmlns="http://www.w3.org/2000/svg"
	width={size}
	height={size}
	viewBox="0 0 24 24"
	fill="none"
	stroke="currentColor"
	stroke-width={stroke}
	stroke-linecap="round"
	stroke-linejoin="round"
	class="shrink-0 {className}"
	aria-hidden="true"
>
	<!-- Static, trusted shapes bundled in glyphs.ts. -->
	<!-- eslint-disable-next-line svelte/no-at-html-tags -->
	{@html glyphs[name]}
</svg>
