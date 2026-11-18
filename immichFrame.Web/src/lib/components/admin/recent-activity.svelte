<script lang="ts">
	import { onMount } from 'svelte';
	import { Button, Card, CardBody, Text } from '@immich/ui';
	import type { ServerAccountSettings, SettingsHistoryEntryDto } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import { relativeTime } from './admin-format';
	import { humanizeChange } from './admin-history';
	import { peekLibrary } from './admin-library.svelte';

	interface Props {
		/** Bumped after each save so the list refreshes. */
		refreshKey: number;
		dirty: boolean;
		accounts: ServerAccountSettings[];
		onrestored: () => void;
	}

	let { refreshKey, dirty, accounts, onrestored }: Props = $props();

	let entries: SettingsHistoryEntryDto[] | null = $state(null);
	let error = $state('');
	let busy: number | null = $state(null);
	let confirming: number | null = $state(null);
	let showAll = $state(false);

	let names = $derived(accounts.map((a, i) => peekLibrary(a)?.userName || `Account ${i + 1}`));

	async function load() {
		error = '';
		try {
			const res = await adminApi.getSettingsHistory();
			if (res.status !== 200) throw new Error();
			entries = res.data;
		} catch {
			error = 'Couldn’t load the history.';
		}
	}

	onMount(load);
	$effect(() => {
		void refreshKey;
		load();
	});

	/** Going back to the version saved just before entry `index`. */
	async function restoreBefore(index: number) {
		const target = entries?.[index + 1];
		if (!target?.version) return;
		busy = index;
		confirming = null;
		try {
			const res = await adminApi.restoreSettingsVersion(target.version);
			if (res.status !== 200) throw new Error();
			onrestored();
		} catch {
			error = 'That didn’t work. Try again.';
		} finally {
			busy = null;
		}
	}

	function summary(entry: SettingsHistoryEntryDto) {
		const lines = (entry.changes ?? []).map((c) => humanizeChange(c, names));
		return lines.length ? lines : ['Saved with no changes'];
	}
</script>

<Card>
	<CardBody>
		<Text fontWeight="semi-bold">Recent changes</Text>
		<Text size="small" class="mb-3 text-dark/70">Every save, newest first.</Text>
		{#if error}
			<div class="flex items-center gap-2">
				<Text size="small" color="danger" class="flex-1">{error}</Text>
				<Button size="tiny" variant="outline" onclick={load}>Retry</Button>
			</div>
		{:else if entries === null}
			<div class="flex flex-col gap-3" aria-busy="true">
				{#each [0, 1, 2] as i (i)}<div
						class="h-10 animate-pulse rounded-lg bg-subtle/50 dark:bg-light-100"
					></div>{/each}
			</div>
		{:else}
			<ul>
				{#each showAll ? entries : entries.slice(0, 6) as entry, index (entry.version)}
					{@const lines = summary(entry)}
					<li class="border-t border-light-200 py-3 first:border-t-0">
						<div class="flex items-start gap-3">
							<span
								class="mt-1.5 h-2 w-2 shrink-0 rounded-full {index === 0
									? 'bg-success'
									: 'bg-light-400'}"
							></span>
							<div class="min-w-0 flex-1">
								<Text size="small" fontWeight="medium">{lines[0]}</Text>
								{#each lines.slice(1, 3) as line (line)}<Text size="small" class="text-dark/70"
										>{line}</Text
									>{/each}
								{#if lines.length > 3}<Text size="small" class="text-dark/60"
										>and {lines.length - 3} more</Text
									>{/if}
								<Text size="tiny" class="mt-0.5 text-dark/60">
									{index === 0 ? 'Current · ' : ''}{relativeTime(
										new Date(entry.savedAtUtc!).getTime()
									)}
								</Text>
							</div>
							{#if index < entries.length - 1 && confirming !== index}
								<Button
									size="tiny"
									variant="outline"
									color="secondary"
									loading={busy === index}
									onclick={() => (confirming = index)}
								>
									{index === 0 ? 'Undo' : 'Go back…'}
								</Button>
							{/if}
						</div>
						{#if confirming === index}
							<div
								class="mt-2 ml-5 rounded-xl bg-subtle/50 p-3 dark:bg-light-100"
								role="alertdialog"
								aria-label="Confirm"
							>
								<Text size="small">
									{index === 0
										? 'Undo this save?'
										: `Go back to how things were before this? The ${index + 1} newest saves are undone.`}
									{dirty ? ' Your unsaved changes are lost.' : ''}
								</Text>
								<div class="mt-2 flex justify-end gap-2">
									<Button
										size="tiny"
										variant="ghost"
										color="secondary"
										onclick={() => (confirming = null)}>Cancel</Button
									>
									<Button size="tiny" onclick={() => restoreBefore(index)}
										>{index === 0 ? 'Undo' : 'Go back'}</Button
									>
								</div>
							</div>
						{/if}
					</li>
				{/each}
			</ul>
			{#if entries.length > 6 && !showAll}
				<Button size="tiny" variant="ghost" class="mt-1" onclick={() => (showAll = true)}
					>Show all {entries.length}</Button
				>
			{/if}
			<Text size="tiny" class="mt-2 text-dark/60">Going back never changes the admin password.</Text
			>
		{/if}
	</CardBody>
</Card>
