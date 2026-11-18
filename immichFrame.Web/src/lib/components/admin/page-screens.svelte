<script lang="ts">
	import { onMount } from 'svelte';
	import { mdiMonitor, mdiRefresh, mdiTablet, mdiTelevision, mdiCellphone } from '@mdi/js';
	import { Button, Icon, IconButton, Input, Text } from '@immich/ui';
	import type { ScreenDto } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import AddScreen from './add-screen.svelte';
	import PageCard from './page-card.svelte';
	import { describeDevice, relativeTime } from './admin-format';

	interface Props {
		secret: string | null | undefined;
		ongotoaccess: () => void;
		onscreens?: (screens: ScreenDto[]) => void;
	}

	let { secret, ongotoaccess, onscreens }: Props = $props();

	let screens: ScreenDto[] | null = $state(null);
	let error = $state('');
	let renaming: string | null = $state(null);
	let renameValue = $state('');
	let now = $state(Date.now());

	async function load() {
		error = '';
		try {
			const res = await adminApi.getScreens();
			if (res.status !== 200) throw new Error();
			screens = res.data;
			onscreens?.(res.data);
		} catch {
			error = 'Could not load the screens.';
		}
	}

	onMount(() => {
		load();
		const timer = setInterval(() => {
			now = Date.now();
			load();
		}, 30_000);
		return () => clearInterval(timer);
	});

	async function rename(screen: ScreenDto) {
		await adminApi.renameScreen(screen.clientIdentifier!, renameValue.trim() || null);
		renaming = null;
		await load();
	}

	async function forget(screen: ScreenDto) {
		await adminApi.forgetScreen(screen.clientIdentifier!);
		await load();
	}

	const deviceIcon = (kind: string) =>
		kind === 'tv'
			? mdiTelevision
			: kind === 'tablet'
				? mdiTablet
				: kind === 'phone'
					? mdiCellphone
					: mdiMonitor;
</script>

<div class="flex flex-col gap-6">
	<PageCard
		title="Screens"
		description="Every device that has shown the frame. A screen shows up after its first visit."
	>
		{#snippet actions()}
			<IconButton
				icon={mdiRefresh}
				variant="ghost"
				color="secondary"
				aria-label="Refresh"
				onclick={load}
			/>
		{/snippet}

		{#if error}
			<Text color="danger" size="small"
				>{error} <button class="underline" onclick={load}>Retry</button></Text
			>
		{:else if screens === null}
			<Text color="muted" size="small">Loading…</Text>
		{:else if screens.length === 0}
			<Text color="muted" size="small">No screen has opened the frame yet. Add one below.</Text>
		{:else}
			<ul class="mt-2 grid gap-4 sm:grid-cols-2">
				{#each screens as screen (screen.clientIdentifier)}
					{@const device = describeDevice(screen.userAgent)}
					{@const seen = new Date(screen.lastSeenUtc!).getTime()}
					{@const live = now - seen < 3 * 60_000}
					<li class="rounded-2xl border border-light-300 p-4">
						<div class="flex items-start gap-3">
							<div class="rounded-xl bg-primary/10 p-2 text-primary">
								<Icon icon={deviceIcon(device.kind)} size="28" />
							</div>
							<div class="min-w-0 flex-1">
								{#if renaming === screen.clientIdentifier}
									<form
										class="flex gap-2"
										onsubmit={(e) => {
											e.preventDefault();
											rename(screen);
										}}
									>
										<Input bind:value={renameValue} aria-label="Screen name" />
										<Button type="submit" size="small">Save</Button>
									</form>
								{:else}
									<Text fontWeight="semi-bold" class="truncate">
										{screen.name || screen.clientIdentifier}
									</Text>
								{/if}
								<Text size="small" color="muted">{device.label}</Text>
								<span
									class="mt-2 inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium {live
										? 'bg-success/15 text-success'
										: 'bg-warning/15 text-warning'}"
								>
									<span class="h-1.5 w-1.5 rounded-full {live ? 'bg-success' : 'bg-warning'}"
									></span>
									{live ? 'Showing photos · now' : `Last seen ${relativeTime(seen, now)}`}
								</span>
							</div>
						</div>
						<div class="mt-3 flex justify-end gap-2">
							<Button size="small" variant="ghost" color="danger" onclick={() => forget(screen)}
								>Forget</Button
							>
							<Button
								size="small"
								variant="outline"
								onclick={() => {
									renaming = screen.clientIdentifier!;
									renameValue = screen.name ?? '';
								}}
							>
								Rename
							</Button>
						</div>
					</li>
				{/each}
			</ul>
		{/if}
	</PageCard>

	<AddScreen {secret} {ongotoaccess} />
</div>
