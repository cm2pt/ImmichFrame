<script lang="ts">
	import type { Snippet } from 'svelte';

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

<div class="border-sep min-h-11 border-t py-3 first:border-t-0" {id} data-setting={title}>
	<div class="flex flex-wrap items-center justify-between gap-x-6 gap-y-2.5">
		<!-- A real basis (not flex-1's 0) so the row wraps the control under the title when
		     both don't fit, instead of drawing it over the text. -->
		<div class="flex min-w-0 flex-[1_1_12rem] items-start gap-3">
			{#if icon}
				<div class="mt-0.5 text-primary">{@render icon()}</div>
			{/if}
			<div class="min-w-0">
				<p class="text-[0.9375rem] leading-snug font-medium">{title}</p>
				{#if description}
					<p class="text-label-2 mt-0.5 text-[0.8125rem] leading-snug">{description}</p>
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
