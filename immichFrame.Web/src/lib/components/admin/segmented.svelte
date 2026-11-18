<script lang="ts" generics="T extends string | number">
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
</script>

<!-- A row of mutually exclusive choices, like Immich's own setting toggles. -->
<div
	role="radiogroup"
	aria-label={label}
	class="inline-flex flex-wrap gap-1 rounded-full bg-subtle/60 p-1 dark:bg-light-200"
>
	{#each options as option (option.value)}
		{@const selected = option.value === value}
		<button
			type="button"
			role="radio"
			aria-checked={selected}
			class="rounded-full px-3 py-1.5 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-primary {selected
				? 'bg-light text-primary shadow-sm dark:bg-light-50'
				: 'text-dark/70 hover:text-dark'}"
			onclick={() => onchange(option.value)}
		>
			{option.label}
		</button>
	{/each}
</div>
