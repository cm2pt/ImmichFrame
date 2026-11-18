<script lang="ts">
	import type { Snippet } from 'svelte';
	import { Card, CardBody, Heading, Text } from '@immich/ui';

	interface Props {
		title?: string;
		description?: string;
		actions?: Snippet;
		children?: Snippet;
		id?: string;
	}

	let { title, description, actions, children, id }: Props = $props();
</script>

<Card class="scroll-mt-24" {id}>
	<CardBody>
		{#if title}
			<div class="mb-2 flex flex-wrap items-start justify-between gap-3">
				<div>
					<Heading tag="h2" size="small" fontWeight="semi-bold">{title}</Heading>
					{#if description}
						<Text size="small" class="mt-1 text-dark/70">{description}</Text>
					{/if}
				</div>
				{#if actions}<div class="flex gap-2">{@render actions()}</div>{/if}
			</div>
		{/if}
		{@render children?.()}
	</CardBody>
</Card>
