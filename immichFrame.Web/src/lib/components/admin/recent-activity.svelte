<script lang="ts">
	import { onMount } from 'svelte';
	import { Button, Card, CardBody, Text } from '@immich/ui';
	import type { SettingsHistoryEntryDto } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import { relativeTime } from './admin-format';

	interface Props {
		/** Bumped after each save so the list refreshes. */
		refreshKey: number;
		dirty: boolean;
		onrestored: () => void;
	}

	let { refreshKey, dirty, onrestored }: Props = $props();

	let entries: SettingsHistoryEntryDto[] | null = $state(null);
	let error = $state('');
	let busy: number | null = $state(null);

	async function load() {
		error = '';
		try {
			const res = await adminApi.getSettingsHistory();
			if (res.status !== 200) throw new Error();
			entries = res.data;
		} catch {
			error = 'Could not load the history.';
		}
	}

	onMount(load);
	$effect(() => {
		void refreshKey;
		load();
	});

	// Undoing a save = going back to the version before it.
	async function undo(index: number) {
		const previous = entries?.[index + 1];
		if (!previous?.version) return;
		if (dirty && !confirm('You have unsaved changes. Undo anyway and lose them?')) return;
		busy = index;
		try {
			const res = await adminApi.restoreSettingsVersion(previous.version);
			if (res.status !== 200) throw new Error();
			onrestored();
		} catch {
			error = 'Undo failed.';
		} finally {
			busy = null;
		}
	}
</script>

<Card>
	<CardBody>
		<Text fontWeight="semi-bold">Recent changes</Text>
		<Text size="small" color="muted" class="mb-2">Each save can be undone from here.</Text>
		{#if error}
			<Text size="small" color="danger"
				>{error} <button class="underline" onclick={load}>Retry</button></Text
			>
		{:else if entries === null}
			<Text size="small" color="muted">Loading…</Text>
		{:else}
			<ul>
				{#each entries.slice(0, 10) as entry, index (entry.version)}
					<li class="flex items-start gap-3 border-t border-light-200 py-3 first:border-t-0">
						<span
							class="mt-1.5 h-2 w-2 shrink-0 rounded-full {index === 0
								? 'bg-success'
								: 'bg-primary'}"
						></span>
						<div class="min-w-0 flex-1">
							<Text size="small" fontWeight="semi-bold">
								{entry.changes?.length ? entry.changes.slice(0, 3).join('; ') : 'Settings saved'}
								{#if (entry.changes?.length ?? 0) > 3}<span class="text-muted">
										+{entry.changes!.length - 3} more</span
									>{/if}
							</Text>
							<Text size="tiny" color="muted">
								{index === 0 ? 'Current · ' : ''}{relativeTime(
									new Date(entry.savedAtUtc!).getTime()
								)}
							</Text>
						</div>
						{#if index < entries.length - 1}
							<Button
								size="tiny"
								variant="outline"
								loading={busy === index}
								onclick={() => undo(index)}>Undo</Button
							>
						{/if}
					</li>
				{/each}
			</ul>
		{/if}
	</CardBody>
</Card>
