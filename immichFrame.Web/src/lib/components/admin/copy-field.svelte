<script lang="ts">
	import Btn from './btn.svelte';

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

<!-- A read-only value: the same filled field as every input, with Copy (and Show for secrets). -->
<div class="min-w-0">
	<p class="mb-1 text-[0.8125rem] font-medium">{label}</p>
	<div
		class="bg-fill rounded-control flex min-h-10 min-w-0 items-center gap-1 py-1 pr-1 pl-3 md:min-h-9"
	>
		<span
			class="min-w-0 flex-1 truncate text-[0.9375rem] select-all md:text-[0.875rem] {secret
				? 'font-mono tracking-wider'
				: ''}"
		>
			{secret && !revealed ? '•'.repeat(16) : value}
		</span>
		{#if secret}
			<Btn
				variant="ghost"
				color="primary"
				size="small"
				aria-label={revealed ? `Hide ${label.toLowerCase()}` : `Show ${label.toLowerCase()}`}
				onclick={() => (revealed = !revealed)}>{revealed ? 'Hide' : 'Show'}</Btn
			>
		{/if}
		<Btn
			variant="ghost"
			color="primary"
			size="small"
			aria-label="Copy {label.toLowerCase()}"
			onclick={copy}>{copied ? 'Copied' : 'Copy'}</Btn
		>
	</div>
	<span class="sr-only" aria-live="polite">{copied ? `${label} copied` : ''}</span>
</div>
