<script lang="ts">
	import {
		mdiAccountOutline,
		mdiCalendarHeart,
		mdiChevronDown,
		mdiHeartOutline,
		mdiImageMultipleOutline,
		mdiCancel,
		mdiTagOutline
	} from '@mdi/js';
	import { Avatar, Card, CardBody, Checkbox, Icon, Input, Switch, Text } from '@immich/ui';
	import type { ServerAccountSettings } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import { accountDefaults, effective } from './admin-defaults';
	import { formatCount, libraryFor, hasConnection } from './admin-library.svelte';
	import ChipPicker from './chip-picker.svelte';
	import Segmented from './segmented.svelte';
	import SettingRow from './setting-row.svelte';

	interface Props {
		account: ServerAccountSettings;
		index: number;
		expanded?: boolean;
		/** Reports how many photos this account's choices match (memories excluded). */
		onmatching?: (count: number | null) => void;
	}

	let { account, index, expanded = $bindable(true), onmatching }: Props = $props();

	// The search's own count for exactly these choices, refreshed shortly after each change.
	let matching: number | null = $state(null);
	$effect(() => {
		const snapshot = JSON.stringify(account);
		if (!hasConnection(account)) return;
		const timer = setTimeout(async () => {
			try {
				const res = await adminApi.getAccountLibrary(JSON.parse(snapshot));
				matching = res.status === 200 ? (res.data.matchingAssets ?? null) : null;
			} catch {
				matching = null;
			}
			onmatching?.(matching);
		}, 800);
		return () => clearTimeout(timer);
	});

	const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
	const avatarColors = ['primary', 'orange', 'green', 'purple', 'pink', 'blue'] as const;

	let library = $derived(libraryFor(account));
	let name = $derived(library.userName || `Account ${index + 1}`);
	// Initials of a real name, or just the number for "Account 2".
	let avatarName = $derived(library.userName || String(index + 1));
	const get = <K extends keyof ServerAccountSettings>(key: K) =>
		effective(account, accountDefaults, key);

	let host = $derived.by(() => {
		try {
			return new URL(account.immichServerUrl ?? '').host;
		} catch {
			return account.immichServerUrl ?? '';
		}
	});

	let summary = $derived(
		[
			get('showFavorites') && 'Favourites',
			get('showMemories') && 'Memories',
			(account.albums?.length ?? 0) > 0 && `${account.albums!.length} album(s)`,
			(account.people?.length ?? 0) > 0 && `${account.people!.length} people`,
			(account.excludedAlbums?.length ?? 0) > 0 &&
				`never ${account.excludedAlbums!.length} album(s)`
		]
			.filter(Boolean)
			.join(' · ') || 'The whole library'
	);

	let hasSource = $derived(
		get('showFavorites') ||
			(account.albums?.length ?? 0) > 0 ||
			(account.people?.length ?? 0) > 0 ||
			(account.tags?.length ?? 0) > 0
	);

	// "Taken" presets map onto ImagesFromDays / ImagesFromDate / ImagesUntilDate.
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

	const dateOnly = (iso: string | null | undefined) => (iso ?? '').substring(0, 10);
	const toIso = (raw: string) => (raw === '' ? null : new Date(raw).toISOString());
</script>

