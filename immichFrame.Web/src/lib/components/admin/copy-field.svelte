<script lang="ts">
	import { mdiCheck, mdiContentCopy, mdiEyeOffOutline, mdiEyeOutline } from '@mdi/js';
	import { IconButton, Text } from '@immich/ui';

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
			// Clipboard needs a secure context; reveal the value so it can be selected instead.
			revealed = true;
		}
	}
</script>

<div class="min-w-0">
	<Text size="small" fontWeight="medium" class="mb-1">{label}</Text>
	<div
		class="flex min-w-0 items-center gap-1 rounded-xl border border-light-300 bg-light-50 py-1 pr-1 pl-4"
	>
		<code class="min-w-0 flex-1 truncate font-mono text-sm select-all">
			{secret && !revealed ? '•'.repeat(16) : value}
		</code>
		{#if secret}
			<IconButton
				icon={revealed ? mdiEyeOffOutline : mdiEyeOutline}
				size="small"
				variant="ghost"
				color="secondary"
				aria-label={revealed ? `Hide ${label.toLowerCase()}` : `Show ${label.toLowerCase()}`}
				onclick={() => (revealed = !revealed)}
			/>
		{/if}
		<IconButton
			icon={copied ? mdiCheck : mdiContentCopy}
			size="small"
			variant="ghost"
			color={copied ? 'success' : 'secondary'}
			aria-label={copied ? 'Copied' : `Copy ${label.toLowerCase()}`}
			title={copied ? 'Copied' : 'Copy'}
			onclick={copy}
		/>
	</div>
	<span class="sr-only" aria-live="polite">{copied ? `${label} copied` : ''}</span>
</div>
