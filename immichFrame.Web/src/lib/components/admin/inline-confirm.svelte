<script lang="ts">
	import Btn from './btn.svelte';
	import { onMount } from 'svelte';

	interface Props {
		message: string;
		confirmLabel: string;
		danger?: boolean;
		/** id of the button that opened this, to give focus back when it closes. */
		returnFocusTo?: string;
		onconfirm: () => void;
		oncancel: () => void;
	}

	let {
		message,
		confirmLabel,
		danger = false,
		returnFocusTo,
		onconfirm,
		oncancel
	}: Props = $props();

	let cancelButton: HTMLElement | null = $state(null);

	onMount(() => {
		// Focus the safe choice, like a system alert.
		cancelButton?.querySelector('button')?.focus();
		return () => {
			if (!returnFocusTo) return;
			// The opener is rendered again once this closes; focus it on the next frame.
			requestAnimationFrame(() => document.getElementById(returnFocusTo)?.focus());
		};
	});
</script>

<div
	class="bg-fill rounded-control mt-2.5 p-3.5"
	role="alertdialog"
	aria-modal="false"
	aria-label={confirmLabel}
	tabindex="-1"
	onkeydown={(e) => {
		if (e.key === 'Escape') {
			e.stopPropagation();
			oncancel();
		}
	}}
>
	<p class="text-[0.875rem] leading-snug">{message}</p>
	<div class="mt-3 flex justify-end gap-2">
		<span bind:this={cancelButton}>
			<Btn size="small" variant="outline" color="secondary" onclick={oncancel}>Cancel</Btn>
		</span>
		<Btn size="small" color={danger ? 'danger' : 'primary'} onclick={onconfirm}>{confirmLabel}</Btn>
	</div>
</div>
