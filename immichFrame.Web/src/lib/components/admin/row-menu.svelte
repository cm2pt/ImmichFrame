<script lang="ts">
	import Glyph from './glyph.svelte';

	interface Item {
		label: string;
		destructive?: boolean;
		onselect: () => void;
	}

	interface Props {
		label: string;
		items: Item[];
		id?: string;
	}

	let { label, items, id }: Props = $props();

	let open = $state(false);
	let button: HTMLButtonElement | undefined = $state();
	let menu: HTMLElement | undefined = $state();
	let active = $state(0);

	$effect(() => {
		if (!open) return;
		active = 0;
		requestAnimationFrame(() => menu?.querySelector<HTMLElement>('[role=menuitem]')?.focus());
		const onDown = (e: MouseEvent) => {
			if (!menu?.contains(e.target as Node) && !button?.contains(e.target as Node)) open = false;
		};
		document.addEventListener('mousedown', onDown);
		return () => document.removeEventListener('mousedown', onDown);
	});

	function close() {
		open = false;
		button?.focus();
	}

	function onKeydown(e: KeyboardEvent) {
		const buttons = [...(menu?.querySelectorAll<HTMLElement>('[role=menuitem]') ?? [])];
		if (e.key === 'Escape') close();
		else if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
			e.preventDefault();
			active = (active + (e.key === 'ArrowDown' ? 1 : -1) + buttons.length) % buttons.length;
			buttons[active]?.focus();
		}
	}
</script>

<!-- A "⋯" button with a small menu, like a list row's More button in Apple apps. -->
<div class="relative">
	<button
		bind:this={button}
		{id}
		type="button"
		class="text-label-2 flex h-8 w-8 items-center justify-center rounded-full hover:bg-black/5 focus-visible:outline-2 focus-visible:outline-primary dark:hover:bg-white/10"
		aria-label={label}
		aria-haspopup="menu"
		aria-expanded={open}
		onclick={() => (open = !open)}
	>
		<Glyph name="ellipsis" size={18} stroke={2.2} />
	</button>
	{#if open}
		<div
			bind:this={menu}
			role="menu"
			tabindex="-1"
			aria-label={label}
			class="bg-group border-sep absolute top-full right-0 z-30 mt-1 min-w-40 rounded-[10px] border p-1 shadow-[0_12px_32px_rgb(0_0_0/0.18)]"
			onkeydown={onKeydown}
		>
			{#each items as item (item.label)}
				<button
					type="button"
					role="menuitem"
					class="block w-full rounded-[6px] px-3 py-1.5 text-left text-[0.875rem] hover:bg-[var(--if-accent-fill)] focus:bg-[var(--if-accent-fill)] focus:outline-none {item.destructive
						? 'text-[#d70015] hover:text-white focus:text-white dark:text-[#ff6961]'
						: ''}"
					onclick={() => {
						open = false;
						item.onselect();
					}}
				>
					{item.label}
				</button>
			{/each}
		</div>
	{/if}
</div>
