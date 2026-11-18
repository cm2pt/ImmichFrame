<script lang="ts">
	import type { Snippet } from 'svelte';
	import { mdiAlertCircleOutline, mdiAlertOutline, mdiInformationOutline } from '@mdi/js';
	import { Icon } from '@immich/ui';

	interface Props {
		tone?: 'warning' | 'danger' | 'info';
		children: Snippet;
	}

	let { tone = 'warning', children }: Props = $props();

	// The tint and icon carry the color; the words stay dark so they always read at 4.5:1.
	const icons = {
		warning: mdiAlertOutline,
		danger: mdiAlertCircleOutline,
		info: mdiInformationOutline
	};
	const tints = { warning: 'bg-warning/12', danger: 'bg-danger/10', info: 'bg-primary/8' };
	const iconColors = { warning: 'text-warning', danger: 'text-danger', info: 'text-primary' };
</script>

<div
	class="flex gap-3 rounded-xl px-4 py-3 text-sm text-dark {tints[tone]}"
	role={tone === 'info' ? 'note' : 'alert'}
>
	<Icon icon={icons[tone]} size="18" class="mt-0.5 shrink-0 {iconColors[tone]}" />
	<div class="min-w-0 [&_code]:break-all">{@render children()}</div>
</div>
