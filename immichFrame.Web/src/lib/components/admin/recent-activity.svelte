<script lang="ts">
	import Btn from './btn.svelte';
	import { onMount } from 'svelte';
	import PageCard from './page-card.svelte';
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

<PageCard
	title="Recent changes"
	footer="Every save, newest first. Restoring never changes the admin password."
>
	{#if error}
		<div class="flex min-h-11 items-center justify-between gap-2 py-2">
			<p class="text-[0.875rem]">{error}</p>
			<Btn size="small" variant="ghost" color="primary" onclick={load}>Try Again</Btn>
		</div>
	{:else if entries === null}
		{#each [0, 1, 2] as i (i)}
			<div
				class="border-sep flex min-h-12 items-center border-t py-2 first:border-t-0"
				aria-busy="true"
			>
				<span class="bg-fill h-3 w-48 animate-pulse rounded"></span>
			</div>
		{/each}
	{:else}
		{#each showAll ? entries : entries.slice(0, 6) as entry, index (entry.version)}
			{@const lines = summary(entry)}
			<div class="border-sep border-t py-2.5 first:border-t-0">
				<div class="flex items-center gap-3">
					<div class="min-w-0 flex-1">
						{#each lines.slice(0, 3) as line (line)}
							<p class="text-[0.9375rem] leading-snug md:text-[0.875rem]">{line}</p>
						{/each}
						{#if lines.length > 3}<p class="text-label-2 text-[0.8125rem]">
								and {lines.length - 3} more
							</p>{/if}
						<p class="text-label-2 mt-0.5 text-[0.8125rem] md:text-[0.75rem]">
							{index === 0 ? 'Current\u00a0·\u2002' : ''}{relativeTime(
								new Date(entry.savedAtUtc!).getTime()
							)}
						</p>
					</div>
					{#if index < entries.length - 1 && confirming !== index}
						<Btn
							id={`history-${entry.version}`}
							size="small"
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
			</div>
		{/each}
		{#if entries.length > 6 && !showAll}
			<button
				type="button"
				class="border-sep flex min-h-11 w-full items-center border-t py-2 text-left text-[0.875rem] text-primary"
				onclick={() => (showAll = true)}>Show All {entries.length}</button
			>
		{/if}
	{/if}
</PageCard>
