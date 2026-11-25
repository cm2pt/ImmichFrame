<script lang="ts">
	import PopupSelect from './popup-select.svelte';
	import { Input } from '@immich/ui';
	import type { ServerAccountSettings } from '$lib/immichFrameApi';
	import { accountDefaults, effective } from './admin-defaults';
	import { formatCount, libraryFor } from './admin-library.svelte';
	import ItemList from './item-list.svelte';
	import NeverShow from './never-show.svelte';
	import PageCard from './page-card.svelte';
	import Segmented from './segmented.svelte';
	import SettingRow from './setting-row.svelte';
	import Toggle from './toggle.svelte';

	interface Props {
		account: ServerAccountSettings;
		/** Its place in the saved settings, for faces and album covers. */
		accountIndex?: number | null;
	}

	let { account, accountIndex = null }: Props = $props();

	const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

	let library = $derived(libraryFor(account));
	let loading = $derived(library.state === 'loading');
	const get = <K extends keyof ServerAccountSettings>(key: K) =>
		effective(account, accountDefaults, key);

	let hasSource = $derived(
		get('showFavorites') ||
			get('showAssetsInAlbums') ||
			(account.albums?.length ?? 0) > 0 ||
			(account.people?.length ?? 0) > 0 ||
			(account.tags?.length ?? 0) > 0
	);

	let includeFooter = $derived.by(() => {
		if (!hasSource)
			return get('showMemories')
				? 'Only memories: nothing else is picked.'
				: 'Nothing is picked, so the frame shows the whole library.';
		return 'Each source adds photos: a photo shows if it matches any of them.';
	});

	// One album, person or tag can't be both shown and hidden: each list marks the other's.
	const marks = (ids: readonly unknown[] | null | undefined, reason: string) =>
		new Map((ids ?? []).map((id) => [String(id).toLowerCase(), reason]));
	let includedMap = $derived(marks(account.albums, 'Shown'));
	let excludedMap = $derived(marks(account.excludedAlbums, 'In Never show'));
	let includedPeopleMap = $derived(marks(account.people, 'Shown'));
	let excludedPeopleMap = $derived(marks(account.excludedPeople, 'In Never show'));
	let includedTagsMap = $derived(marks(account.tags, 'Shown'));
	let excludedTagsMap = $derived(marks(account.excludedTags, 'In Never show'));

	// "Taken" maps onto ImagesFromDays / ImagesFromDate / ImagesUntilDate.
	type Taken = 'any' | '365' | '1825' | 'custom';
	let takenCustom = $state(false);
	let taken: Taken = $derived.by(() => {
		if (takenCustom || account.imagesFromDate || account.imagesUntilDate) return 'custom';
		if (account.imagesFromDays == null) return 'any';
		if (account.imagesFromDays === 365) return '365';
		if (account.imagesFromDays === 1825) return '1825';
		return 'custom';
	});

	function setTaken(value: Taken) {
		takenCustom = value === 'custom';
		if (value === 'custom') return;
		account.imagesFromDate = null;
		account.imagesUntilDate = null;
		account.imagesFromDays = value === 'any' ? null : Number(value);
	}

	// Custom is either a moving window ("the last 90 days") or fixed dates, never both: the
	// server lets a From date override the days, which a page showing both couldn't explain.
	let customDatesPicked = $state(false);
	let customMode: 'days' | 'dates' = $derived(
		customDatesPicked || account.imagesFromDate || account.imagesUntilDate ? 'dates' : 'days'
	);
	function setCustomMode(mode: 'days' | 'dates') {
		customDatesPicked = mode === 'dates';
		if (mode === 'dates') account.imagesFromDays = null;
		else {
			account.imagesFromDate = null;
			account.imagesUntilDate = null;
		}
	}

	const dateOnly = (iso: string | null | undefined) => (iso ?? '').substring(0, 10);
	const toIso = (raw: string) => (raw === '' ? null : new Date(raw).toISOString());
</script>

