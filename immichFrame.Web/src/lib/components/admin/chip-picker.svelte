<script lang="ts">
	import Btn from './btn.svelte';
	import { mdiClose, mdiPlus } from '@mdi/js';
	import { Icon, Input, Text } from '@immich/ui';

	export interface PickerItem {
		id: string;
		name: string;
		/** e.g. a photo count */
		detail?: string;
	}

	interface Props {
		/** The selected ids (album/person GUIDs, or tag values). */
		selected: string[];
		/** What can be picked; null while loading or when the server couldn't list it. */
		items: PickerItem[] | null;
		/** True while the list is still loading (chips show a placeholder, not raw ids). */
		loading?: boolean;
		addLabel: string;
		emptyHint?: string;
		/** Ids already used elsewhere (e.g. in Never show), with the reason shown in the list. */
		usedElsewhere?: Map<string, string>;
		/** Validates a hand-typed value (used when `items` is null). */
		manualPattern?: RegExp;
		manualPlaceholder?: string;
		onchange: (ids: string[]) => void;
	}

	let {
		selected,
		items,
		loading = false,
		usedElsewhere,
		addLabel,
		emptyHint,
		manualPattern,
		manualPlaceholder = 'Paste an ID',
		onchange
	}: Props = $props();

	let open = $state(false);
	let query = $state('');
	let manual = $state('');
	let manualError = $state('');

	const byId = $derived(new Map((items ?? []).map((item) => [item.id.toLowerCase(), item])));
	const matches = $derived(
		(items ?? [])
			.filter((item) => !selected.some((id) => id.toLowerCase() === item.id.toLowerCase()))
			.filter((item) => item.name.toLowerCase().includes(query.trim().toLowerCase()))
			.slice(0, 50)
	);

	function add(id: string) {
		onchange([...selected, id]);
		query = '';
	}

	function remove(id: string) {
		onchange(selected.filter((s) => s !== id));
	}

	function addManual() {
		const value = manual.trim();
		if (!value) return;
		if (manualPattern && !manualPattern.test(value)) {
			manualError = 'That doesn’t look like a valid ID.';
			return;
		}
		manualError = '';
		manual = '';
		if (!selected.includes(value)) add(value);
	}

	function onKeydown(event: KeyboardEvent) {
		if (event.key === 'Escape') open = false;
	}
</script>

<div class="flex flex-wrap items-center gap-2">
	{#each selected as id (id)}
		{@const item = byId.get(id.toLowerCase())}
		<span
			class="inline-flex items-center gap-2 rounded-full bg-subtle/70 py-1 pr-1 pl-3 text-sm dark:bg-light-200"
			title={id}
		>
			<span class="font-medium"
				>{item?.name ?? (items ? 'Unknown (' + id.slice(0, 8) + '…)' : id)}</span
			>
			{#if item?.detail}<span class="text-xs text-label-2 tabular-nums">{item.detail}</span>{/if}
			<button
				type="button"
				class="rounded-full p-1 hover:bg-light-300"
				aria-label="Remove {item?.name ?? id}"
				onclick={() => remove(id)}
			>
				<Icon icon={mdiClose} size="14" />
			</button>
		</span>
	{/each}

	<Btn
		size="small"
		variant="ghost"
		leadingIcon={mdiPlus}
		class="rounded-full bg-primary/10 hover:bg-primary/15"
		aria-expanded={open}
		onclick={() => (open = !open)}
	>
		{addLabel}
	</Btn>
</div>

{#if selected.length === 0 && emptyHint && !open}
	<Text size="small" class="text-label-2 mt-2">{emptyHint}</Text>
{/if}

{#if open}
	<!-- svelte-ignore a11y_no_static_element_interactions -->
	<div class="mt-3 rounded-2xl border border-light-300 bg-light-50 p-3" onkeydown={onKeydown}>
		<div class="mb-2 flex items-center justify-between">
			<Text size="small" fontWeight="semi-bold">{addLabel}</Text>
			<Btn size="small" variant="ghost" onclick={() => (open = false)}>Done</Btn>
		</div>
		{#if loading}
			<Text class="text-label-2" size="small">Loading from Immich…</Text>
		{:else if items}
			<Input placeholder="Search" bind:value={query} aria-label="Search {addLabel}" autofocus />
			<ul class="mt-2 max-h-64 overflow-y-auto">
				{#each matches as item (item.id)}
					{@const elsewhere = usedElsewhere?.get(item.id.toLowerCase())}
					<li>
						<button
							type="button"
							class="flex w-full items-center justify-between gap-3 rounded-lg px-3 py-2 text-left hover:bg-light-200 disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:bg-transparent"
							disabled={!!elsewhere}
							onclick={() => add(item.id)}
						>
							<span class="min-w-0 truncate">{item.name}</span>
							<span class="shrink-0 text-xs text-label-2 tabular-nums"
								>{elsewhere ?? item.detail ?? ''}</span
							>
						</button>
					</li>
				{:else}
					<li><Text size="small" class="text-label-2 px-3 py-2">Nothing matches.</Text></li>
				{/each}
			</ul>
		{:else}
			<Text size="small" class="text-label-2 mb-2">
				The list from Immich isn't available, so paste the ID instead (it's in the address bar when
				you open the album or person in Immich).
			</Text>
			<form
				class="flex gap-2"
				onsubmit={(e) => {
					e.preventDefault();
					addManual();
				}}
			>
				<Input placeholder={manualPlaceholder} bind:value={manual} aria-label={manualPlaceholder} />
				<Btn type="submit" size="small">Add</Btn>
			</form>
			{#if manualError}<Text size="small" class="text-danger-700 dark:text-danger-400 mt-1"
					>{manualError}</Text
				>{/if}
		{/if}
	</div>
{/if}
