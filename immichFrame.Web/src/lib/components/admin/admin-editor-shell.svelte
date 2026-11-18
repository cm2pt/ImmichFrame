<script lang="ts">
	import { onMount } from 'svelte';
	import {
		mdiImageMultipleOutline,
		mdiLockOutline,
		mdiMonitorDashboard,
		mdiOpenInNew,
		mdiPlayBoxOutline,
		mdiShieldAccountOutline
	} from '@mdi/js';
	import { Alert, Button, Heading, Icon, Text } from '@immich/ui';
	import type { ScreenDto, ServerSettings } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import AdminBrand from './admin-brand.svelte';
	import type { AdminEditor } from './admin-editor.svelte';
	import { AdminPreview } from './admin-preview.svelte';
	import LivePreview from './live-preview.svelte';
	import PageAccess from './page-access.svelte';
	import PageLook from './page-look.svelte';
	import PagePhotos from './page-photos.svelte';
	import PageScreens from './page-screens.svelte';
	import RecentActivity from './recent-activity.svelte';
	import SettingsBackup from './settings-backup.svelte';
	import UpNext from './up-next.svelte';

	interface Props {
		editor: AdminEditor;
		saving: boolean;
		saveError: string;
		saveWarnings: string[];
		savedAt: number;
		onsave: () => void;
		onreload: () => void;
		onlogout: () => void;
	}

	let { editor, saving, saveError, saveWarnings, savedAt, onsave, onreload, onlogout }: Props =
		$props();

	type PageId = 'photos' | 'look' | 'screens' | 'access';
	const pages: { id: PageId; title: string; icon: string; intro: string }[] = [
		{
			id: 'photos',
			title: 'Photos',
			icon: mdiImageMultipleOutline,
			intro: 'Choose what the frame shows. Screens only change when you save.'
		},
		{
			id: 'look',
			title: 'Slideshow & look',
			icon: mdiPlayBoxOutline,
			intro: 'How photos appear on every screen. The preview follows your changes.'
		},
		{
			id: 'screens',
			title: 'Screens',
			icon: mdiMonitorDashboard,
			intro: 'Every device showing the frame, and how to add another.'
		},
		{
			id: 'access',
			title: 'Access & accounts',
			icon: mdiShieldAccountOutline,
			intro: 'The Immich accounts the frame reads from, and who can see and change it.'
		}
	];

	const readHash = (): PageId => {
		const hash = typeof window === 'undefined' ? '' : window.location.hash.slice(1);
		return (pages.find((p) => p.id === hash)?.id ?? 'photos') as PageId;
	};
	let page: PageId = $state(readHash());
	let current = $derived(pages.find((p) => p.id === page)!);

	function go(id: PageId) {
		page = id;
		history.replaceState(null, '', `#${id}`);
		window.scrollTo({ top: 0 });
	}

	const preview = new AdminPreview();
	let liveScreens = $state(0);
	let frameVersion = $state('');

	function countLive(screens: ScreenDto[]) {
		liveScreens = screens.filter(
			(s) => Date.now() - new Date(s.lastSeenUtc!).getTime() < 3 * 60_000
		).length;
	}

	onMount(() => {
		const onHash = () => (page = readHash());
		const onBeforeUnload = (e: BeforeUnloadEvent) => {
			if (editor.dirty) e.preventDefault();
		};
		window.addEventListener('hashchange', onHash);
		window.addEventListener('beforeunload', onBeforeUnload);
		adminApi
			.getAdminInfo()
			.then((res) => res.status === 200 && (frameVersion = res.data.version ?? ''))
			.catch(() => {});
		adminApi
			.getScreens()
			.then((res) => res.status === 200 && countLive(res.data))
			.catch(() => {});
		return () => {
			window.removeEventListener('hashchange', onHash);
			window.removeEventListener('beforeunload', onBeforeUnload);
		};
	});

	// After a save, the sample reflects the new choice.
	$effect(() => {
		if (savedAt) preview.load(4);
	});

	let savedSettings: ServerSettings = $derived(JSON.parse(editor.saved));

	function addAccount() {
		editor.settings.Accounts = [...(editor.settings.Accounts ?? []), {}];
		go('access');
	}

	function removeAccount(index: number) {
		editor.settings.Accounts = (editor.settings.Accounts ?? []).filter((_, i) => i !== index);
		editor.version++;
	}

	function restoreFromFile(settings: ServerSettings) {
		const saved = editor.saved;
		editor.load(settings);
		editor.saved = saved; // keep it unsaved so the bar offers Save / Discard
	}

	let showSaved = $state(false);
	$effect(() => {
		if (!savedAt) return;
		showSaved = true;
		const timer = setTimeout(() => (showSaved = false), saveWarnings.length ? 15_000 : 4_000);
		return () => clearTimeout(timer);
	});
</script>

