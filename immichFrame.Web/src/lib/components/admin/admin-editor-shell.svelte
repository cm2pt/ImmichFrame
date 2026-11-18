<script lang="ts">
	import Btn from './btn.svelte';
	import { onMount, tick } from 'svelte';
	import { resolve } from '$app/paths';
	import { fly } from 'svelte/transition';
	import { cubicOut } from 'svelte/easing';
	import {
		mdiCheckCircle,
		mdiClose,
		mdiMagnify,
		mdiImageMultipleOutline,
		mdiLockOutline,
		mdiMonitorDashboard,
		mdiOpenInNew,
		mdiPlayBoxOutline,
		mdiShieldAccountOutline
	} from '@mdi/js';
	import { Icon, Text } from '@immich/ui';
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
	import SettingsSearch, { type SearchHit } from './settings-search.svelte';

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
	const pages: { id: PageId; title: string; icon: string; intro: string; tile: string }[] = [
		{
			id: 'photos',
			tile: '#0a84ff',
			title: 'Photos',
			icon: mdiImageMultipleOutline,
			intro: 'Choose which photos the frame shows.'
		},
		{
			id: 'look',
			tile: '#bf5af2',
			title: 'Slideshow & look',
			icon: mdiPlayBoxOutline,
			intro: 'How photos appear on every screen.'
		},
		{
			id: 'screens',
			tile: '#ff9f0a',
			title: 'Screens',
			icon: mdiMonitorDashboard,
			intro: 'The devices showing the frame, and how to add another.'
		},
		{
			id: 'access',
			tile: '#8e8e93',
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

	// Search: switch page, then scroll to the row and flash it, like System Settings.
	async function jumpTo(hit: SearchHit) {
		go(hit.page as PageId);
		await tick();
		window.dispatchEvent(new CustomEvent('if-reveal', { detail: hit.title }));
		await new Promise((r) => setTimeout(r, 260));
		const target =
			document.querySelector<HTMLElement>(`[data-setting="${CSS.escape(hit.title)}"]`) ??
			document.querySelector<HTMLElement>(`section[aria-label="${CSS.escape(hit.title)}"]`);
		if (!target) return;
		target.scrollIntoView({ block: 'center', behavior: 'smooth' });
		target.classList.add('if-flash');
		setTimeout(() => target.classList.remove('if-flash'), 1600);
	}

	let mobileSearch = $state(false);
	const preview = new AdminPreview();
	let liveScreens: number | null = $state(null);
	let frameVersion = $state('');
	let passwordFromEnvironment = $state(false);

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
			.then((res) => {
				if (res.status !== 200) return;
				frameVersion = res.data.version ?? '';
				passwordFromEnvironment = !!res.data.passwordFromEnvironment;
			})
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

<div class="min-h-dvh">
	<header
		class="border-sep sticky top-0 z-30 border-b bg-[#f5f5f7]/80 backdrop-blur-xl backdrop-saturate-150 dark:bg-black/70"
	>
		<div class="flex items-center justify-between gap-4 px-4 py-2 md:px-5">
			<div class="flex items-center gap-2.5">
				<img src="/logo.svg" alt="" class="h-6 w-6" />
				<span class="text-[0.9375rem] font-semibold tracking-tight">ImmichFrame</span>
			</div>
			<div class="flex items-center gap-1.5">
				{#if liveScreens !== null}
					<button
						type="button"
						class="text-label-2 hidden items-center gap-2 rounded-full px-2.5 py-1 text-[0.8125rem] hover:bg-black/5 focus-visible:outline-2 focus-visible:outline-primary sm:flex dark:hover:bg-white/10"
						onclick={() => go('screens')}
					>
						<span class="h-2 w-2 rounded-full {liveScreens ? 'bg-[#30d158]' : 'bg-[#8e8e93]'}"
						></span>
						{liveScreens
							? `${plural(liveScreens, 'screen')} showing photos`
							: 'No screen showing photos'}
					</button>
				{/if}
				<a
					href={resolve('/')}
					target="_blank"
					class="flex items-center gap-1.5 rounded-full px-3 py-1.5 text-[0.8125rem] font-medium text-primary hover:bg-primary/10 focus-visible:outline-2 focus-visible:outline-primary"
					aria-label="Open frame in a new tab"
				>
					<span class="hidden sm:inline">Open frame</span>
					<Icon icon={mdiOpenInNew} size="15" />
				</a>
				<button
					type="button"
					class="flex items-center gap-1.5 rounded-full px-3 py-1.5 text-[0.8125rem] font-medium hover:bg-black/5 focus-visible:outline-2 focus-visible:outline-primary dark:hover:bg-white/10"
					aria-label="Lock"
					onclick={onlogout}
				>
					<Icon icon={mdiLockOutline} size="15" />
					<span class="hidden sm:inline">Lock</span>
				</button>
			</div>
		</div>
		{#if mobileSearch}
			<div class="px-3 pb-2 md:hidden">
				<SettingsSearch
					autofocus
					onpick={(hit) => {
						mobileSearch = false;
						jumpTo(hit);
					}}
				/>
			</div>
		{/if}
		<nav class="flex items-center gap-1 overflow-x-auto px-3 pb-2 md:hidden" aria-label="Sections">
			{#each pages as p (p.id)}
				<a
					href="#{p.id}"
					class="shrink-0 rounded-full px-3.5 py-1.5 text-[0.875rem] font-medium focus-visible:outline-2 focus-visible:outline-primary {page ===
					p.id
						? 'bg-primary text-white dark:text-black'
						: 'text-label-2'}"
					aria-current={page === p.id ? 'page' : undefined}
					onclick={(e) => {
						e.preventDefault();
						go(p.id);
					}}
				>
					{shortTitles[p.id]}
				</a>
			{/each}
			<button
				type="button"
				class="text-label-2 ml-auto shrink-0 rounded-full p-1.5 focus-visible:outline-2 focus-visible:outline-primary"
				aria-label={mobileSearch ? 'Close search' : 'Search settings'}
				aria-expanded={mobileSearch}
				onclick={() => (mobileSearch = !mobileSearch)}
			>
				<Icon icon={mobileSearch ? mdiClose : mdiMagnify} size="20" />
			</button>
		</nav>
	</header>

	<div class="mx-auto flex max-w-[84rem]">
		<div class="hidden w-64 shrink-0 md:block">
			<aside
				class="sticky top-[49px] flex h-[calc(100dvh-49px)] flex-col justify-between px-3 py-4"
			>
				<div>
					<SettingsSearch onpick={jumpTo} />
					<nav class="mt-3 flex flex-col gap-0.5" aria-label="Sections">
						{#each pages as p (p.id)}
							{@const selected = page === p.id}
							<a
								href="#{p.id}"
								class="flex items-center gap-2.5 rounded-lg px-2 py-1.5 text-[0.9375rem] transition-colors focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-primary {selected
									? 'bg-primary font-medium text-white dark:text-black'
									: 'hover:bg-black/5 dark:hover:bg-white/10'}"
								aria-current={selected ? 'page' : undefined}
								onclick={(e) => {
									e.preventDefault();
									go(p.id);
								}}
							>
								<span
									class="flex h-[26px] w-[26px] items-center justify-center rounded-[7px] text-white shadow-[inset_0_0_0_0.5px_rgba(0,0,0,0.1)]"
									style="background:{p.tile}"
								>
									<Icon icon={p.icon} size="17" />
								</span>
								{p.title}
							</a>
						{/each}
					</nav>
				</div>
				{#if frameVersion}
					<p class="text-label-2 px-2 text-[0.75rem]">ImmichFrame {frameVersion}</p>
				{/if}
			</aside>
		</div>

		<main class="relative min-w-0 flex-1 px-4 pt-6 pb-36 md:px-8 md:pt-8">
			{#key page}
				<div in:fly={{ y: 6, duration: 220, easing: cubicOut }}>
					<h1 class="title-large">{current.title}</h1>
					<p class="text-label-2 mt-1.5 mb-8 text-[1.0625rem]">{current.intro}</p>
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
										{passwordFromEnvironment}
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
				</div>
			{/key}

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
									<Text size="small" class="text-label-2">
										{saveWarnings.length
											? saveWarnings.join(' ')
											: 'Screens update on their next reload; weather and calendars within 15 minutes.'}
									</Text>
								</div>
							{/if}
							{#if editor.dirty}
								<div class="flex gap-2">
									<Btn
										variant="ghost"
										color="secondary"
										disabled={saving}
										onclick={() => editor.discard()}
									>
										Discard
									</Btn>
									<Btn loading={saving} disabled={!canSave} onclick={onsave} title="Save (⌘S)">
										{saving ? 'Saving…' : 'Save'}
									</Btn>
								</div>
							{:else}
								<Btn
									variant="ghost"
									color="secondary"
									size="small"
									onclick={() => (justSaved = false)}
								>
									Dismiss
								</Btn>
							{/if}
						</div>
					</div>
				</div>
			{/if}
		</main>
	</div>
</div>
