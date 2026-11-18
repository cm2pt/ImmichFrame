<script lang="ts">
	import Btn from './btn.svelte';
	import { onMount } from 'svelte';
	import { Text, Heading } from '@immich/ui';
	import type { ServerAccountSettings, SettingsHistoryEntryDto } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import { relativeTime } from './admin-format';
	import { humanizeChange } from './admin-history';
	import { peekLibrary } from './admin-library.svelte';
	import InlineConfirm from './inline-confirm.svelte';

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

<div class="bg-group rounded-2xl shadow-[0_1px_2px_rgba(0,0,0,0.04)]">
	<div class="p-4">
		<Heading tag="h2" size="tiny" fontWeight="semi-bold">Recent changes</Heading>
		<Text size="small" class="mb-3 text-label-2">Every save, newest first.</Text>
		{#if error}
			<div class="flex items-center gap-2">
				<Text size="small" class="text-danger-700 dark:text-danger-400 flex-1">{error}</Text>
				<Btn size="tiny" variant="outline" onclick={load}>Retry</Btn>
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
								{#each lines.slice(0, 3) as line (line)}<Text size="small" fontWeight="medium"
										>{line}</Text
									>{/each}
								{#if lines.length > 3}<Text size="small" class="text-label-2"
										>and {lines.length - 3} more</Text
									>{/if}
								<Text size="tiny" class="mt-0.5 text-label-2">
									{index === 0 ? 'Current\u00a0·\u2002' : ''}{relativeTime(
										new Date(entry.savedAtUtc!).getTime()
									)}
								</Text>
							</div>
							{#if index < entries.length - 1 && confirming !== index}
								<Btn
									id={`history-${entry.version}`}
									size="tiny"
									variant="outline"
									color="secondary"
									loading={busy === index}
									onclick={() => (confirming = index)}
								>
									{index === 0 ? 'Undo' : 'Restore…'}
								</Btn>
							{/if}
						</div>
						{#if confirming === index}
							<InlineConfirm
								message={(index === 0
									? 'Undo the last save?'
									: `Restore the settings from ${relativeTime(new Date(entries[index + 1].savedAtUtc!).getTime())}? The ${index + 1} saves after it are undone.`) +
									(dirty ? ' Your unsaved changes are lost too.' : '')}
								confirmLabel={index === 0 ? 'Undo' : 'Restore'}
								returnFocusTo={`history-${entry.version}`}
								onconfirm={() => restoreBefore(index)}
								oncancel={() => (confirming = null)}
							/>
						{/if}
					</li>
				{/each}
			</ul>
			{#if entries.length > 6 && !showAll}
				<Btn size="tiny" variant="ghost" class="mt-1" onclick={() => (showAll = true)}
					>Show all {entries.length}</Btn
				>
			{/if}
			<Text size="tiny" class="mt-2 text-label-2">Restoring never changes the admin password.</Text>
		{/if}
	</div>
</div>
