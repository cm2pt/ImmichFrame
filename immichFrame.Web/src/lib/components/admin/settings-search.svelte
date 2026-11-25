<script lang="ts" module>
	export interface SearchHit {
		title: string;
		page: string;
		pageTitle: string;
	}

	// Every setting by the title its row shows, with a few words people might type instead.
	const index: { title: string; page: string; words?: string }[] = [
		{ title: 'Favorites', page: 'photos', words: 'heart starred' },
		{ title: 'In any album', page: 'photos', words: 'all albums every album' },
		{ title: 'Full resolution', page: 'look', words: 'quality sharp 4k original high resolution' },
		{ title: 'Memories', page: 'photos', words: 'on this day birthdays' },
		{ title: 'Albums', page: 'photos' },
		{ title: 'People', page: 'photos', words: 'faces persons' },
		{ title: 'Tags', page: 'photos' },
		{ title: 'Never show', page: 'photos', words: 'hide exclude albums people tags screenshots' },
		{ title: 'Order', page: 'look', words: 'shuffle random chronological oldest newest repeat' },
		{ title: 'Ages', page: 'look', words: 'age birthday years old' },
		{ title: 'Show events for', page: 'look', words: 'calendar days ahead week upcoming' },
		{ title: 'Taken', page: 'photos', words: 'date range year' },
		{ title: 'Minimum rating', page: 'photos', words: 'stars' },
		{ title: 'Videos', page: 'photos' },
		{ title: 'Archived photos', page: 'photos', words: 'archive' },
		{
			title: 'Time on each photo',
			page: 'look',
			words: 'interval seconds duration speed delay how long'
		},
		{ title: 'Change between photos', page: 'look', words: 'transition fade' },
		{ title: 'Progress bar', page: 'look' },
		{ title: 'Layout', page: 'look', words: 'split view portrait two photos' },
		{ title: 'Photo size', page: 'look', words: 'fit fill crop' },
		{ title: 'Movement', page: 'look', words: 'zoom pan ken burns motion animation' },
		{ title: 'Video sound', page: 'look', words: 'audio mute' },
		{ title: 'Clock', page: 'look', words: 'time format' },
		{ title: 'Date taken', page: 'look', words: 'photo date format' },
		{ title: 'Place', page: 'look', words: 'location city country where gps' },
		{ title: 'Album name', page: 'look' },
		{ title: 'Description', page: 'look', words: 'caption' },
		{
			title: 'Behind the clock and captions',
			page: 'look',
			words: 'style blur frosted solid background box readable'
		},
		{ title: 'Text color', page: 'look', words: 'colour primary' },
		{ title: 'Box color', page: 'look', words: 'colour background secondary' },
		{ title: 'Text size', page: 'look', words: 'font size bigger' },
		{ title: 'Language', page: 'look', words: 'locale' },
		{ title: 'Weather', page: 'look', words: 'openweathermap temperature' },
		{ title: 'Calendars', page: 'look', words: 'ics events appointments' },
		{ title: 'Add a screen', page: 'screens', words: 'device apple tv ipad qr code new' },
		{ title: 'Immich accounts', page: 'access', words: 'api key server account connection' },
		{ title: 'Frame secret', page: 'access', words: 'authentication password screens' },
		{ title: 'Admin password', page: 'access', words: 'login sign in' },
		{ title: 'Keep images on disk', page: 'access', words: 'cache download' },
		{ title: 'Refresh albums and people', page: 'access', words: 'interval' },
		{ title: 'Webhook', page: 'access', words: 'home assistant automation' },
		{ title: 'Clock & Captions', page: 'look', words: 'overlay text corners info captions' },
		{ title: 'Location', page: 'look', words: 'weather city coordinates latitude longitude where' },
		{ title: 'Units', page: 'look', words: 'celsius fahrenheit metric imperial temperature' },
		{ title: 'Weather description', page: 'look', words: 'rain sunny forecast' },
		{ title: 'Screens', page: 'screens', words: 'devices tv apple tv ipad frames connected' },
		{ title: 'Recent changes', page: 'access', words: 'history undo restore' },
		{ title: 'Backup', page: 'access', words: 'download export import restore' },
		{ title: 'Preview', page: 'look', words: 'live preview' },
		{ title: 'Up next', page: 'photos', words: 'sample shuffle' }
	];

	const pageTitles: Record<string, string> = {
		photos: 'Photos',
		look: 'Slideshow & Look',
		screens: 'Screens',
		access: 'Access & Accounts'
	};
</script>

