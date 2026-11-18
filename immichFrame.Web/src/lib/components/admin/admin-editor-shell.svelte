<script lang="ts">
	import { onMount } from 'svelte';
	import {
		mdiCheckCircle,
		mdiImageMultipleOutline,
		mdiLockOutline,
		mdiMonitorDashboard,
		mdiOpenInNew,
		mdiPlayBoxOutline,
		mdiShieldAccountOutline
	} from '@mdi/js';
	import { Button, Heading, Icon, Text } from '@immich/ui';
	import type { ScreenDto, ServerSettings } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import { invalidFormats } from './format-presets';
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
	const shortTitles: Record<PageId, string> = {
		photos: 'Photos',
		look: 'Look',
		screens: 'Screens',
		access: 'Access'
	};
	const pages: { id: PageId; title: string; icon: string; intro: string }[] = [
		{
			id: 'photos',
			title: 'Photos',
			icon: mdiImageMultipleOutline,
			intro: 'Choose which photos the frame shows.'
		},
		{
			id: 'look',
			title: 'Slideshow & look',
			icon: mdiPlayBoxOutline,
			intro: 'How photos appear on every screen.'
		},
		{
			id: 'screens',
			title: 'Screens',
			icon: mdiMonitorDashboard,
			intro: 'The devices showing the frame, and how to add another.'
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
	let liveScreens: number | null = $state(null);
	let frameVersion = $state('');

	function countLive(screens: ScreenDto[]) {
		liveScreens = screens.filter(
			(s) => Date.now() - new Date(s.lastSeenUtc!).getTime() < 3 * 60_000
		).length;
	}

	let canSave = $derived(editor.dirty && !saving && invalidFormats.size === 0);

	onMount(() => {
		const onHash = () => (page = readHash());
		const onBeforeUnload = (e: BeforeUnloadEvent) => {
			if (editor.dirty) e.preventDefault();
		};
		// ⌘S / Ctrl+S saves, as in any editor.
		const onKey = (e: KeyboardEvent) => {
			if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 's') {
				e.preventDefault();
				if (canSave) onsave();
			}
		};
		window.addEventListener('hashchange', onHash);
		window.addEventListener('beforeunload', onBeforeUnload);
		window.addEventListener('keydown', onKey);
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
			window.removeEventListener('keydown', onKey);
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

	// The bar stays a moment after saving to confirm it (longer when there are warnings).
	let justSaved = $state(false);
	$effect(() => {
		if (!savedAt) return;
		justSaved = true;
		const timer = setTimeout(() => (justSaved = false), saveWarnings.length ? 12_000 : 2_500);
		return () => clearTimeout(timer);
	});

	let barVisible = $derived(editor.dirty || justSaved || !!saveError);
	const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`;
</script>

<div class="min-h-dvh bg-light">
	<header class="sticky top-0 z-30 border-b border-light-200 bg-light/85 backdrop-blur-md">
		<div class="flex items-center justify-between gap-4 px-4 py-2.5 md:px-6">
			<div class="flex items-center gap-2.5">
				<img src="/logo.svg" alt="" class="h-7 w-7" />
				<span class="text-[17px] font-semibold tracking-tight">ImmichFrame</span>
				<span class="hidden text-sm text-dark/60 sm:inline">Admin</span>
			</div>
			<div class="flex items-center gap-2">
				{#if liveScreens !== null}
					<button
						type="button"
						class="hidden items-center gap-2 rounded-full px-2 py-1 text-sm text-dark/70 hover:text-dark focus-visible:outline-2 focus-visible:outline-primary sm:flex"
						onclick={() => go('screens')}
					>
						<span class="h-2 w-2 rounded-full {liveScreens ? 'bg-success' : 'bg-light-400'}"></span>
						{liveScreens
							? `${plural(liveScreens, 'screen')} showing photos`
							: 'No screen showing photos'}
					</button>
				{/if}
				<Button
					size="small"
					variant="outline"
					color="secondary"
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
				<a
					href="#{p.id}"
					class="shrink-0 rounded-full px-3 py-1.5 text-sm font-medium focus-visible:outline-2 focus-visible:outline-primary {page ===
					p.id
						? 'bg-primary/10 text-primary'
						: 'text-dark/70'}"
					aria-current={page === p.id ? 'page' : undefined}
					onclick={(e) => {
						e.preventDefault();
						go(p.id);
					}}
				>
					{shortTitles[p.id]}
				</a>
			{/each}
		</nav>
	</header>

	<div class="mx-auto flex max-w-[90rem]">
		<div class="hidden w-60 shrink-0 border-r border-light-200 md:block">
			<aside class="sticky top-[53px] flex h-[calc(100dvh-53px)] flex-col justify-between p-3">
				<nav class="flex flex-col gap-0.5" aria-label="Sections">
					{#each pages as p (p.id)}
						<a
							href="#{p.id}"
							class="flex items-center gap-3 rounded-full px-4 py-2.5 font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary {page ===
							p.id
								? 'bg-primary/10 text-primary'
								: 'text-dark/80 hover:bg-subtle/60'}"
							aria-current={page === p.id ? 'page' : undefined}
							onclick={(e) => {
								e.preventDefault();
								go(p.id);
							}}
						>
							<Icon icon={p.icon} size="20" />
							{p.title}
						</a>
					{/each}
				</nav>
				{#if frameVersion}
					<Text size="small" class="px-4 pb-2 text-dark/60">ImmichFrame {frameVersion}</Text>
				{/if}
			</aside>
		</div>

		<main class="relative min-w-0 flex-1 px-4 pt-7 pb-36 md:px-8">
			<Heading tag="h1" size="large">{current.title}</Heading>
			<Text class="mt-2 mb-7 text-dark/70">{current.intro}</Text>

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
					<!-- On narrow screens the Look page shows the preview first, so changes are in view. -->
					<aside
						class="flex flex-col gap-4 xl:sticky xl:top-20 xl:w-96 {page === 'look'
							? 'order-first xl:order-none'
							: ''}"
						aria-label="Preview"
					>
						<LivePreview general={editor.settings.General ?? {}} {preview} />
						<div class={page === 'look' ? 'hidden xl:block' : ''}><UpNext {preview} /></div>
					</aside>
				{:else if page === 'access'}
					<aside class="flex flex-col gap-4 xl:sticky xl:top-20 xl:w-96">
						<RecentActivity
							refreshKey={savedAt}
							dirty={editor.dirty}
							accounts={editor.settings.Accounts ?? []}
							onrestored={onreload}
						/>
						<SettingsBackup saved={savedSettings} onrestore={restoreFromFile} />
					</aside>
				{/if}
			</div>

			<!-- The save bar floats over the content column, not the sidebar. -->
			{#if barVisible}
				<div class="pointer-events-none fixed inset-x-0 bottom-0 z-40">
					<div class="mx-auto max-w-[90rem] p-4 md:pl-64">
						<div
							class="pointer-events-auto mx-auto flex max-w-2xl flex-wrap items-center justify-between gap-3 rounded-2xl border border-light-300 bg-light-50/95 px-5 py-3 shadow-xl backdrop-blur dark:bg-light-100/95"
							role="region"
							aria-label="Unsaved changes"
							aria-live="polite"
						>
							{#if saveError}
								<Text
									fontWeight="medium"
									class="text-danger-700 dark:text-danger-400 min-w-0 flex-1">{saveError}</Text
								>
							{:else if editor.dirty}
								<Text fontWeight="medium" class="min-w-0 flex-1">
									{invalidFormats.size > 0
										? 'Fix the invalid date or clock pattern to save'
										: plural(Math.max(editor.changeCount, 1), 'unsaved change')}
								</Text>
							{:else}
								<div class="flex min-w-0 flex-1 flex-col">
									<span
										class="flex items-center gap-2 font-medium {saveWarnings.length
											? 'text-dark'
											: 'text-dark'}"
									>
										<Icon
											icon={mdiCheckCircle}
											size="20"
											class={saveWarnings.length ? 'text-warning' : 'text-success'}
										/>
										{saveWarnings.length ? 'Saved, with a warning' : 'Saved'}
									</span>
									<Text size="small" class="text-dark/70">
										{saveWarnings.length
											? saveWarnings.join(' ')
											: 'Screens update on their next reload; weather and calendars within 15 minutes.'}
									</Text>
								</div>
							{/if}
							{#if editor.dirty}
								<div class="flex gap-2">
									<Button
										variant="ghost"
										color="secondary"
										disabled={saving}
										onclick={() => editor.discard()}
									>
										Discard
									</Button>
									<Button loading={saving} disabled={!canSave} onclick={onsave} title="Save (⌘S)">
										{saving ? 'Saving…' : 'Save'}
									</Button>
								</div>
							{:else}
								<Button
									variant="ghost"
									color="secondary"
									size="small"
									onclick={() => (justSaved = false)}
								>
									Dismiss
								</Button>
							{/if}
						</div>
					</div>
				</div>
			{/if}
		</main>
	</div>
</div>