<PageCard title="Include" footer={includeFooter}>
	<SettingRow
		title="Favorites"
		description={library.favoriteCount != null
			? `${formatCount(library.favoriteCount)} photos with a heart`
			: 'Photos with a heart'}
	>
		{#snippet control()}
			<Toggle
				aria-label="Favorites"
				checked={get('showFavorites')}
				onCheckedChange={(v) => (account.showFavorites = v)}
			/>
		{/snippet}
	</SettingRow>
	<SettingRow
		title="In any album"
		description="Every photo you’ve put in an album, new albums included"
	>
		{#snippet control()}
			<Toggle
				aria-label="In any album"
				checked={get('showAssetsInAlbums')}
				onCheckedChange={(v) => (account.showAssetsInAlbums = v)}
			/>
		{/snippet}
	</SettingRow>
	<SettingRow
		title="Memories"
		description="On this day and birthdays from past years, refreshed daily"
	>
		{#snippet control()}
			<Toggle
				aria-label="Memories"
				checked={get('showMemories')}
				onCheckedChange={(v) => (account.showMemories = v)}
			/>
		{/snippet}
	</SettingRow>
	{#if get('showMemories')}
		<SettingRow title="On this day" nested>
			{#snippet control()}
				<Toggle
					aria-label="On this day"
					checked={get('showOnThisDayMemories')}
					onCheckedChange={(v) => (account.showOnThisDayMemories = v)}
				/>
			{/snippet}
		</SettingRow>
		<SettingRow title="Birthdays" description="People with a birth date in Immich" nested>
			{#snippet control()}
				<Toggle
					aria-label="Birthdays"
					checked={get('showBirthdayMemories')}
					onCheckedChange={(v) => (account.showBirthdayMemories = v)}
				/>
			{/snippet}
		</SettingRow>
	{/if}
</PageCard>

<PageCard
	title="Albums"
	footer={get('showAssetsInAlbums')
		? '“In any album” is on, so every album already shows.'
		: 'Every photo in these albums, favorite or not.'}
>
	<!-- With "In any album" on, picking albums adds nothing: shown dimmed and inert. -->
	<div
		inert={get('showAssetsInAlbums')}
		class="transition-opacity {get('showAssetsInAlbums') ? 'opacity-40' : ''}"
	>
		<ItemList
			noun="Album"
			{accountIndex}
			selected={(account.albums ?? []) as string[]}
			items={library.albums}
			{loading}
			usedElsewhere={excludedMap}
			manualPattern={guid}
			onchange={(ids) => (account.albums = ids)}
		/>
	</div>
</PageCard>

<PageCard title="People" footer="Any photo with one of them in it.">
	<ItemList
		noun="Person"
		{accountIndex}
		selected={(account.people ?? []) as string[]}
		items={library.people}
		{loading}
		usedElsewhere={excludedPeopleMap}
		manualPattern={guid}
		onchange={(ids) => (account.people = ids)}
	/>
</PageCard>

<PageCard title="Tags" footer="Photos with any of these tags.">
	<ItemList
		noun="Tag"
		glyph="tag"
		selected={account.tags ?? []}
		items={library.tags}
		{loading}
		usedElsewhere={excludedTagsMap}
		manualPlaceholder="Tag, e.g. Family/Holidays"
		onchange={(ids) => (account.tags = ids)}
	/>
</PageCard>

<PageCard
	title="Never show"
	footer="Photos in these albums, with these people or with these tags never show. Memories ignore this."
>
	<NeverShow
		{account}
		{library}
		{loading}
		{accountIndex}
		shown={{ albums: includedMap, people: includedPeopleMap, tags: includedTagsMap }}
	/>
</PageCard>

<PageCard title="Narrow down" footer="These apply to everything above except memories.">
	<SettingRow title="Taken">
		{#snippet control()}
			<Segmented
				label="Taken"
				value={taken}
				options={[
					{ value: 'any', label: 'Any Time' },
					{ value: '365', label: 'Past Year' },
					{ value: '1825', label: 'Past 5 Years' },
					{ value: 'custom', label: 'Custom' }
				]}
				onchange={setTaken}
			/>
		{/snippet}
	</SettingRow>
	{#if taken === 'custom'}
		<SettingRow title="Range" nested>
			{#snippet control()}
				<Segmented
					label="Range"
					value={customMode}
					options={[
						{ value: 'days', label: 'Last Days' },
						{ value: 'dates', label: 'Between Dates' }
					]}
					onchange={setCustomMode}
				/>
			{/snippet}
		</SettingRow>
		{#if customMode === 'days'}
			<SettingRow title="Taken in the last" nested>
				{#snippet control()}
					<span class="flex items-center gap-2">
						<Input
							type="number"
							min="1"
							placeholder="90"
							aria-label="Number of days"
							class="w-24 text-right"
							value={account.imagesFromDays?.toString() ?? ''}
							oninput={(e) => {
								const v = e.currentTarget.value;
								account.imagesFromDays = v === '' ? null : Number(v);
							}}
						/>
						<span class="text-label-2 text-[0.875rem]">days</span>
					</span>
				{/snippet}
			</SettingRow>
		{:else}
			<SettingRow title="From" nested>
				{#snippet control()}
					<Input
						type="date"
						aria-label="From"
						value={dateOnly(account.imagesFromDate)}
						oninput={(e) => (account.imagesFromDate = toIso(e.currentTarget.value))}
					/>
				{/snippet}
			</SettingRow>
			<SettingRow title="Until" nested>
				{#snippet control()}
					<Input
						type="date"
						aria-label="Until"
						value={dateOnly(account.imagesUntilDate)}
						oninput={(e) => (account.imagesUntilDate = toIso(e.currentTarget.value))}
					/>
				{/snippet}
			</SettingRow>
		{/if}
	{/if}
	<SettingRow title="Minimum rating">
		{#snippet control()}
			<PopupSelect
				label="Minimum rating"
				options={[
					{ value: '0', label: 'Any' },
					{ value: '1', label: '★ and up' },
					{ value: '2', label: '★★ and up' },
					{ value: '3', label: '★★★ and up' },
					{ value: '4', label: '★★★★ and up' },
					{ value: '5', label: '★★★★★' }
				]}
				value={String(account.rating ?? 0)}
				onchange={(v) => (account.rating = v === '0' ? null : Number(v))}
			/>
		{/snippet}
	</SettingRow>
	<SettingRow title="Videos" description="Play them, then move on">
		{#snippet control()}
			<Toggle
				aria-label="Videos"
				checked={get('showVideos')}
				onCheckedChange={(v) => (account.showVideos = v)}
			/>
		{/snippet}
	</SettingRow>
	<SettingRow title="Archived photos" description="Photos hidden from the Immich timeline">
		{#snippet control()}
			<Toggle
				aria-label="Archived photos"
				checked={get('showArchived')}
				onCheckedChange={(v) => (account.showArchived = v)}
			/>
		{/snippet}
	</SettingRow>
</PageCard>
