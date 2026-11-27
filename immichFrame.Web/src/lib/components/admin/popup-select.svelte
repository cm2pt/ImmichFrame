<script lang="ts">
	import Glyph from './glyph.svelte';

	interface Option {
		value: string;
		label: string;
		/** What the button shows on a phone when the label is long ("14:30" for
		 * "14:30 · 24-hour"); the menu always lists the full label. */
		short?: string;
	}

	interface Props {
		options: Option[];
		value: string | undefined;
		label: string;
		onchange: (value: string) => void;
		disabled?: boolean;
	}

	let { options, value, label, onchange, disabled = false }: Props = $props();
	let chosen = $derived(options.find((o) => o.value === value));
	let current = $derived(chosen?.label ?? '');
	let short = $derived(chosen?.short ?? current);
</script>

<!-- Apple's pop-up button: the current value and an up-down chevron. A native <select>
     underneath opens the system's own menu (and its picker wheel on iPhone). -->
<label
	class="bg-fill relative inline-flex h-8 max-w-full min-w-0 cursor-pointer items-center gap-1.5 rounded-[7px] pr-2 pl-3 text-[0.875rem] focus-within:outline-2 focus-within:outline-primary md:h-7 md:text-[0.8125rem] {disabled
		? 'pointer-events-none opacity-40'
		: ''}"
>
	{#if short !== current}
		<span class="truncate sm:hidden">{short}</span>
		<span class="hidden truncate sm:inline">{current}</span>
	{:else}
		<span class="truncate">{current}</span>
	{/if}
	<Glyph name="chevronsUpDown" size={13} stroke={2} class="text-label-2" />
	<select
		class="absolute inset-0 cursor-pointer opacity-0"
		aria-label={label}
		{disabled}
		{value}
		onchange={(e) => onchange(e.currentTarget.value)}
	>
		{#each options as option (option.value)}
			<option value={option.value}>{option.label}</option>
		{/each}
	</select>
</label>
