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
		/** Buttons for the bottom of the desktop sheet; defaults to a prominent Done. */
		actions?: Snippet;
		/** Leaves the phone nav bar untitled when the content already starts with the name. */
		hideTitle?: boolean;
	}

	let { title, onclose, returnFocusTo, children, actions, hideTitle = false }: Props = $props();

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
		// The page behind steps back around the part of it in view (CSS applies it on phones).
		const shell = document.querySelector<HTMLElement>('.if-shell');
		if (shell) shell.style.transformOrigin = `50% ${window.scrollY + 150}px`;
		document.body.classList.add('if-sheet-open');
		return () => {
			clearTimeout(timer);
			window.removeEventListener('keydown', onKey);
			document.body.style.overflow = overflow;
			document.body.classList.remove('if-sheet-open');
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
		class="relative mt-[calc(env(safe-area-inset-top)+24px)] flex h-[calc(100%-env(safe-area-inset-top)-24px)] w-full flex-col overflow-hidden rounded-t-[10px] bg-[#f5f5f7] outline-none md:mt-0 md:h-auto md:max-h-[85dvh] md:w-[30rem] md:rounded-[12px] md:shadow-[0_24px_64px_rgb(0_0_0/0.3),0_0_0_0.5px_rgb(0_0_0/0.15)] dark:bg-[#1c1c1e] dark:md:shadow-[0_24px_64px_rgb(0_0_0/0.6),0_0_0_0.5px_rgb(255_255_255/0.12)]"
		transition:fly={{ y: 24, duration: 220, easing: cubicOut }}
	>
		<!-- Phone: an iOS page sheet, with a grabber and a nav bar. Desktop: a macOS sheet, with
		     no title bar and its buttons at the bottom right. -->
		<button
			type="button"
			class="mx-auto mt-1.5 h-[5px] w-9 shrink-0 rounded-full bg-black/20 md:hidden dark:bg-white/25"
			aria-label="Close"
			onclick={onclose}
		></button>
		<header
			class="border-sep grid grid-cols-[1fr_auto_1fr] items-center border-b px-3 py-2.5 md:hidden"
		>
			<span></span>
			<h2
				class="truncate text-[1.0625rem] font-semibold md:text-[0.9375rem] {hideTitle
					? 'invisible'
					: ''}"
			>
				{title}
			</h2>
			<button
				type="button"
				class="justify-self-end rounded-md px-2 py-1 text-[1.0625rem] font-semibold text-primary focus-visible:outline-2 focus-visible:outline-primary md:text-[0.875rem]"
				onclick={onclose}>Done</button
			>
		</header>
		<div class="flex-1 overflow-y-auto px-4 py-5 md:px-5 md:pt-6">
			<div class="flex flex-col gap-6">
				{@render children()}
			</div>
		</div>
		<footer class="hidden justify-end gap-2 px-5 pt-1 pb-5 md:flex">
			{#if actions}
				{@render actions()}
			{:else}
				<button
					type="button"
					class="bg-accent h-7 rounded-[6px] px-4 text-[0.8125rem] font-medium focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
					onclick={onclose}>Done</button
				>
			{/if}
		</footer>
	</div>
</div>
