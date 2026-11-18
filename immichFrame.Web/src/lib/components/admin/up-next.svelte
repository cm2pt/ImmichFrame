<script lang="ts">
	import { onMount } from 'svelte';
	import PageCard from './page-card.svelte';
	import type { AdminPreview } from './admin-preview.svelte';

	interface Props {
		preview: AdminPreview;
	}

	let { preview }: Props = $props();

	onMount(() => {
		if (preview.state === 'idle') preview.load(4);
	});
</script>

<PageCard
	title="Up next"
	footer="A sample of what will show next, to spot screenshots or receipts that slip in."
>
	{#snippet actions()}
		<button
			type="button"
			class="text-primary disabled:opacity-40"
			disabled={preview.state === 'loading'}
			onclick={() => preview.load(4)}>Shuffle</button
		>
	{/snippet}
	<div class="py-4">
		{#if preview.state === 'error'}
			<p class="text-[0.8125rem]">
				{preview.message}
				<button class="text-primary" onclick={() => preview.load(4)}>Try Again</button>
			</p>
		{:else}
			<div class="grid grid-cols-4 gap-2">
				{#each preview.state === 'ready' ? preview.items : [0, 1, 2, 3].map(() => null) as item, i (item?.id ?? i)}
					<div class="bg-fill aspect-[3/4] overflow-hidden rounded-[8px]">
						{#if item?.url}
							<img
								src={item.url}
								alt={item.fileName ?? ''}
								title={[item.fileName, item.city].filter(Boolean).join(', ')}
								class="h-full w-full object-cover"
							/>
						{/if}
					</div>
				{/each}
			</div>
		{/if}
	</div>
</PageCard>
