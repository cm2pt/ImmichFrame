<script lang="ts">
	import Btn from './btn.svelte';
	import { onMount } from 'svelte';
	import PageCard from './page-card.svelte';
	import type { ServerAccountSettings, SettingsHistoryEntryDto } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import { savedAt } from './admin-format';
	import { humanizeChange } from './admin-history';
	import { accountLabel, peekLibrary } from './admin-library.svelte';
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

	let names = $derived(accounts.map((a, i) => accountLabel(a, i, peekLibrary(a))));

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

	/**
	 * Rows are versions, newest first, as in Time Machine: each says what changed and when, and
	 * Restore… brings back the settings as they were right after it. A run of saves that only
	 * flipped the same setting back and forth is one row.
	 */
	interface Row {
		version: number;
		savedAtUtc: string;
		lines: string[];
		saves: number;
	}
	const arrow = /^(.*): (.*) → (.*)$/;
	let rows: Row[] = $derived.by(() => {
		const out: (Row & { key?: string; from?: string; to?: string })[] = [];
		for (const entry of entries ?? []) {
			const changes = entry.changes ?? [];
			const m = changes.length === 1 ? arrow.exec(changes[0]) : null;
			const last = out.at(-1);
			if (m && last?.key === m[1]) {
				// Older save of the same setting: the run now starts from its "from" value.
				last.from = m[2];
				last.saves += 1;
				const line = `${m[1]}: ${last.from} → ${last.to}`;
				// Changed and changed back: say the run came to nothing ("Full resolution: no net
				// change, still off"), not "off → off".
				last.lines = [
					last.from === last.to
						? `${humanizeChange(`${m[1]}: x → ${last.to}`, names).replace(/: x → (.*)$/, ': no net change, still $1')}`
						: humanizeChange(line, names)
				];
				continue;
			}
			// A save with nothing you'd see counts towards the newer row instead of being its own.
			if (last && summary(entry)[0] === noChange) {
				last.saves += 1;
				continue;
			}
			out.push({
				version: entry.version!,
				savedAtUtc: entry.savedAtUtc!,
				lines: summary(entry),
				saves: 1,
				...(m ? { key: m[1], from: m[2], to: m[3] } : {})
			});
		}
		return out;
	});

	async function restore(row: Row) {
		busy = row.version;
		confirming = null;
		try {
			const res = await adminApi.restoreSettingsVersion(row.version);
			if (res.status !== 200) throw new Error();
			onrestored();
		} catch {
			error = 'That didn’t work. Try again.';
		} finally {
			busy = null;
		}
	}

	// Each row can stand for several saves (repeated toggles of one setting), so count saves.
	function restoreMessage(index: number, when: string) {
		const undone = rows.slice(0, index).reduce((n, r) => n + r.saves, 0);
		return (
			`Go back to the settings from ${when}? This undoes ${undone === 1 ? 'the save' : `${undone} saves`} since then.` +
			(dirty ? ' Your unsaved changes are lost too.' : '')
		);
	}

	// A setting going from "not set" to its own default ("off → off") changes nothing you'd see.
	const noChange = 'Saved with no visible changes';
	const noOp = (line: string) => /: (.+) → \1$/.test(line);
	function summary(entry: SettingsHistoryEntryDto) {
		const lines = (entry.changes ?? [])
			.map((c) => humanizeChange(c, names))
			.filter((l) => !noOp(l));
		return lines.length ? lines : [noChange];
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
		{#each showAll ? rows : rows.slice(0, 6) as row, index (row.version)}
			{@const when = savedAt(new Date(row.savedAtUtc).getTime())}
			<div class="border-sep border-t py-2.5 first:border-t-0">
				<div class="flex items-center gap-3">
					<div class="min-w-0 flex-1">
						{#each row.lines.slice(0, 3) as line (line)}
							<p class="text-[0.9375rem] leading-snug md:text-[0.875rem]">{line}</p>
						{/each}
						{#if row.lines.length > 3}<p class="text-label-2 text-[0.8125rem]">
								and {row.lines.length - 3} more
							</p>{/if}
						<p class="text-label-2 mt-0.5 text-[0.8125rem] md:text-[0.75rem]">
							{when}{row.saves > 1 ? `\u00a0·\u2002saved ${row.saves} times` : ''}
						</p>
					</div>
					{#if index === 0}
						<span class="text-label-2 text-[0.8125rem]">Current</span>
					{:else if confirming !== row.version}
						<Btn
							id={`history-${row.version}`}
							size="small"
							variant="outline"
							color="secondary"
							loading={busy === row.version}
							onclick={() => (confirming = row.version)}
						>
							Restore…
						</Btn>
					{/if}
				</div>
				{#if confirming === row.version}
					<InlineConfirm
						message={restoreMessage(index, when)}
						confirmLabel="Restore"
						returnFocusTo={`history-${row.version}`}
						onconfirm={() => restore(row)}
						oncancel={() => (confirming = null)}
					/>
				{/if}
			</div>
		{/each}
		{#if rows.length > 6 && !showAll}
			<button
				type="button"
				class="border-sep flex min-h-11 w-full items-center border-t py-2 text-left text-[0.875rem] text-primary"
				onclick={() => (showAll = true)}>Show All {rows.length}</button
			>
		{/if}
	{/if}
</PageCard>
