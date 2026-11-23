<script lang="ts">
	import { getAvatarUrl } from '$lib/services/admin';
	import Glyph from './glyph.svelte';

	interface Props {
		name: string;
		size?: number;
		/** A saved account whose Immich profile picture replaces the initials, once loaded. */
		avatarOf?: number | null;
	}

	let { name, size = 36, avatarOf = null }: Props = $props();

	// Initials of a name ("Alex Smith" → "AS"), or the text as given for short labels ("2").
	let initials = $derived(
		name
			.split(/\s+/)
			.filter(Boolean)
			.slice(0, 2)
			.map((part) => part[0]?.toUpperCase())
			.join('')
	);

	// A generic name ("Account 1" → "1") gets Apple's person silhouette instead of a digit.
	let generic = $derived(initials === '' || /^\d+$/.test(initials));

	let avatar: string | null = $state(null);
	$effect(() => {
		avatar = null;
		if (avatarOf === null) return;
		let cancelled = false;
		getAvatarUrl(avatarOf).then((url) => {
			if (!cancelled) avatar = url;
		});
		return () => {
			cancelled = true;
		};
	});
</script>

<!-- Apple's contact monogram: a gray gradient circle with white initials, or the person's
     own picture from Immich when they have one. -->
{#if avatar}
	<img
		src={avatar}
		alt=""
		class="shrink-0 rounded-full object-cover"
		style="width:{size}px;height:{size}px"
		aria-hidden="true"
	/>
{:else}
	<span
		class="flex shrink-0 items-center justify-center rounded-full bg-[linear-gradient(#a5abb8,#858994)] font-semibold text-white"
		style="width:{size}px;height:{size}px;font-size:{Math.round(size * 0.42)}px"
		aria-hidden="true"
		>{#if generic}<Glyph
				name="user"
				size={Math.round(size * 0.55)}
				stroke={2}
			/>{:else}{initials}{/if}</span
	>
{/if}