<div class="min-h-dvh bg-light">
	<header class="sticky top-0 z-30 border-b border-light-200 bg-light/90 backdrop-blur">
		<div class="flex items-center justify-between gap-4 px-4 py-3 md:px-6">
			<AdminBrand subtitle="Admin" />
			<div class="flex items-center gap-2">
				<span class="hidden items-center gap-2 text-sm text-muted sm:flex">
					<span class="h-2 w-2 rounded-full {liveScreens ? 'bg-success' : 'bg-light-400'}"></span>
					{liveScreens === 1 ? '1 screen' : `${liveScreens} screens`} showing photos
				</span>
				<Button
					size="small"
					variant="outline"
					trailingIcon={mdiOpenInNew}
					href="/"
					target="_blank"
					aria-label="Open frame"><span class="hidden sm:inline">Open frame</span></Button
				>
				<Button
					size="small"
					variant="outline"
					color="secondary"
					leadingIcon={mdiLockOutline}
					aria-label="Lock"
					onclick={onlogout}><span class="hidden sm:inline">Lock</span></Button
				>
			</div>
		</div>
		<nav class="flex gap-1 overflow-x-auto px-3 pb-2 md:hidden" aria-label="Sections">
			{#each pages as p (p.id)}
				<button
					type="button"
					class="shrink-0 rounded-full px-3 py-1.5 text-sm font-medium {page === p.id
						? 'bg-primary/10 text-primary'
						: 'text-muted'}"
					aria-current={page === p.id ? 'page' : undefined}
					onclick={() => go(p.id)}
				>
					{p.title}
				</button>
			{/each}
		</nav>
	</header>

	<div class="mx-auto flex max-w-[90rem]">
		<div class="hidden w-60 shrink-0 border-r border-light-200 md:block">
			<aside class="sticky top-[65px] flex h-[calc(100dvh-65px)] flex-col justify-between p-4">
				<nav class="flex flex-col gap-1" aria-label="Sections">
					{#each pages as p (p.id)}
						<button
							type="button"
							class="flex items-center gap-3 rounded-full px-4 py-2.5 text-left font-medium transition-colors {page ===
							p.id
								? 'bg-primary/10 text-primary'
								: 'text-dark/80 hover:bg-subtle/60'}"
							aria-current={page === p.id ? 'page' : undefined}
							onclick={() => go(p.id)}
						>
							<Icon icon={p.icon} size="20" />
							{p.title}
						</button>
					{/each}
				</nav>
				{#if frameVersion}
					<div class="rounded-2xl bg-subtle/50 p-3 dark:bg-light-100">
						<Text size="small" fontWeight="semi-bold">ImmichFrame v{frameVersion}</Text>
					</div>
				{/if}
			</aside>
		</div>

		<main class="min-w-0 flex-1 px-4 pt-6 pb-32 md:px-8">
			<Heading tag="h1" size="large">{current.title}</Heading>
			<Text color="muted" class="mt-2 mb-6">{current.intro}</Text>

			{#if showSaved}
				<Alert color={saveWarnings.length ? 'warning' : 'success'} title="Saved" class="mb-4">
					Screens pick up display changes on their next reload; weather and calendars within ~15
					minutes.
					{#if saveWarnings.length}
						<ul class="mt-2 list-disc pl-5">
							{#each saveWarnings as warning (warning)}<li>{warning}</li>{/each}
						</ul>
					{/if}
				</Alert>
			{/if}
			{#if saveError}
				<Alert color="danger" class="mb-4">{saveError}</Alert>
			{/if}

			<div class="flex flex-col gap-6 xl:flex-row xl:items-start">
				<div class="min-w-0 flex-1">
					{#if page === 'photos'}
						<PagePhotos
							settings={editor.settings}
							version={editor.version}
							onaddaccount={addAccount}
						/>
					{:else if page === 'look'}
						{#key editor.version}
							<PageLook general={editor.settings.General ?? (editor.settings.General = {})} />
						{/key}
					{:else if page === 'screens'}
						<PageScreens
							secret={savedSettings.General?.authenticationSecret}
							ongotoaccess={() => go('access')}
							onscreens={countLive}
						/>
					{:else}
						{#key editor.version}
							<PageAccess
								settings={editor.settings}
								version={editor.version}
								onaddaccount={() =>
									(editor.settings.Accounts = [...(editor.settings.Accounts ?? []), {}])}
								onremoveaccount={removeAccount}
							/>
						{/key}
					{/if}
				</div>

				{#if page === 'photos' || page === 'look'}
					<aside class="flex flex-col gap-4 xl:sticky xl:top-24 xl:w-96">
						<LivePreview general={editor.settings.General ?? {}} {preview} />
						<UpNext {preview} />
					</aside>
				{:else if page === 'access'}
					<aside class="flex flex-col gap-4 xl:sticky xl:top-24 xl:w-96">
						<RecentActivity refreshKey={savedAt} dirty={editor.dirty} onrestored={onreload} />
						<SettingsBackup saved={savedSettings} onrestore={restoreFromFile} />
					</aside>
				{/if}
			</div>
		</main>
	</div>

	{#if editor.dirty}
		<div class="fixed inset-x-0 bottom-0 z-40 p-4" role="region" aria-label="Unsaved changes">
			<div
				class="mx-auto flex max-w-2xl items-center justify-between gap-4 rounded-2xl border border-light-300 bg-light-50 px-5 py-3 shadow-xl dark:bg-light-100"
			>
				<Text fontWeight="medium">You have unsaved changes</Text>
				<div class="flex gap-2">
					<Button
						variant="ghost"
						color="secondary"
						disabled={saving}
						onclick={() => editor.discard()}>Discard</Button
					>
					<Button loading={saving} onclick={onsave}>{saving ? 'Saving…' : 'Save'}</Button>
				</div>
			</div>
		</div>
	{/if}
</div>
