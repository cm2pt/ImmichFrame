<script lang="ts">
	import type { Snippet } from 'svelte';

	interface Props {
		title: string;
		description?: string;
		/** A leading 29 px tile (System Settings style), when the row needs an icon. */
		icon?: Snippet;
		/** The control on the right (switch, choices…). */
		control?: Snippet;
		/** Anything that needs the full width under the title. */
		children?: Snippet;
		/** Indent under a parent row (e.g. memory types under Memories). */
		nested?: boolean;
		id?: string;
	}

	let { title, description, icon, control, children, nested = false, id }: Props = $props();
</script>

<div
	class="border-sep min-h-11 border-t py-2.5 first:border-t-0 {nested ? 'ml-6' : ''}"
	{id}
	data-setting={title}
>
	<div class="flex flex-wrap items-center justify-between gap-x-6 gap-y-2">
		<!-- A real basis (not flex-1's 0) so the control wraps under the title when both don't
		     fit, instead of covering it. -->
		<div class="flex min-w-0 flex-[1_1_9rem] items-center gap-3">
			{#if icon}{@render icon()}{/if}
			<div class="min-w-0">
				<p class="text-[0.9375rem] leading-snug md:text-[0.875rem]">{title}</p>
				{#if description}
					<p class="text-label-2 mt-0.5 text-[0.8125rem] leading-snug md:text-[0.75rem]">
						{description}
					</p>
				{/if}
			</div>
		</div>
		{#if control}
			<div class="flex max-w-full min-w-0 flex-wrap items-center gap-2.5 sm:justify-end">
				{@render control()}
			</div>
		{/if}
	</div>
	{#if children}
		<div class="mt-2.5 pb-0.5">{@render children()}</div>
	{/if}
</div>
