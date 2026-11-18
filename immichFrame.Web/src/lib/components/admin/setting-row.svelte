<script lang="ts">
	import type { Snippet } from 'svelte';
	import { Text } from '@immich/ui';

	interface Props {
		title: string;
		description?: string;
		icon?: Snippet;
		/** The control on the right (switch, choices…). */
		control?: Snippet;
		/** Anything that needs the full width under the title (chips, inputs). */
		children?: Snippet;
		id?: string;
	}

	let { title, description, icon, control, children, id }: Props = $props();
</script>

<div class="border-t border-light-200 py-4 first:border-t-0" {id}>
	<div class="flex flex-wrap items-center justify-between gap-x-6 gap-y-3">
		<div class="flex min-w-0 flex-1 items-start gap-3">
			{#if icon}
				<div class="mt-0.5 text-primary">{@render icon()}</div>
			{/if}
			<div class="min-w-48">
				<Text fontWeight="semi-bold">{title}</Text>
				{#if description}
					<Text size="small" class="text-dark/70">{description}</Text>
				{/if}
			</div>
		</div>
		{#if control}
			<!-- Shrinks and wraps under the title on narrow screens instead of overflowing. -->
			<div class="flex max-w-full min-w-0 flex-wrap items-center gap-3 sm:justify-end">
				{@render control()}
			</div>
		{/if}
	</div>
	{#if children}
		<div class="mt-3">{@render children()}</div>
	{/if}
</div>
