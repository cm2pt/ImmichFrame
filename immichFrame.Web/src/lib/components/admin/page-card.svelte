<script lang="ts">
	import type { Snippet } from 'svelte';

	interface Props {
		title?: string;
		description?: string;
		/** A note under the group, as System Settings puts explanations. */
		footer?: string;
		actions?: Snippet;
		children?: Snippet;
		id?: string;
	}

	let { title, description, footer, actions, children, id }: Props = $props();
</script>

<!-- A grouped list: a small heading above, a rounded group, an optional note below. -->
<section class="scroll-mt-24" {id} aria-label={title}>
	{#if title}
		<div class="mb-2 flex items-end justify-between gap-3 px-1">
			<div>
				<h2 class="text-[1.0625rem] font-semibold tracking-[-0.01em]">{title}</h2>
				{#if description}
					<p class="text-label-2 mt-0.5 text-[0.8125rem]">{description}</p>
				{/if}
			</div>
			{#if actions}<div class="flex gap-2">{@render actions()}</div>{/if}
		</div>
	{/if}
	<div class="bg-group rounded-2xl px-4 py-1 shadow-[0_1px_2px_rgba(0,0,0,0.04)]">
		{@render children?.()}
	</div>
	{#if footer}
		<p class="text-label-2 mt-2 px-4 text-[0.8125rem]">{footer}</p>
	{/if}
</section>
