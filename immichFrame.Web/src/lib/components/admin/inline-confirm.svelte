<script lang="ts">
	import Btn from './btn.svelte';
	import { onMount } from 'svelte';
	import { Text } from '@immich/ui';

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
	class="mt-3 rounded-xl bg-subtle/60 p-4 dark:bg-light-100"
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
	<Text size="small">{message}</Text>
	<div class="mt-3 flex justify-end gap-2">
		<span bind:this={cancelButton}>
			<Btn size="small" variant="ghost" color="secondary" onclick={oncancel}>Cancel</Btn>
		</span>
		<Btn size="small" color={danger ? 'danger' : 'primary'} onclick={onconfirm}>{confirmLabel}</Btn>
	</div>
</div>
