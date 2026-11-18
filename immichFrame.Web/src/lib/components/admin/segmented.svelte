<script lang="ts" generics="T extends string | number">
	import { Select } from '@immich/ui';
	interface Option {
		value: T;
		label: string;
	}

	interface Props {
		options: Option[];
		value: T | undefined;
		label: string;
		onchange: (value: T) => void;
	}

	let { options, value, label, onchange }: Props = $props();

	let buttons: HTMLButtonElement[] = $state([]);
	// One tab stop for the group (the selected option, or the first); arrows move and select,
	// like a native radio group.
	let focusIndex = $derived(
		Math.max(
			0,
			options.findIndex((o) => o.value === value)
		)
	);

	function onKeydown(event: KeyboardEvent, index: number) {
		const step = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[event.key];
		const jump = event.key === 'Home' ? 0 : event.key === 'End' ? options.length - 1 : null;
		if (step === undefined && jump === null) return;
		event.preventDefault();
		const next = jump ?? (index + step! + options.length) % options.length;
		onchange(options[next].value);
		buttons[next]?.focus();
	}
</script>

<!-- A row of mutually exclusive choices. With more than three, phones get a menu instead,
     so the row never wraps onto two lines. -->
{#if options.length > 3}
	<div class="w-full sm:hidden" role="group" aria-label={label}>
		<Select
			options={options.map((o) => ({ value: String(o.value), label: o.label }))}
			value={value === undefined ? undefined : String(value)}
			onChange={(v) => {
				const match = options.find((o) => String(o.value) === v);
				if (match) onchange(match.value);
			}}
		/>
	</div>
{/if}
<div
	role="radiogroup"
	aria-label={label}
	class="{options.length > 3
		? 'hidden sm:inline-flex'
		: 'inline-flex'} max-w-full flex-wrap gap-1 rounded-[1.25rem] bg-subtle/60 p-1 dark:bg-light-200"
>
	{#each options as option, index (option.value)}
		{@const selected = option.value === value}
		<button
			type="button"
			role="radio"
			aria-checked={selected}
			tabindex={index === focusIndex ? 0 : -1}
			bind:this={buttons[index]}
			class="rounded-full px-3 py-1.5 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-primary {selected
				? 'bg-light text-primary shadow-sm dark:bg-light-50'
				: 'text-dark/75 hover:text-dark'}"
			onclick={() => onchange(option.value)}
			onkeydown={(e) => onKeydown(e, index)}
		>
			{option.label}
		</button>
	{/each}
</div>
