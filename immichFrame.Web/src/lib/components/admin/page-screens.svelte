<script lang="ts">
	import { onMount } from 'svelte';
	import { mdiCellphone, mdiMonitor, mdiRefresh, mdiTablet, mdiTelevision } from '@mdi/js';
	import { Button, Card, CardBody, Icon, IconButton, Input, Text } from '@immich/ui';
	import type { ScreenDto } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import AddScreen from './add-screen.svelte';
	import { describeDevice, relativeTime } from './admin-format';

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

	const deviceIcon = (kind: string) =>
		kind === 'tv'
			? mdiTelevision
			: kind === 'tablet'
				? mdiTablet
				: kind === 'phone'
					? mdiCellphone
					: mdiMonitor;
	const screenName = (screen: ScreenDto) => screen.name || screen.clientIdentifier || 'Screen';
</script>

<div class="flex flex-col gap-6">
	<Card>
		<CardBody>
			<div class="mb-3 flex items-center justify-between gap-3">
				<Text class="text-dark/70" size="small">
					{screens?.length
						? `${screens.length} ${screens.length === 1 ? 'screen' : 'screens'} · updated ${relativeTime(now)}`
						: 'Screens appear here after they first open the frame.'}
				</Text>
				<IconButton
					icon={mdiRefresh}
					variant="ghost"
					color="secondary"
					size="small"
					aria-label="Refresh"
					title="Refresh"
					disabled={loading}
					onclick={load}
				/>
			</div>

			{#if error}
				<div class="flex items-center gap-3 rounded-xl bg-danger/10 p-4">
					<Text size="small" color="danger" class="flex-1">{error}</Text>
					<Button size="small" variant="outline" onclick={load}>Retry</Button>
				</div>
			{:else if screens === null}
				<ul class="grid gap-4 sm:grid-cols-2" aria-busy="true">
					{#each [0, 1] as i (i)}
						<li class="h-36 animate-pulse rounded-2xl bg-subtle/50 dark:bg-light-100"></li>
					{/each}
				</ul>
			{:else if screens.length === 0}
				<div class="rounded-2xl border border-dashed border-light-300 p-8 text-center">
					<Text fontWeight="semi-bold">No screens yet</Text>
					<Text size="small" class="text-dark/70 mt-1"
						>Add one below. It shows up here once it opens the frame.</Text
					>
				</div>
			{:else}
				<ul class="grid gap-4 sm:grid-cols-2">
					{#each screens as screen (screen.clientIdentifier)}
						{@const device = describeDevice(screen.userAgent)}
						{@const seen = new Date(screen.lastSeenUtc!).getTime()}
						{@const live = now - seen < 3 * 60_000}
						<li class="flex flex-col rounded-2xl border border-light-300 p-4">
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
											<Button
												size="small"
												variant="ghost"
												color="secondary"
												onclick={() => (renaming = null)}>Cancel</Button
											>
										</form>
									{:else}
										<Text fontWeight="semi-bold" class="truncate">{screenName(screen)}</Text>
									{/if}
									<Text class="text-dark/70" size="small">{device.label}</Text>
									<span
										class="mt-2 inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium {live
											? 'bg-success/15 text-success'
											: 'bg-warning/15 text-warning'}"
									>
										<span class="h-1.5 w-1.5 rounded-full {live ? 'bg-success' : 'bg-warning'}"
										></span>
										{live ? 'Showing now' : `Last seen ${relativeTime(seen, now)}`}
									</span>
								</div>
							</div>

							{#if confirmForget === screen.clientIdentifier}
								<div
									class="mt-4 rounded-xl bg-subtle/50 p-3 dark:bg-light-100"
									role="alertdialog"
									aria-label="Forget screen"
								>
									<Text size="small">
										Forget “{screenName(screen)}”? It comes back with no name the next time it opens
										the frame.
									</Text>
									<div class="mt-3 flex justify-end gap-2">
										<Button
											size="small"
											variant="ghost"
											color="secondary"
											onclick={() => (confirmForget = null)}>Cancel</Button
										>
										<Button size="small" color="danger" onclick={() => forget(screen)}
											>Forget</Button
										>
									</div>
								</div>
							{:else if renaming !== screen.clientIdentifier}
								<div class="mt-auto flex justify-end gap-2 pt-4">
									<Button
										size="small"
										variant="ghost"
										color="secondary"
										onclick={() => (confirmForget = screen.clientIdentifier!)}
									>
										Forget…
									</Button>
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
							{/if}
						</li>
					{/each}
				</ul>
			{/if}
		</CardBody>
	</Card>

	<AddScreen {secret} {ongotoaccess} />
</div>