<script lang="ts">
	import Glyph from './glyph.svelte';

	interface Props {
		onpick: (hit: SearchHit) => void;
		autofocus?: boolean;
		/** True while there's a query: the results take the place of the sections list. */
		searching?: boolean;
		/** The phone's root list shows results as a grouped card; the sidebar as plain rows. */
		variant?: 'sidebar' | 'list';
	}

	let {
		onpick,
		autofocus = false,
		searching = $bindable(false),
		variant = 'sidebar'
	}: Props = $props();

	let query = $state('');
	let active = $state(0);
	let input: HTMLInputElement | undefined = $state();

	// Every word typed must start a word of the title or its keywords ("zoom" finds Movement,
	// "weather loc" finds Location). Title matches come first.
	let hits: SearchHit[] = $derived.by(() => {
		const words = query.trim().toLowerCase().split(/\s+/).filter(Boolean);
		if (!words.length) return [];
		const starts = (text: string, w: string) =>
			text.startsWith(w) || text.includes(` ${w}`) || text.includes(`-${w}`);
		return index
			.map((entry) => {
				const title = entry.title.toLowerCase();
				const all = `${title} ${entry.words ?? ''}`;
				if (!words.every((w) => starts(all, w))) return null;
				const score = title.startsWith(words[0]) ? 0 : words.every((w) => starts(title, w)) ? 1 : 2;
				return { entry, score };
			})
			.filter((r): r is { entry: (typeof index)[number]; score: number } => r !== null)
			.sort((a, b) => a.score - b.score)
			.map(({ entry }) => ({
				title: entry.title,
				page: entry.page,
				pageTitle: pageTitles[entry.page]
			}));
	});
	// Shown grouped by page, in the sidebar's page order, as System Settings does.
	let groups = $derived(
		Object.keys(pageTitles)
			.map((page) => ({ page, title: pageTitles[page], hits: hits.filter((h) => h.page === page) }))
			.filter((g) => g.hits.length)
	);
	let ordered = $derived(groups.flatMap((g) => g.hits));
	$effect(() => {
		searching = query.trim().length > 0;
	});

	$effect(() => {
		void hits;
		active = 0;
	});

	$effect(() => {
		if (autofocus) input?.focus();
	});

	function pick(hit: SearchHit) {
		query = '';
		input?.blur();
		onpick(hit);
	}

	function onKeydown(e: KeyboardEvent) {
		if (e.key === 'ArrowDown') {
			e.preventDefault();
			active = Math.min(active + 1, ordered.length - 1);
		} else if (e.key === 'ArrowUp') {
			e.preventDefault();
			active = Math.max(active - 1, 0);
		} else if (e.key === 'Enter' && ordered[active]) {
			e.preventDefault();
			pick(ordered[active]);
		} else if (e.key === 'Escape') {
			query = '';
		}
	}
</script>

<div class="relative">
	<div class="bg-fill flex items-center gap-1.5 rounded-lg px-2 py-1.5">
		<Glyph name="search" size={15} class="text-label-2" />
		<input
			bind:this={input}
			bind:value={query}
			type="search"
			placeholder="Search"
			aria-label="Search settings"
			role="combobox"
			aria-expanded={ordered.length > 0}
			aria-controls="settings-search-results"
			aria-activedescendant={ordered.length ? `search-hit-${active}` : undefined}
			autocomplete="off"
			class="w-full min-w-0 bg-transparent text-[0.875rem] outline-none placeholder:text-[#6e6e73] [&::-webkit-search-cancel-button]:hidden dark:placeholder:text-[#a1a1a6]"
			onkeydown={onKeydown}
		/>
		{#if query}
			<button
				type="button"
				class="text-label-2 rounded-full p-0.5"
				aria-label="Clear search"
				onclick={() => (query = '')}
			>
				<Glyph name="x" size={13} stroke={2.2} />
			</button>
		{/if}
	</div>
	{#if query.trim()}
		<div
			id="settings-search-results"
			role="listbox"
			aria-label="Search results"
			class={variant === 'list' ? 'mt-5 flex flex-col gap-5' : 'mt-3 flex flex-col gap-3'}
		>
			{#each groups as group (group.page)}
				<div role="group" aria-label={group.title}>
					<p
						class="text-label-2 mb-1 px-2 {variant === 'list'
							? 'px-4 text-[0.8125rem] uppercase'
							: 'text-[0.6875rem] font-semibold'}"
					>
						{group.title}
					</p>
					<div class={variant === 'list' ? 'bg-group rounded-group px-4' : ''}>
						{#each group.hits as hit (hit.page + hit.title)}
							{@const i = ordered.indexOf(hit)}
							<button
								id="search-hit-{i}"
								type="button"
								role="option"
								aria-selected={i === active}
								class={variant === 'list'
									? `border-sep flex min-h-11 w-full items-center justify-between border-t py-2 text-left text-[1.0625rem] first:border-t-0`
									: `flex w-full rounded-[6px] px-2 py-[5px] text-left text-[0.875rem] ${i === active ? 'bg-accent' : 'hover:bg-black/5 dark:hover:bg-white/10'}`}
								onmouseenter={() => (active = i)}
								onclick={() => pick(hit)}
							>
								{hit.title}
								{#if variant === 'list'}<Glyph
										name="chevronRight"
										size={16}
										class="text-label-3"
									/>{/if}
							</button>
						{/each}
					</div>
				</div>
			{:else}
				<p class="text-label-2 px-2 py-2 text-center text-[0.875rem]">
					No Results for “{query.trim()}”
				</p>
			{/each}
		</div>
	{/if}
</div>
