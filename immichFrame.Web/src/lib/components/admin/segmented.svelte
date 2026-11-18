<script lang="ts" generics="T extends string | number">
	import PopupSelect from './popup-select.svelte';

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
	let selectedIndex = $derived(options.findIndex((o) => o.value === value));
	// One tab stop for the group (the selected option, or the first); arrows move and select,
	// like a native radio group.
	let focusIndex = $derived(Math.max(0, selectedIndex));

	// The highlight slides to the selected option.
	let thumb = $state({ left: 0, width: 0, ready: false });
	$effect(() => {
		const el = buttons[selectedIndex];
		if (!el) {
			thumb = { ...thumb, width: 0 };
			return;
		}
		const update = () => (thumb = { left: el.offsetLeft, width: el.offsetWidth, ready: true });
		update();
		const observer = new ResizeObserver(update);
		observer.observe(el);
		return () => observer.disconnect();
	});

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
		<PopupSelect
			{label}
			options={options.map((o) => ({ value: String(o.value), label: o.label }))}
			value={value === undefined ? undefined : String(value)}
			onchange={(v) => {
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
		: 'inline-flex'} bg-fill relative max-w-full rounded-[9px] p-[2px]"
>
	{#if thumb.width}
		<span
			aria-hidden="true"
			class="absolute top-[2px] bottom-[2px] rounded-[7px] bg-white shadow-[0_3px_8px_rgba(0,0,0,0.12),0_3px_1px_rgba(0,0,0,0.04)] dark:bg-[#636366] {thumb.ready
				? 'transition-all duration-200 ease-out'
				: ''}"
			style="left:{thumb.left}px;width:{thumb.width}px"
		></span>
	{/if}
	{#each options as option, index (option.value)}
		{@const selected = option.value === value}
		<button
			type="button"
			role="radio"
			aria-checked={selected}
			tabindex={index === focusIndex ? 0 : -1}
			bind:this={buttons[index]}
			class="relative z-[1] rounded-[7px] px-3.5 py-[5px] text-[0.8125rem] whitespace-nowrap transition-colors focus-visible:outline-2 focus-visible:outline-primary {selected
				? 'font-semibold'
				: 'font-medium opacity-80 hover:opacity-100'}"
			onclick={() => onchange(option.value)}
			onkeydown={(e) => onKeydown(e, index)}
		>
			{option.label}
		</button>
	{/each}
</div>
