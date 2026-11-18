<script lang="ts">
	import { untrack, type Snippet } from 'svelte';
	import { fade, fly } from 'svelte/transition';
	import { cubicOut } from 'svelte/easing';

	interface Props {
		title: string;
		onclose: () => void;
		/** id of the element that opened the sheet, to focus again when it closes. */
		returnFocusTo?: string;
		children: Snippet;
	}

	let { title, onclose, returnFocusTo, children }: Props = $props();

	let panel: HTMLElement | undefined = $state();
	// Captured when the sheet opens: props are live, and by the time it closes the opener's
	// state (and so this id) may already have changed.
	const returnId = untrack(() => returnFocusTo);

	// At the end of <body>, so no sticky or transformed parent can trap it.
	function portal(node: HTMLElement) {
		document.body.appendChild(node);
		return { destroy: () => node.remove() };
	}

	$effect(() => {
		const timer = setTimeout(() => panel?.focus(), 30);
		const onKey = (e: KeyboardEvent) => {
			if (e.key === 'Escape') {
				e.preventDefault();
				onclose();
			}
		};
		window.addEventListener('keydown', onKey);
		const overflow = document.body.style.overflow;
		document.body.style.overflow = 'hidden';
		return () => {
			clearTimeout(timer);
			window.removeEventListener('keydown', onKey);
			document.body.style.overflow = overflow;
			if (returnId) requestAnimationFrame(() => document.getElementById(returnId)?.focus());
		};
	});
</script>

<!-- A sheet: centered over a dimmed page on desktop, full screen on a phone. -->
<div
	use:portal
	class="if-admin fixed inset-0 z-50 flex items-end justify-center md:items-center md:p-6"
	style="background:transparent"
>
	<!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_static_element_interactions -->
	<div
		class="absolute inset-0 bg-black/30 dark:bg-black/55"
		transition:fade={{ duration: 150 }}
		onclick={onclose}
	></div>
	<div
		bind:this={panel}
		role="dialog"
		aria-modal="true"
		aria-label={title}
		tabindex="-1"
		class="relative flex h-full w-full flex-col overflow-hidden bg-[#f5f5f7] outline-none md:h-auto md:max-h-[85dvh] md:w-[30rem] md:rounded-[12px] md:shadow-[0_24px_64px_rgb(0_0_0/0.3),0_0_0_0.5px_rgb(0_0_0/0.15)] dark:bg-[#1c1c1e] dark:md:shadow-[0_24px_64px_rgb(0_0_0/0.6),0_0_0_0.5px_rgb(255_255_255/0.12)]"
		transition:fly={{ y: 24, duration: 220, easing: cubicOut }}
	>
		<header class="border-sep grid grid-cols-[1fr_auto_1fr] items-center border-b px-3 py-2.5">
			<span></span>
			<h2 class="truncate text-[1.0625rem] font-semibold md:text-[0.9375rem]">{title}</h2>
			<button
				type="button"
				class="justify-self-end rounded-md px-2 py-1 text-[1.0625rem] font-semibold text-primary focus-visible:outline-2 focus-visible:outline-primary md:text-[0.875rem]"
				onclick={onclose}>Done</button
			>
		</header>
		<div class="flex-1 overflow-y-auto px-4 py-5">
			<div class="flex flex-col gap-6">
				{@render children()}
			</div>
		</div>
	</div>
</div>
