<script lang="ts">
	import { onMount, tick } from 'svelte';
	import { resolve } from '$app/paths';
	import { fade, fly } from 'svelte/transition';
	import { cubicOut } from 'svelte/easing';
	import type { ScreenDto, ServerSettings } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import Btn from './btn.svelte';
	import Glyph from './glyph.svelte';
	import type { GlyphName } from './glyphs';
	import { invalidFormats } from './format-presets';
	import type { AdminEditor } from './admin-editor.svelte';
	import { accountLabel, libraryFor, peekLibrary } from './admin-library.svelte';
	import { describeUnsavedChange } from './admin-history';
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
	const pages: { id: PageId; title: string; glyph: GlyphName; intro: string; tile: string }[] = [
		{
			id: 'photos',
			title: 'Photos',
			glyph: 'images',
			tile: '#0a84ff',
			intro: 'Choose which photos the frame shows.'
		},
		{
			id: 'look',
			title: 'Slideshow & Look',
			glyph: 'play',
			tile: '#bf5af2',
			intro: 'How photos appear on every screen.'
		},
		{
			id: 'screens',
			title: 'Screens',
			glyph: 'tv',
			tile: '#ff9f0a',
			intro: 'The devices showing the frame, and how to add another.'
		},
		{
			id: 'access',
			title: 'Access & Accounts',
			glyph: 'shieldCheck',
			tile: '#8e8e93',
			intro: 'The Immich accounts the frame reads from, and who can see and change it.'
		}
	];

	// Where you are lives in the URL hash, one level per segment: "#look/captions",
	// "#photos/account-2". On phones an empty hash is the root list, like iOS Settings. Every
	// move is a history entry, so the browser's Back and the iPhone's swipe go up one level.
	interface Place {
		page: PageId | null;
		account: number | null;
		captions: boolean;
	}
	const readPlace = (): Place => {
		const [id, sub = ''] =
			typeof window === 'undefined' ? [''] : window.location.hash.slice(1).split('/');
		const page = (pages.find((p) => p.id === id)?.id ?? null) as PageId | null;
		const account =
			page === 'photos' && /^account-\d+$/.test(sub) ? Number(sub.slice(8)) - 1 : null;
		return { page, account, captions: page === 'look' && sub === 'captions' };
	};
	const hashOf = (p: Place) =>
		p.page === null
			? ''
			: `#${p.page}${p.page === 'photos' && p.account !== null ? `/account-${p.account + 1}` : ''}${
					p.page === 'look' && p.captions ? '/captions' : ''
				}`;
	const start = readPlace();
	let hashPage: PageId | null = $state(start.page);
	let page: PageId = $derived(hashPage ?? 'photos');
	let current = $derived(pages.find((p) => p.id === page)!);

	// Photos drills into one account (when there are several): its name becomes the page's
	// title. Slideshow & Look drills into Clock & Captions the same way.
	let photosAccount: number | null = $state(start.account);
	let lookCaptions = $state(start.captions);
	// While search has a query, its results replace the sections list.
	let phoneSearching = $state(false);
	let sidebarSearching = $state(false);

	// Any change of place becomes a history entry (marked, so Back knows it can go back).
	$effect(() => {
		const want = hashOf({ page: hashPage, account: photosAccount, captions: lookCaptions });
		if (want === window.location.hash) return;
		history.pushState({ immichFrameAdmin: true }, '', want || window.location.pathname);
	});
	function applyPlace() {
		const p = readPlace();
		hashPage = p.page;
		photosAccount = p.account;
		lookCaptions = p.captions;
	}

	function go(id: PageId | null) {
		if (id !== hashPage) {
			lookCaptions = false;
			photosAccount = null;
		}
		hashPage = id;
		window.scrollTo({ top: 0 });
	}
	let drill: { title: string; back: string } | null = $derived.by(() => {
		if (page === 'photos' && photosAccount !== null) {
			const account = editor.settings.Accounts?.[photosAccount];
			if (account)
				return { title: accountLabel(account, photosAccount, libraryFor(account)), back: 'Photos' };
		}
		if (page === 'look' && lookCaptions)
			return { title: 'Clock & Captions', back: 'Slideshow & Look' };
		return null;
	});
	let title = $derived(drill?.title ?? current.title);
	let backLabel = $derived(drill?.back ?? 'ImmichFrame');
	function back() {
		// Opened straight at a deep link there's nothing of ours to go back to: go up instead.
		if (history.state?.immichFrameAdmin) history.back();
		else if (drill) {
			if (page === 'photos') photosAccount = null;
			else lookCaptions = false;
		} else go(null);
		window.scrollTo({ top: 0 });
	}

	// Search: switch page, then scroll to the row and highlight it, like System Settings.
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
		// Keyboard and VoiceOver users land on the setting too: its first control, or the row.
		const control = target.querySelector<HTMLElement>(
			'button:not([disabled]), input:not([disabled]), select:not([disabled]), [role=switch], [role=radio][tabindex="0"], a[href]'
		);
		if (control) control.focus({ preventScroll: true });
		else {
			target.tabIndex = -1;
			target.focus({ preventScroll: true });
		}
		target.classList.add('if-flash');
		setTimeout(() => target.classList.remove('if-flash'), 1100);
	}

	// Phone nav bar: becomes solid once the page's large title has scrolled under it.
	let scrolled = $state(false);
	$effect(() => {
		const onScroll = () => (scrolled = window.scrollY > 44);
		onScroll();
		window.addEventListener('scroll', onScroll, { passive: true });
		return () => window.removeEventListener('scroll', onScroll);
	});

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
		const onHash = () => applyPlace();
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
		window.addEventListener('popstate', onHash);
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
			window.removeEventListener('popstate', onHash);
			window.removeEventListener('beforeunload', onBeforeUnload);
			window.removeEventListener('keydown', onKey);
		};
	});

	// After a save, the sample reflects the new choice.
	$effect(() => {
		if (savedAt) preview.load(4);
	});

	let savedSettings: ServerSettings = $derived(JSON.parse(editor.saved));

	// Photos' "Add Account" opens the Add sheet on Access & Accounts; nothing is added until
	// that sheet's Add.
	let startNewAccount = $state(false);
	function addAccount() {
		startNewAccount = true;
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
	// While the bar floats over the bottom of the page, keyboard focus must not land under it
	// (WCAG 2.4.11): scrolling keeps a bar's height clear, and a control tabbed to behind it is
	// scrolled up into view.
	$effect(() => {
		if (!barVisible) return;
		const root = document.documentElement;
		root.style.scrollPaddingBottom = '7rem';
		const onFocus = (e: FocusEvent) => {
			const target = e.target as HTMLElement | null;
			const bar = document.querySelector('.if-savebar [class*="rounded-2xl"]');
			if (!target?.getBoundingClientRect || !bar || bar.contains(target)) return;
			const overlap =
				target.getBoundingClientRect().bottom - (bar.getBoundingClientRect().top - 12);
			if (overlap > 0) window.scrollBy({ top: overlap });
		};
		document.addEventListener('focusin', onFocus);
		return () => {
			root.style.scrollPaddingBottom = '';
			document.removeEventListener('focusin', onFocus);
		};
	});
	const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`;
	let statusText = $derived(
		liveScreens === null
			? ''
			: liveScreens
				? `${plural(liveScreens, 'screen')} showing photos`
				: 'No screens connected'
	);
</script>

{#snippet tile(p: (typeof pages)[number], size = 26)}
	<span
		class="flex shrink-0 items-center justify-center rounded-[7px] text-white shadow-[inset_0_0_0_0.5px_rgba(0,0,0,0.12)]"
		style="background:{p.tile};width:{size}px;height:{size}px"
	>
		<Glyph name={p.glyph} size={Math.round(size * 0.62)} stroke={2} />
	</span>
{/snippet}

<div class="if-shell min-h-dvh">
	<!-- Phone: the root list, then each page pushed with a back button. On a page this wrapper
	     holds only the nav bar, so it is what sticks (a sticky child of a box its own height
	     scrolled away with it). -->
	<div class="md:hidden {hashPage === null ? '' : 'sticky top-0 z-30'}">
		{#if hashPage === null}
			<div class="px-4 pt-10 pb-28" in:fade={{ duration: 150 }}>
				<h1 class="mb-4 text-[2.125rem] leading-tight font-bold tracking-[-0.02em]">ImmichFrame</h1>
				<SettingsSearch onpick={jumpTo} variant="list" bind:searching={phoneSearching} />
				{#if !phoneSearching}
					<nav class="bg-group rounded-group mt-5 px-4" aria-label="Sections">
						{#each pages as p (p.id)}
							<!-- iOS insets the separator to where the text starts, not under the icon. -->
							<a
								href="#{p.id}"
								class="group flex min-h-11 items-center gap-3"
								onclick={(e) => {
									e.preventDefault();
									go(p.id);
								}}
							>
								{@render tile(p, 29)}
								<span
									class="border-sep flex min-h-11 flex-1 items-center border-t py-2 group-first:border-t-0"
								>
									<span class="flex-1 text-[1.0625rem]">{p.title}</span>
									<Glyph name="chevronRight" size={16} class="text-label-3" />
								</span>
							</a>
						{/each}
					</nav>
					<div class="bg-group rounded-group mt-6 px-4">
						<a
							href={resolve('/')}
							target="_blank"
							class="flex min-h-11 items-center justify-between py-2 text-[1.0625rem] text-primary"
						>
							Open Frame <Glyph name="externalLink" size={16} />
						</a>
						<button
							type="button"
							class="border-sep flex min-h-11 w-full items-center justify-between border-t py-2 text-left text-[1.0625rem] text-primary"
							onclick={onlogout}
						>
							Lock <Glyph name="lock" size={16} />
						</button>
					</div>
					<p class="text-label-2 mt-3 px-4 text-[0.8125rem]">
						{statusText}{statusText && frameVersion ? ' · ' : ''}{frameVersion
							? `ImmichFrame ${frameVersion}`
							: ''}
					</p>
				{/if}
			</div>
		{:else}
			<!-- Transparent at the top; the small title, material and hairline fade in once the
			     large title scrolls under the bar, as in iOS. -->
			<header
				class="sticky top-0 z-30 grid grid-cols-[1fr_minmax(0,auto)_1fr] items-center border-b px-2 py-2.5 transition-[background-color,border-color] duration-200 {scrolled
					? 'border-sep bg-[rgb(249_249_249/0.92)] backdrop-blur-[20px] backdrop-saturate-[180%] dark:bg-[rgb(22_22_22/0.92)]'
					: 'border-transparent'}"
			>
				<button
					type="button"
					class="flex min-w-0 items-center gap-0.5 justify-self-start rounded-md px-1 text-[1.0625rem] whitespace-nowrap text-primary"
					onclick={back}
				>
					<Glyph name="chevronLeft" size={22} stroke={2.2} />
					<!-- The parent's name, as iOS does; once the centred title shows and both won't fit,
					     only the chevron stays (the name is still read out). -->
					<span class={scrolled && backLabel.length + title.length > 24 ? 'sr-only' : 'truncate'}
						>{backLabel}</span
					>
				</button>
				<span
					class="min-w-0 truncate text-center text-[1.0625rem] font-semibold transition-opacity duration-200 {scrolled
						? 'opacity-100'
						: 'opacity-0'}"
					aria-hidden={!scrolled}>{title}</span
				>
				<span></span>
			</header>
		{/if}
	</div>

	<div class="flex">
		<!-- Desktop sidebar: its own surface, flush left. Brand, search, sections; status and
		     actions at the bottom. -->
		<div class="bg-sidebar border-sep hidden w-60 shrink-0 border-r backdrop-blur-xl md:block">
			<aside class="sticky top-0 flex h-dvh flex-col justify-between px-2.5 pt-5 pb-4">
				<div>
					<div class="mb-4 flex items-center gap-2.5 px-2">
						<img src="/logo.svg" alt="" class="h-7 w-7" />
						<span class="text-[0.9375rem] font-semibold tracking-tight">ImmichFrame</span>
					</div>
					<SettingsSearch onpick={jumpTo} bind:searching={sidebarSearching} />
					{#if !sidebarSearching}
						<nav class="mt-3 flex flex-col gap-0.5" aria-label="Sections">
							{#each pages as p (p.id)}
								{@const selected = page === p.id}
								<a
									href="#{p.id}"
									class="flex items-center gap-2.5 rounded-[6px] px-2 py-[5px] text-[0.875rem] focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-primary {selected
										? 'bg-accent'
										: 'hover:bg-black/5 dark:hover:bg-white/10'}"
									aria-current={selected ? 'page' : undefined}
									onclick={(e) => {
										e.preventDefault();
										go(p.id);
									}}
								>
									{@render tile(p, 24)}
									{p.title}
								</a>
							{/each}
						</nav>
					{/if}
				</div>
				<!-- Window actions as sidebar items; status and version as small print. -->
				<div class="flex flex-col gap-0.5">
					<a
						href={resolve('/')}
						target="_blank"
						class="flex items-center gap-2.5 rounded-[6px] px-2 py-[5px] text-[0.875rem] hover:bg-black/5 dark:hover:bg-white/10"
					>
						<span class="text-label-2 flex w-6 justify-center"
							><Glyph name="externalLink" size={16} /></span
						>
						Open Frame
					</a>
					<button
						type="button"
						class="flex items-center gap-2.5 rounded-[6px] px-2 py-[5px] text-left text-[0.875rem] hover:bg-black/5 dark:hover:bg-white/10"
						onclick={onlogout}
					>
						<span class="text-label-2 flex w-6 justify-center"><Glyph name="lock" size={16} /></span
						>
						Lock
					</button>
					<!-- Status as small print, like the version: Screens in the list above has the details. -->
					<div class="text-label-2 mt-2 px-2 text-[0.6875rem] leading-relaxed">
						{#if statusText}
							<p class="flex items-center gap-1.5">
								<span
									class="h-1.5 w-1.5 rounded-full {liveScreens ? 'bg-[#30d158]' : 'bg-[#8e8e93]'}"
								></span>
								{statusText}
							</p>
						{/if}
						{#if frameVersion}<p>ImmichFrame {frameVersion}</p>{/if}
					</div>
				</div>
			</aside>
		</div>

		<main
			class="relative min-w-0 flex-1 px-4 pb-36 md:px-10 md:pt-10 {hashPage === null
				? 'hidden md:block'
				: 'pt-4'}"
		>
			<div class="mx-auto max-w-[44rem]">
				{#key page}
					<div in:fly={{ y: 6, duration: 220, easing: cubicOut }}>
						{#if drill}
							<!-- Desktop only: the phone's nav bar already carries the back button. -->
							<button
								type="button"
								class="-ml-1 mb-1 hidden items-center gap-0.5 rounded-md text-[0.875rem] text-primary focus-visible:outline-2 focus-visible:outline-primary md:inline-flex"
								onclick={back}
							>
								<Glyph name="chevronLeft" size={18} stroke={2.2} />
								{drill.back}
							</button>
						{/if}
						<h1
							class="text-[1.75rem] leading-tight font-bold tracking-[-0.02em] md:text-[2rem] md:tracking-[-0.025em]"
						>
							{title}
						</h1>
						{#if drill}
							<div class="mb-7"></div>
						{:else}
							<p class="text-label-2 mt-1 mb-7 text-[0.9375rem]">{current.intro}</p>
						{/if}

						<div class="flex flex-col gap-7">
							{#if page === 'photos'}
								<PagePhotos
									settings={editor.settings}
									version={editor.version}
									onaddaccount={addAccount}
									bind:selected={photosAccount}
								/>
								{#if photosAccount === null}<UpNext {preview} />{/if}
							{:else if page === 'look'}
								<LivePreview general={editor.settings.General ?? {}} {preview} />
								{#key editor.version}
									<PageLook
										general={editor.settings.General ?? (editor.settings.General = {})}
										bind:captions={lookCaptions}
									/>
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
										onaddaccount={(account) =>
											(editor.settings.Accounts = [...(editor.settings.Accounts ?? []), account])}
										startNew={startNewAccount}
										onstartednew={() => (startNewAccount = false)}
										onremoveaccount={removeAccount}
										{passwordFromEnvironment}
									/>
								{/key}
								<RecentActivity
									refreshKey={savedAt}
									dirty={editor.dirty}
									accounts={editor.settings.Accounts ?? []}
									onrestored={onreload}
								/>
								<SettingsBackup saved={savedSettings} onrestore={restoreFromFile} />
							{/if}
						</div>
					</div>
				{/key}
			</div>

			<!-- The save bar floats over the content column, in translucent material. -->
			{#if barVisible}
				<div
					class="if-savebar pointer-events-none fixed inset-x-0 bottom-0 z-40"
					transition:fly={{ y: 16, duration: 200 }}
				>
					<div class="p-4 md:pl-[15rem]">
						<div
							class="pointer-events-auto mx-auto flex max-w-[44rem] flex-wrap items-center justify-between gap-3 rounded-2xl if-material bg-white/97 px-4 py-3 shadow-[0_8px_24px_rgb(0_0_0/0.12),0_0_0_0.5px_rgb(0_0_0/0.1)] backdrop-blur-[20px] backdrop-saturate-[180%] dark:bg-[rgb(30_30_30/0.97)] dark:shadow-[0_8px_24px_rgb(0_0_0/0.4),0_0_0_0.5px_rgb(255_255_255/0.12)]"
							role="region"
							aria-label="Unsaved changes"
							aria-live="polite"
						>
							{#if saveError}
								<p
									class="min-w-0 flex-1 text-[0.875rem] font-medium text-[#d70015] dark:text-[#ff6961]"
								>
									{saveError}
								</p>
							{:else if editor.dirty}
								<p class="text-label-2 min-w-0 flex-1 text-[0.8125rem]">
									{invalidFormats.size > 0
										? 'Fix the invalid date or clock pattern to save'
										: editor.changeCount === 1
											? (describeUnsavedChange(
													editor.changedKeys[0],
													(editor.settings.Accounts ?? []).map((a, i) =>
														accountLabel(a, i, peekLibrary(a))
													)
												) ?? '1 unsaved change')
											: plural(Math.max(editor.changeCount, 1), 'unsaved change')}
								</p>
							{:else}
								<div class="flex min-w-0 flex-1 flex-col">
									<span class="flex items-center gap-1.5 text-[0.875rem] font-medium">
										<span class={saveWarnings.length ? 'text-[#ff9f0a]' : 'text-[#30d158]'}>
											<Glyph
												name={saveWarnings.length ? 'triangleAlert' : 'circleCheck'}
												size={17}
												stroke={2}
											/>
										</span>
										{saveWarnings.length ? 'Saved, with a warning' : 'Saved'}
									</span>
									<p class="text-label-2 text-[0.8125rem]">
										{saveWarnings.length
											? saveWarnings.join(' ')
											: 'Screens in a browser update within a minute.'}
									</p>
								</div>
							{/if}
							{#if editor.dirty}
								<div class="flex gap-2">
									<Btn
										variant="outline"
										color="secondary"
										size="small"
										disabled={saving}
										onclick={() => editor.discard()}
									>
										{editor.changeCount > 3 ? `Discard ${editor.changeCount}` : 'Discard'}
									</Btn>
									<Btn
										size="small"
										loading={saving}
										disabled={!canSave}
										onclick={onsave}
										title="Save (⌘S)"
									>
										Save
									</Btn>
								</div>
							{:else}
								<Btn
									variant="ghost"
									color="secondary"
									size="small"
									onclick={() => (justSaved = false)}>Dismiss</Btn
								>
							{/if}
						</div>
					</div>
				</div>
			{/if}
		</main>
	</div>
</div>
