<script lang="ts">
	import { mdiPlus } from '@mdi/js';
	import { Button, Card, CardBody, Text } from '@immich/ui';
	import type { ServerSettings } from '$lib/immichFrameApi';
	import { effective, generalDefaults } from './admin-defaults';
	import { formatCount, libraryFor } from './admin-library.svelte';
	import AccountPhotos from './account-photos.svelte';

	interface Props {
		settings: ServerSettings;
		version: number;
		onaddaccount: () => void;
	}

	let { settings, version, onaddaccount }: Props = $props();

	let accounts = $derived(settings.Accounts ?? []);
	let libraries = $derived(accounts.map((account) => libraryFor(account)));
	let matching: (number | null)[] = $state([]);
	let total = $derived(
		matching.length && matching.every((n) => n != null)
			? matching.reduce<number>((sum, n) => sum + (n ?? 0), 0)
			: null
	);
	let perHour = $derived.by(() => {
		const general = settings.General ?? {};
		const interval = effective(general, generalDefaults, 'interval') ?? 45;
		const split = effective(general, generalDefaults, 'layout') === 'splitview';
		return Math.round(3600 / Math.max(interval, 1)) * (split ? 2 : 1);
	});
</script>

<div class="flex flex-col gap-6">
	{#if accounts.length}
		<Card>
			<CardBody>
				<div class="flex flex-wrap items-end gap-x-10 gap-y-4">
					<div class="border-light-200 pr-10 sm:border-r">
						<Text class="text-4xl font-bold text-primary"
							>{total != null ? formatCount(total) : '…'}</Text
						>
						<Text size="small" color="muted">photos in rotation, plus memories</Text>
					</div>
					{#each libraries as library, i (i)}
						<div>
							<Text size="large" fontWeight="bold">
								{matching[i] != null ? formatCount(matching[i]) : '–'}
							</Text>
							<Text size="small" color="muted">from {library.userName || `Account ${i + 1}`}</Text>
						</div>
					{/each}
					<div>
						<Text size="large" fontWeight="bold">{perHour}</Text>
						<Text size="small" color="muted">photos an hour</Text>
					</div>
				</div>
			</CardBody>
		</Card>
	{/if}

	{#key version}
		{#each accounts as account, index (index)}
			<AccountPhotos
				{account}
				{index}
				expanded={index === 0}
				onmatching={(count) => (matching[index] = count)}
			/>
		{/each}
	{/key}

	<button
		type="button"
		class="flex items-center justify-center gap-2 rounded-2xl border-2 border-dashed border-light-300 p-5 font-medium text-primary hover:border-primary/60"
		onclick={onaddaccount}
	>
		<Button size="small" variant="ghost" leadingIcon={mdiPlus} tabindex={-1}
			>Add an Immich account</Button
		>
	</button>
</div>
