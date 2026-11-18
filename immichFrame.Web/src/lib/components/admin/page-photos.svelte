<script lang="ts">
	import { mdiPlus } from '@mdi/js';
	import { Card, CardBody, Icon, Text } from '@immich/ui';
	import type { ServerSettings } from '$lib/immichFrameApi';
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
						<Text class="text-dark/70" size="small">photos in rotation, plus memories</Text>
					</div>
					{#each libraries as library, i (i)}
						<div>
							<Text size="large" fontWeight="bold">
								{matching[i] != null ? formatCount(matching[i]) : '–'}
							</Text>
							<Text class="text-dark/70" size="small"
								>from {library.userName || `Account ${i + 1}`}</Text
							>
						</div>
					{/each}
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
		class="flex items-center justify-center gap-2 rounded-2xl border border-light-300 p-5 font-medium text-primary transition-colors hover:bg-primary/5 focus-visible:outline-2 focus-visible:outline-primary"
		onclick={onaddaccount}
	>
		<Icon icon={mdiPlus} size="20" />
		Add an Immich account
	</button>
</div>
