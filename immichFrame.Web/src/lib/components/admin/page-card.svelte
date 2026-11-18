<script lang="ts">
	import type { Snippet } from 'svelte';

	interface Props {
		/** The group's header, shown above it in small capitals (Apple's grouped-list style). */
		title?: string;
		/** A note under the group. */
		footer?: string;
		/** Older name for the note; shown under the group too. */
		description?: string;
		actions?: Snippet;
		children?: Snippet;
		/** Extra content under the group's note (e.g. a link button). */
		after?: Snippet;
		id?: string;
	}

	let { title, footer, description, actions, children, after, id }: Props = $props();
	let note = $derived(footer ?? description);
</script>

<section class="scroll-mt-24" {id} aria-label={title}>
	{#if title || actions}
		<div class="mb-1.5 flex min-h-6 items-end justify-between gap-3 px-4">
			{#if title}
				<h2 class="text-label-2 text-[0.8125rem] tracking-[0.02em] uppercase">{title}</h2>
			{/if}
			{#if actions}<div class="flex items-center gap-1">{@render actions()}</div>{/if}
		</div>
	{/if}
	<div class="bg-group rounded-group px-4">
		{@render children?.()}
	</div>
	{#if note}
		<p class="text-label-2 mt-1.5 px-4 text-[0.8125rem] leading-snug">{note}</p>
	{/if}
	{@render after?.()}
</section>
