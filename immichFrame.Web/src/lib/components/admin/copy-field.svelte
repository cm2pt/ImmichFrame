<script lang="ts">
	import { Button, Text } from '@immich/ui';

	interface Props {
		label: string;
		value: string;
		secret?: boolean;
	}

	let { label, value, secret = false }: Props = $props();

	let revealed = $state(false);
	let copied = $state(false);

	async function copy() {
		try {
			await navigator.clipboard.writeText(value);
			copied = true;
			setTimeout(() => (copied = false), 1500);
		} catch {
			// Clipboard needs a secure context; the value is selectable as a fallback.
			revealed = true;
		}
	}
</script>

<div>
	<Text size="small" fontWeight="medium" class="mb-1">{label}</Text>
	<div
		class="flex items-center gap-2 rounded-xl border border-light-300 bg-light-50 py-1.5 pr-1.5 pl-4"
	>
		<code class="min-w-0 flex-1 truncate font-mono text-sm select-all">
			{secret && !revealed ? '•'.repeat(Math.min(Math.max(value.length, 8), 24)) : value}
		</code>
		{#if secret}
			<Button
				size="small"
				variant="outline"
				color="secondary"
				onclick={() => (revealed = !revealed)}
			>
				{revealed ? 'Hide' : 'Show'}
			</Button>
		{/if}
		<Button size="small" variant="outline" color="secondary" onclick={copy}>
			{copied ? 'Copied' : 'Copy'}
		</Button>
	</div>
</div>