<Card>
	<CardBody>
		<button
			type="button"
			class="flex w-full items-center gap-4 text-left"
			aria-expanded={expanded}
			onclick={() => (expanded = !expanded)}
		>
			<Avatar name={avatarName} color={avatarColors[index % avatarColors.length]} />
			<div class="min-w-0 flex-1">
				<Text size="large" fontWeight="semi-bold">{name}</Text>
				<Text size="small" color={library.state === 'error' ? 'danger' : 'muted'} class="truncate">
					{#if !hasConnection(account)}
						Not connected yet. Add it under Access &amp; accounts.
					{:else if library.state === 'loading'}
						Connecting to {host}…
					{:else if library.state === 'error'}
						{library.message}
					{:else if expanded}
						Connected · {host}
					{:else}
						{summary}
					{/if}
				</Text>
			</div>
			{#if matching != null}
				<span
					class="hidden rounded-full bg-primary/10 px-3 py-1 font-mono text-sm text-primary sm:inline"
					title="Photos these choices match, not counting memories"
				>
					{formatCount(matching)} photos
				</span>
			{/if}
			<span class="transition-transform {expanded ? 'rotate-180' : ''}">
				<Icon icon={mdiChevronDown} size="24" />
			</span>
		</button>

		{#if expanded}
			<div class="mt-4">
				<Text size="tiny" fontWeight="bold" color="muted" class="tracking-widest uppercase">
					Include
				</Text>

				<SettingRow
					title="Favourites"
					description="Everything {library.userName
						? `${library.userName} `
						: ''}marked with a heart{library.favoriteCount != null
						? ` · ${formatCount(library.favoriteCount)} photos`
						: ''}"
				>
					{#snippet icon()}<Icon icon={mdiHeartOutline} size="20" />{/snippet}
					{#snippet control()}
						<Switch
							aria-label="Favourites"
							checked={get('showFavorites')}
							onCheckedChange={(v) => (account.showFavorites = v)}
						/>
					{/snippet}
				</SettingRow>

				<SettingRow title="Memories" description="“On this day” from past years, refreshed daily">
					{#snippet icon()}<Icon icon={mdiCalendarHeart} size="20" />{/snippet}
					{#snippet control()}
						<Switch
							aria-label="Memories"
							checked={get('showMemories')}
							onCheckedChange={(v) => (account.showMemories = v)}
						/>
					{/snippet}
					{#if get('showMemories')}
						<div class="flex flex-wrap gap-6 pl-8">
							<label class="flex items-center gap-2 text-sm">
								<Checkbox
									checked={get('showOnThisDayMemories')}
									onCheckedChange={(v) => (account.showOnThisDayMemories = v === true)}
								/>
								On this day
							</label>
							<label class="flex items-center gap-2 text-sm">
								<Checkbox
									checked={get('showBirthdayMemories')}
									onCheckedChange={(v) => (account.showBirthdayMemories = v === true)}
								/>
								Birthdays (people with a birth date in Immich)
							</label>
						</div>
					{/if}
				</SettingRow>

				<SettingRow title="Albums" description="Every photo in these albums, favourite or not">
					{#snippet icon()}<Icon icon={mdiImageMultipleOutline} size="20" />{/snippet}
					<div class="pl-8">
						<ChipPicker
							selected={(account.albums ?? []) as string[]}
							items={library.albums}
							addLabel="Add album"
							manualPattern={guid}
							onchange={(ids) => (account.albums = ids)}
						/>
					</div>
				</SettingRow>

				<SettingRow title="People" description="Any photo with one of them in it">
					{#snippet icon()}<Icon icon={mdiAccountOutline} size="20" />{/snippet}
					<div class="pl-8">
						<ChipPicker
							selected={(account.people ?? []) as string[]}
							items={library.people}
							addLabel="Add people"
							manualPattern={guid}
							onchange={(ids) => (account.people = ids)}
						/>
					</div>
				</SettingRow>

				<SettingRow title="Tags" description="Photos with any of these tags">
					{#snippet icon()}<Icon icon={mdiTagOutline} size="20" />{/snippet}
					<div class="pl-8">
						<ChipPicker
							selected={account.tags ?? []}
							items={library.tags}
							addLabel="Add tag"
							manualPlaceholder="Tag, e.g. Family/Holidays"
							onchange={(ids) => (account.tags = ids)}
						/>
					</div>
				</SettingRow>

				{#if !hasSource}
					<Text size="small" color="muted" class="mb-2 pl-8">
						{get('showMemories')
							? 'Only memories: nothing else is picked.'
							: 'Nothing picked, so the frame shows the whole library.'}
					</Text>
				{/if}

				<Text size="tiny" fontWeight="bold" color="muted" class="mt-6 tracking-widest uppercase">
					Never show
				</Text>
				<SettingRow
					title="Photos in these albums"
					description="Add a photo to one of these albums in Immich to keep it off the frame. Memories ignore this."
				>
					{#snippet icon()}<Icon icon={mdiCancel} size="20" />{/snippet}
					<div class="pl-8">
						<ChipPicker
							selected={(account.excludedAlbums ?? []) as string[]}
							items={library.albums}
							addLabel="Add album"
							manualPattern={guid}
							onchange={(ids) => (account.excludedAlbums = ids)}
						/>
					</div>
				</SettingRow>

				<Text size="tiny" fontWeight="bold" color="muted" class="mt-6 tracking-widest uppercase">
					Narrow down
				</Text>
				<Text size="small" color="muted">These apply to everything above except memories.</Text>

				<SettingRow title="Taken" description="Only photos from this period">
					{#snippet control()}
						<Segmented
							label="Taken"
							value={taken}
							options={[
								{ value: 'any', label: 'Any time' },
								{ value: '365', label: 'Last year' },
								{ value: '1825', label: 'Last 5 years' },
								{ value: 'custom', label: 'Custom…' }
							]}
							onchange={setTaken}
						/>
					{/snippet}
					{#if taken === 'custom'}
						<div class="grid gap-4 sm:grid-cols-3">
							<label class="text-sm">
								<span class="mb-1 block font-medium">Last … days</span>
								<Input
									type="number"
									min="1"
									value={account.imagesFromDays?.toString() ?? ''}
									disabled={!!account.imagesFromDate}
									oninput={(e) => {
										const v = e.currentTarget.value;
										account.imagesFromDays = v === '' ? null : Number(v);
									}}
								/>
							</label>
							<label class="text-sm">
								<span class="mb-1 block font-medium">From</span>
								<Input
									type="date"
									value={dateOnly(account.imagesFromDate)}
									oninput={(e) => (account.imagesFromDate = toIso(e.currentTarget.value))}
								/>
							</label>
							<label class="text-sm">
								<span class="mb-1 block font-medium">Until</span>
								<Input
									type="date"
									value={dateOnly(account.imagesUntilDate)}
									oninput={(e) => (account.imagesUntilDate = toIso(e.currentTarget.value))}
								/>
							</label>
						</div>
						{#if account.imagesFromDate && account.imagesFromDays != null}
							<Text size="small" color="muted" class="mt-1"
								>A From date wins over “last … days”.</Text
							>
						{/if}
					{/if}
				</SettingRow>

				<SettingRow title="Minimum rating" description="Stars set in Immich">
					{#snippet control()}
						<Segmented
							label="Minimum rating"
							value={account.rating ?? 0}
							options={[
								{ value: 0, label: 'Off' },
								{ value: 1, label: '★ 1+' },
								{ value: 2, label: '★ 2+' },
								{ value: 3, label: '★ 3+' },
								{ value: 4, label: '★ 4+' },
								{ value: 5, label: '★ 5' }
							]}
							onchange={(v) => (account.rating = v === 0 ? null : v)}
						/>
					{/snippet}
				</SettingRow>

				<SettingRow title="Videos" description="Play them, then move on">
					{#snippet control()}
						<Switch
							aria-label="Videos"
							checked={get('showVideos')}
							onCheckedChange={(v) => (account.showVideos = v)}
						/>
					{/snippet}
				</SettingRow>

				<SettingRow title="Archived photos" description="Photos hidden from the Immich timeline">
					{#snippet control()}
						<Switch
							aria-label="Archived photos"
							checked={get('showArchived')}
							onCheckedChange={(v) => (account.showArchived = v)}
						/>
					{/snippet}
				</SettingRow>
			</div>
		{/if}
	</CardBody>
</Card>
