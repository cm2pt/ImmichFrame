<script lang="ts">
	interface Props {
		title: string;
		value: string;
		secret?: boolean;
		/** Show a Copy button (default) */
		copyable?: boolean;
	}

	let { title, value, secret = false, copyable = true }: Props = $props();

	let revealed = $state(false);
	let copied = $state(false);

	async function copy() {
		try {
			await navigator.clipboard.writeText(value);
			copied = true;
			setTimeout(() => (copied = false), 1500);
		} catch {
			revealed = true;
		}
	}
</script>

<!-- A read-only value: label, the value in secondary text, then text buttons. Never a fake input. -->
<div
	class="border-sep flex min-h-11 items-center gap-4 border-t py-1.5 first:border-t-0"
	data-setting={title}
>
	<span class="shrink-0 text-[0.9375rem] md:text-[0.875rem]">{title}</span>
	<span
		class="text-label-2 min-w-0 flex-1 truncate text-right text-[0.9375rem] select-all md:text-[0.875rem] {secret &&
		!revealed
			? 'tracking-[0.15em]'
			: ''}">{secret && !revealed ? '••••••••••••' : value}</span
	>
	{#if secret}
		<button
			type="button"
			class="shrink-0 text-[0.9375rem] text-primary md:text-[0.875rem]"
			onclick={() => (revealed = !revealed)}>{revealed ? 'Hide' : 'Show'}</button
		>
	{/if}
	{#if copyable}
		<button
			type="button"
			class="shrink-0 text-[0.9375rem] text-primary md:text-[0.875rem]"
			onclick={copy}>{copied ? 'Copied' : 'Copy'}</button
		>
	{/if}
	<span class="sr-only" aria-live="polite">{copied ? `${title} copied` : ''}</span>
</div>
