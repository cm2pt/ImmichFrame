<script lang="ts">
	interface Props {
		name: string;
		size?: number;
	}

	let { name, size = 36 }: Props = $props();

	// Initials of a name ("Alex Smith" → "AS"), or the text as given for short labels ("2").
	let initials = $derived(
		name
			.split(/\s+/)
			.filter(Boolean)
			.slice(0, 2)
			.map((part) => part[0]?.toUpperCase())
			.join('')
	);
</script>

<!-- Apple's contact monogram: a gray gradient circle with white initials. -->
<span
	class="flex shrink-0 items-center justify-center rounded-full bg-[linear-gradient(#a5abb8,#858994)] font-semibold text-white"
	style="width:{size}px;height:{size}px;font-size:{Math.round(size * 0.42)}px"
	aria-hidden="true">{initials}</span
>
