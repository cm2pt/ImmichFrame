<script lang="ts">
	import { onMount } from 'svelte';
	import { Input } from '@immich/ui';
	import type { ScreenDto } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import AddScreen from './add-screen.svelte';
	import { describeDevice, relativeTime } from './admin-format';
	import Btn from './btn.svelte';
	import Glyph from './glyph.svelte';
	import type { GlyphName } from './glyphs';
	import InlineConfirm from './inline-confirm.svelte';
	import PageCard from './page-card.svelte';
	import RowMenu from './row-menu.svelte';

	interface Props {
		secret: string | null | undefined;
		ongotoaccess: () => void;
		onscreens?: (screens: ScreenDto[]) => void;
	}

	let { secret, ongotoaccess, onscreens }: Props = $props();

	let screens: ScreenDto[] | null = $state(null);
	let error = $state('');
	let loading = $state(false);
	let renaming: string | null = $state(null);
	let renameValue = $state('');
	let confirmForget: string | null = $state(null);
	let now = $state(Date.now());

	async function load() {
		error = '';
		loading = true;
		try {
			const res = await adminApi.getScreens();
			if (res.status !== 200) throw new Error();
			screens = res.data;
			onscreens?.(res.data);
		} catch {
			error = 'Couldn’t load the screens.';
		} finally {
			loading = false;
			now = Date.now();
		}
	}

	onMount(() => {
		load();
		const timer = setInterval(load, 30_000);
		return () => clearInterval(timer);
	});

	async function rename(screen: ScreenDto) {
		await adminApi.renameScreen(screen.clientIdentifier!, renameValue.trim() || null);
		renaming = null;
		await load();
	}

	async function forget(screen: ScreenDto) {
		confirmForget = null;
		await adminApi.forgetScreen(screen.clientIdentifier!);
		await load();
	}

	const tiles: Record<string, { glyph: GlyphName; color: string }> = {
		tv: { glyph: 'tv', color: '#8e8e93' },
		tablet: { glyph: 'tablet', color: '#8e8e93' },
		phone: { glyph: 'smartphone', color: '#8e8e93' },
		desktop: { glyph: 'monitor', color: '#8e8e93' }
	};
	const screenName = (screen: ScreenDto) => screen.name || screen.clientIdentifier || 'Screen';
</script>

<PageCard
	title="Screens"
	footer={screens?.length
		? `Updated ${relativeTime(now)}. A screen shows up after it first opens the frame.`
		: 'A screen shows up here after it first opens the frame.'}
>
	{#snippet actions()}
		<button type="button" class="text-primary disabled:opacity-40" disabled={loading} onclick={load}
			>Refresh</button
		>
		>
	{/snippet}

	{#if error}
		<div class="flex min-h-11 items-center justify-between gap-3 py-2">
			<p class="text-[0.875rem]">{error}</p>
			<Btn variant="ghost" color="primary" size="small" onclick={load}>Try Again</Btn>
		</div>
	{:else if screens === null}
		{#each [0, 1] as i (i)}
			<div
				class="border-sep flex min-h-14 items-center gap-3 border-t py-2 first:border-t-0"
				aria-busy="true"
			>
				<span class="bg-fill h-[29px] w-[29px] animate-pulse rounded-[7px]"></span>
				<span class="bg-fill h-3 w-40 animate-pulse rounded"></span>
			</div>
		{/each}
	{:else if screens.length === 0}
		<p class="text-label-2 py-3 text-[0.875rem]">No screens yet.</p>
	{:else}
		{#each screens as screen (screen.clientIdentifier)}
			{@const device = describeDevice(screen.userAgent)}
			{@const seen = new Date(screen.lastSeenUtc!).getTime()}
			{@const live = now - seen < 3 * 60_000}
			{@const look = tiles[device.kind] ?? tiles.desktop}
			<div class="border-sep border-t py-2 first:border-t-0">
				<div class="flex min-h-11 items-center gap-3">
					<span
						class="flex h-[29px] w-[29px] shrink-0 items-center justify-center rounded-[7px] text-white"
						style="background:{look.color}"
					>
						<Glyph name={look.glyph} size={17} stroke={2} />
					</span>
					<div class="min-w-0 flex-1">
						{#if renaming === screen.clientIdentifier}
							<form
								class="flex items-center gap-2"
								onsubmit={(e) => {
									e.preventDefault();
									rename(screen);
								}}
							>
								<Input bind:value={renameValue} aria-label="Screen name" />
								<Btn type="submit" size="small">Save</Btn>
								<Btn variant="ghost" color="primary" size="small" onclick={() => (renaming = null)}
									>Cancel</Btn
								>
							</form>
						{:else}
							<p class="truncate text-[0.9375rem] md:text-[0.875rem]">{screenName(screen)}</p>
							<p class="text-label-2 flex items-center gap-1.5 text-[0.8125rem] md:text-[0.75rem]">
								<span class="h-1.5 w-1.5 rounded-full {live ? 'bg-[#30d158]' : 'bg-[#ff9f0a]'}"
								></span>
								{device.label}&nbsp;·&ensp;{live
									? 'Showing now'
									: `Last seen ${relativeTime(seen, now)}`}
							</p>
						{/if}
					</div>
					{#if renaming !== screen.clientIdentifier}
						<RowMenu
							id={`screen-menu-${screen.clientIdentifier}`}
							label="Actions for {screenName(screen)}"
							items={[
								{
									label: 'Rename…',
									onselect: () => {
										renaming = screen.clientIdentifier!;
										renameValue = screen.name ?? '';
									}
								},
								{
									label: 'Forget…',
									destructive: true,
									onselect: () => (confirmForget = screen.clientIdentifier!)
								}
							]}
						/>
					{/if}
				</div>
				{#if confirmForget === screen.clientIdentifier}
					<InlineConfirm
						message={`Forget “${screenName(screen)}”? It comes back with no name the next time it opens the frame.`}
						confirmLabel="Forget"
						danger
						returnFocusTo={`screen-menu-${screen.clientIdentifier}`}
						onconfirm={() => forget(screen)}
						oncancel={() => (confirmForget = null)}
					/>
				{/if}
			</div>
		{/each}
	{/if}
</PageCard>

<AddScreen {secret} {ongotoaccess} />
