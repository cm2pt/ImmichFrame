<script lang="ts">
	import { onMount } from 'svelte';
	import { mdiShuffleVariant } from '@mdi/js';
	import { Button, Card, CardBody, Text } from '@immich/ui';
	import type { AdminPreview } from './admin-preview.svelte';

	interface Props {
		preview: AdminPreview;
	}

	let { preview }: Props = $props();

	onMount(() => {
		if (preview.state === 'idle') preview.load(4);
	});
</script>

<Card>
	<CardBody>
		<div class="mb-3 flex items-center justify-between gap-2">
			<Text fontWeight="semi-bold">Up next</Text>
			<Button
				size="small"
				variant="outline"
				leadingIcon={mdiShuffleVariant}
				loading={preview.state === 'loading'}
				onclick={() => preview.load(4)}
			>
				Shuffle
			</Button>
		</div>
		{#if preview.state === 'error'}
			<Text size="small" color="danger">
				{preview.message}
				<button class="underline" onclick={() => preview.load(4)}>Retry</button>
			</Text>
		{:else}
			<div class="grid grid-cols-4 gap-2">
				{#each preview.state === 'ready' ? preview.items : [0, 1, 2, 3].map(() => null) as item, i (item?.id ?? i)}
					<div class="aspect-[3/4] overflow-hidden rounded-lg bg-subtle">
						{#if item?.url}
							<img
								src={item.url}
								alt={item.fileName ?? ''}
								title={[item.fileName, item.city].filter(Boolean).join(' · ')}
								class="h-full w-full object-cover"
							/>
						{/if}
					</div>
				{/each}
			</div>
		{/if}
		<Text size="small" class="text-dark/70 mt-3">
			A random sample from the saved choice, so you can spot screenshots or receipts that slip in.
		</Text>
	</CardBody>
</Card>
